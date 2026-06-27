using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Jarvis.Core;
using Jarvis.Core.Shell;
using Jarvis.Core.Services;
using Microsoft.Web.WebView2.Core;
using System.Windows;
using System.Windows.Controls;
using WpfMessageBox = System.Windows.MessageBox;

namespace Jarvis.Windows;

public partial class MainWindow : Window, IBridgeHost
{
    private NotifyIconHelper? _tray;
    private bool _reallyClose;
    private readonly Bridge _bridge;
    private readonly WindowsSystemAccess _sys;
    private readonly WindowService _winSvc;

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

        _tray = new NotifyIconHelper(this);
        EnableDarkTitleBar();

        // Shell mode: maximize to fill the screen
        if (Properties.Settings.Default.ShellMode)
        {
            WindowState = WindowState.Maximized;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
        }

        // Start minimized to tray if configured
        if (Properties.Settings.Default.StartMinimized)
        {
            Hide();
            _tray?.ShowBalloon("Jarvis", "Jarvis is running in the background. Click the tray icon to open.");
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
        core.Settings.AreDevToolsEnabled = true;
        core.Settings.AreDefaultContextMenusEnabled = true;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = true;
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
            await _bridge.HandleMessageAsync(json);
        }
        catch (Exception ex)
        {
            _bridge.PostToWeb(new { @event = "error", message = ex.Message });
        }
    }

    // ── IBridgeHost implementation ──────────────────────────
    public void PostMessage(string json) => Dispatcher.Invoke(() =>
        WebView.CoreWebView2?.PostWebMessageAsJson(json));

    public void NavigateReload() => Dispatcher.Invoke(() => WebView.CoreWebView2?.Reload());

    public void ToggleDevTools() => Dispatcher.Invoke(() =>
    {
        if (WebView.CoreWebView2 != null)
            WebView.CoreWebView2.OpenDevToolsWindow();
    });

    public void SetZoom(double z) => Dispatcher.Invoke(() => WebView.ZoomFactor = z);

    public void BringToFront()
    {
        Dispatcher.Invoke(() =>
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        });
    }

    public void CloseApp()
    {
        _reallyClose = true;
        Dispatcher.Invoke(() => Close());
    }

    // ── Tray / Close behavior ───────────────────────────────
    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized) Hide();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        var behavior = Jarvis.Core.AppContext.Current.Config.Current.CloseBehavior;

        if (behavior == "quit" || _reallyClose)
        {
            _tray?.Dispose();
            return;
        }

        e.Cancel = true;
        Hide();
        _tray?.ShowBalloon("Jarvis", "Jarvis is still running in the background.");
    }
}
