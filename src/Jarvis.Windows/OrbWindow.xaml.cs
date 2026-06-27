using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Jarvis.Core;
using Jarvis.Core.Shell;
using Jarvis.Core.Services;
using Microsoft.Web.WebView2.Core;
using System.Windows;

namespace Jarvis.Windows;

/// <summary>
/// The floating Siri-like orb overlay. This is a separate transparent, always-on-top
/// window that doesn't steal focus from the current app. When the user triggers Jarvis
/// (via hotkey or wake word), this window animates in with the orb, shows the assistant
/// panel, and fades out when done.
///
/// Key behaviors:
///   - Transparent background, no title bar, no taskbar entry
///   - Always on top of every other window
///   - Does NOT steal focus (ShowActivated=False) so the user keeps working
///   - Positioned in the center-bottom of the screen
///   - Click outside or press Escape to dismiss
/// </summary>
public partial class OrbWindow : Window, IBridgeHost
{
    private readonly Bridge _bridge;
    private bool _initialized;

    /// <summary>Called when the user dismisses the orb (Escape, click away, or "go away").</summary>
    public event Action? Dismissed;

    /// <summary>Called when the orb wants the main window to open (e.g. user clicks "open full view").</summary>
    public event Action? OpenMainWindowRequested;

    public OrbWindow()
    {
        InitializeComponent();

        var sys = new WindowsSystemAccess();
        var shell = new ShellService(sys);
        var sysCtrl = new SystemControlService(sys);
        var proc = new ProcessService();
        var winSvc = new WindowService(new WindowsWindowAccess());
        _bridge = new Bridge(this, shell, sysCtrl, proc, winSvc);

        PositionBottomCenter();
    }

    private void PositionBottomCenter()
    {
        var screen = SystemParameters.WorkArea;
        Width = 480;
        Height = 600;
        Left = (screen.Width - Width) / 2;
        Top = screen.Height - Height - 80;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await InitializeWebView();
    }

    private async Task InitializeWebView()
    {
        await OrbWebView.EnsureCoreWebView2Async();
        var core = OrbWebView.CoreWebView2;

        var webRoot = ExtractOrbAssets();
        core.SetVirtualHostNameToFolderMapping("jarvis-orb.app", webRoot,
            CoreWebView2HostResourceAccessKind.Allow);

        core.Settings.AreDevToolsEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
        core.Settings.UserAgent = "Jarvis-Orb/1.0 (Windows)";

        // Transparent background for WebView2 (set via XAML DefaultBackgroundColor)

        core.WebMessageReceived += OnWebMessageReceived;

        var cacheBuster = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        core.Navigate($"https://jarvis-orb.app/orb.html?v={cacheBuster}");
    }

    private static string ExtractOrbAssets()
    {
        var root = Path.Combine(App.DataDir, "web");
        if (!Directory.Exists(root)) Directory.CreateDirectory(root);

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
            var dest = Path.Combine(root, relPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            var resName = resPrefix + "." + relPath.Replace('/', '.');
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

            // Handle orb-specific actions
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var action = doc.RootElement.TryGetProperty("action", out var a) ? a.GetString() : null;

            switch (action)
            {
                case "orb.dismiss":
                    Dismiss();
                    return;
                case "orb.openFull":
                    OpenMainWindowRequested?.Invoke();
                    Dismiss();
                    return;
                case "orb.ready":
                    _initialized = true;
                    return;
            }

            // Forward everything else to the bridge
            await _bridge.HandleMessageAsync(json);
        }
        catch (Exception ex)
        {
            _bridge.PostToWeb(new { @event = "error", message = ex.Message });
        }
    }

    /// <summary>
    /// Show the orb with a Siri-like appear animation.
    /// Called by the hotkey service or wake word service.
    /// </summary>
    public void Summon()
    {
        PositionBottomCenter();
        Show();
        // Tell the web UI to play the summon animation
        PostMessage(System.Text.Json.JsonSerializer.Serialize(new { @event = "summon" }));
    }

    /// <summary>
    /// Dismiss the orb with a fade-out animation.
    /// </summary>
    public void Dismiss()
    {
        PostMessage(System.Text.Json.JsonSerializer.Serialize(new { @event = "dismiss" }));
        // Wait for animation, then hide
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        timer.Tick += (_, _) => { timer.Stop(); Hide(); Dismissed?.Invoke(); };
        timer.Start();
    }

    /// <summary>
    /// Set the orb state (idle, listening, thinking, responding).
    /// </summary>
    public void SetState(string state)
    {
        PostMessage(System.Text.Json.JsonSerializer.Serialize(new { @event = "state", state }));
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        // When the orb loses focus (user clicks elsewhere), dismiss it
        // like Siri does. But only after a short delay to avoid dismissing
        // during the initial show.
        if (_initialized)
        {
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                if (!IsFocused) Dismiss();
            };
            timer.Start();
        }
    }

    // ── IBridgeHost ──────────────────────────────────────────
    public void PostMessage(string json) => Dispatcher.Invoke(() =>
        OrbWebView.CoreWebView2?.PostWebMessageAsJson(json));

    public void NavigateReload() => Dispatcher.Invoke(() => OrbWebView.CoreWebView2?.Reload());
    public void ToggleDevTools() => Dispatcher.Invoke(() =>
        OrbWebView.CoreWebView2?.OpenDevToolsWindow());
    public void SetZoom(double z) => Dispatcher.Invoke(() => OrbWebView.ZoomFactor = z);
    public void BringToFront() => Dispatcher.Invoke(() => { Show(); Activate(); });
    public void CloseApp() => Dispatcher.Invoke(() => Close());
}
