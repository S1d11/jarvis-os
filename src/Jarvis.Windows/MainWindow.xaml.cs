using System;
using System.IO;
using System.Threading.Tasks;
using Jarvis.Core;
using Jarvis.Core.Shell;
using Jarvis.Core.Services;
using Microsoft.Web.WebView2.Core;
using System.Windows;
using WpfMessageBox = System.Windows.MessageBox;

namespace Jarvis.Windows;

/// <summary>
/// The Siri-style assistant overlay window.
///
/// Transparent, borderless, always-on-top. Covers the entire screen
/// so it can catch clicks outside the UI area to dismiss itself.
/// The HTML/CSS renders the actual UI (centered input bar + chat).
/// The rest of the window is transparent, showing your desktop behind it.
///
/// This is NOT a shell replacement. It coexists with Explorer,
/// appearing over whatever you're doing like Spotlight or Siri.
/// </summary>
public partial class MainWindow : Window, IBridgeHost
{
    private readonly Bridge _bridge;
    private readonly WindowsSystemAccess _sys;
    private readonly WindowService _winSvc;
    private bool _closeRequested;

    /// <summary>Fired when the user dismisses the overlay (Escape or click-outside).</summary>
    public event Action? OverlayDismissed;

    public MainWindow()
    {
        InitializeComponent();

        _sys = new WindowsSystemAccess();
        var shell = new ShellService(_sys);
        var sysCtrl = new SystemControlService(_sys);
        var proc = new ProcessService();
        _winSvc = new WindowService(new WindowsWindowAccess());
        _bridge = new Bridge(this, shell, sysCtrl, proc, _winSvc);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await InitializeWebView();
        }
        catch (Exception ex)
        {
            WpfMessageBox.Show(this, "Failed to initialize WebView2.\n" + ex.Message,
                "Jarvis", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }

        EnableDarkTitleBar();
    }

    /// <summary>Clicking the transparent background dismisses the overlay.
    /// The WebView2 handles clicks inside the UI area.</summary>
    private void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Only dismiss on left-click. Let right-click pass through.
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            HideOverlay();
        }
    }

    /// <summary>Closing hides instead of exiting.</summary>
    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_closeRequested)
        {
            e.Cancel = true;
            HideOverlay();
        }
    }

    private void EnableDarkTitleBar()
    {
        try
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var darkMode = 0x01;
            DwmSetWindowAttribute(hwnd, 20, ref darkMode, sizeof(int));
        }
        catch { /* non-critical */ }
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private async Task InitializeWebView()
    {
        await WebView.EnsureCoreWebView2Async();
        var core = WebView.CoreWebView2;

        var webRoot = ExtractWebAssets();
        core.SetVirtualHostNameToFolderMapping("jarvis.app", webRoot,
            CoreWebView2HostResourceAccessKind.Allow);
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
        core.Settings.UserAgent = "Jarvis/1.0 (Windows)";

        core.WebMessageReceived += OnWebMessageReceived;

        var cacheBuster = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        core.Navigate($"https://jarvis.app/index.html?v={cacheBuster}");
    }

    private static string ExtractWebAssets()
    {
        var root = Path.Combine(App.DataDir, "web");
        if (Directory.Exists(root)) Directory.Delete(root, true);
        Directory.CreateDirectory(root);

        var asm = typeof(Jarvis.Core.AppContext).Assembly;
        var resPrefix = asm.GetName().Name + ".Web";
        var manifestName = resPrefix + ".manifest.txt";

        using var manifestStream = asm.GetManifestResourceStream(manifestName);
        if (manifestStream == null) return root;

        using var sr = new StreamReader(manifestStream);
        string? line;
        while ((line = sr.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var relPath = line.Trim();
            var filePart = relPath.StartsWith("Web/", StringComparison.OrdinalIgnoreCase)
                ? relPath["Web/".Length..]
                : relPath;

            var dest = Path.Combine(root, filePart.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            var resName = resPrefix + "." + filePart.Replace('/', '.');
            using var s = asm.GetManifestResourceStream(resName);
            if (s == null) continue;
            using var f = File.Create(dest);
            s.CopyTo(f);
        }
        return root;
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.TryGetWebMessageAsString();

            // Intercept overlay-specific messages before routing to the bridge
            if (json.Contains("\"action\":\"overlay.dismiss\""))
            {
                HideOverlay();
                return;
            }

            await _bridge.HandleMessageAsync(json);
        }
        catch (Exception ex)
        {
            _bridge.PostToWeb(new { @event = "error", message = ex.Message });
        }
    }

    // ── Overlay lifecycle ──────────────────────────────────────

    /// <summary>Show the overlay (called from keyboard hook or orb click).</summary>
    public void ShowOverlay()
    {
        Dispatcher.Invoke(() =>
        {
            Show();
            Activate();
            // Slight flash of topmost then release so it grabs focus
            // but doesn't permanently block other windows
            Topmost = true;
        });
    }

    /// <summary>Hide the overlay back to background.</summary>
    public void HideOverlay()
    {
        Dispatcher.Invoke(() =>
        {
            Hide();
            OverlayDismissed?.Invoke();
        });
    }

    /// <summary>Force close (for app shutdown / uninstall).</summary>
    public void ForceClose()
    {
        _closeRequested = true;
        Dispatcher.Invoke(() => Close());
    }

    // ── IBridgeHost ────────────────────────────────────────────
    public void PostMessage(string json) => Dispatcher.Invoke(() =>
        WebView.CoreWebView2?.PostWebMessageAsJson(json));

    public void NavigateReload() => Dispatcher.Invoke(() => WebView.CoreWebView2?.Reload());
    public void ToggleDevTools() => Dispatcher.Invoke(() =>
    {
        if (WebView.CoreWebView2 != null)
            WebView.CoreWebView2.OpenDevToolsWindow();
    });
    public void SetZoom(double z) => Dispatcher.Invoke(() => WebView.ZoomFactor = z);

    public void BringToFront() => ShowOverlay();

    public void CloseApp() => HideOverlay();
}
