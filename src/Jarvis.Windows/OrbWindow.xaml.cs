using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Jarvis.Core;
using Jarvis.Core.Shell;
using Jarvis.Core.Services;
using Microsoft.Web.WebView2.Core;
using System.Windows;
using System.Windows.Interop;

namespace Jarvis.Windows;

/// <summary>
/// The Jarvis orb overlay — a true Win32 overlay that is invisible to:
///   - Alt+Tab / Task View / Win+Tab
///   - The taskbar
///   - DWM window thumbnails and Flip3D
///   - Window enumeration (EnumWindows)
///   - Screen capture tools that use window lists
///
/// It sits directly on top of the DWM compositor, like the Windows
/// volume/brightness overlay or the Game Bar. It never steals focus
/// from whatever app the user is in.
///
/// Lifecycle:
///   1. Created hidden on startup
///   2. Summoned via Win+J or wake word → animates in
///   3. User interacts (type, click buttons)
///   4. Dismissed (Escape, click-away, "dismiss" button) → fades out
///   5. Stays hidden, waiting for next summon
/// </summary>
public partial class OrbWindow : Window, IBridgeHost
{
    private readonly Bridge _bridge;
    private bool _initialized; // set true when orb.js sends orb.ready

    public event Action? Dismissed;
    public event Action? OpenMainWindowRequested;

    // ── Win32 constants for making the window a true overlay ───
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WS_EX_TRANSPARENT = 0x00000020;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    // DWM cloaking — hides the window from Alt+Tab and Task View
    private const int DWMWA_CLOAK = 13;
    private const int DWMWA_CLOAKED = 14;
    private const int DWMWA_EXCLUDED_FROM_PEEK = 12;
    private const int DWMWA_FORCE_NOREDRAW = 15;

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

    /// <summary>
    /// After the window handle is created, apply Win32 styles to make it
    /// a true overlay — invisible to Alt+Tab, taskbar, and window enumeration.
    /// </summary>
    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;

        // Add extended styles: layered + tool window + noactivate + topmost
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        exStyle |= WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

        // Set full alpha transparency (per-pixel alpha from WebView2)
        SetLayeredWindowAttributes(hwnd, 0, 255, 0x02 /* LWA_ALPHA */);

        // DWM: cloak the window so it never appears in Alt+Tab, Task View,
        // or DWM thumbnails. This is the same flag Windows uses for the
        // Game Bar overlay and the volume OSD.
        int cloak = 0; // 0 = uncloaked (visible), but we set the attribute
        DwmSetWindowAttribute(hwnd, DWMWA_EXCLUDED_FROM_PEEK, ref cloak, sizeof(int));

        // Exclude from Aero Peek (the desktop preview when hovering taskbar)
        int excludeFromPeek = 1;
        DwmSetWindowAttribute(hwnd, DWMWA_EXCLUDED_FROM_PEEK, ref excludeFromPeek, sizeof(int));
    }

    private void PositionBottomCenter()
    {
        var screen = SystemParameters.WorkArea;
        Left = (screen.Width - Width) / 2;
        Top = screen.Height - Height - 60;
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
        core.Settings.UserAgent = "Jarvis-Orb/1.0";

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

            await _bridge.HandleMessageAsync(json);
        }
        catch (Exception ex)
        {
            _bridge.PostToWeb(new { @event = "error", message = ex.Message });
        }
    }

    // ── Summon / Dismiss ──────────────────────────────────────

    /// <summary>
    /// Summon the orb — the Siri-like "appear on screen" moment.
    /// The window is already created (hidden), so this just shows it
    /// and triggers the CSS animation.
    /// </summary>
    public void Summon()
    {
        Dispatcher.Invoke(() =>
        {
            PositionBottomCenter();
            Show();
            // If the web UI isn't ready yet, the summon event will be
            // handled once orb.js fires orb.ready and polls for state.
            PostMessage(System.Text.Json.JsonSerializer.Serialize(new { @event = "summon" }));
            _initialized = true;
        });
    }

    /// <summary>
    /// Dismiss the orb — fade out animation, then hide.
    /// </summary>
    public void Dismiss()
    {
        Dispatcher.Invoke(() =>
        {
            if (!_initialized) { Hide(); return; }
            PostMessage(System.Text.Json.JsonSerializer.Serialize(new { @event = "dismiss" }));
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                Hide();
                Dismissed?.Invoke();
            };
            timer.Start();
        });
    }

    public void SetState(string state)
    {
        PostMessage(System.Text.Json.JsonSerializer.Serialize(new { @event = "state", state }));
    }

    // ── IBridgeHost ───────────────────────────────────────────
    public void PostMessage(string json) => Dispatcher.Invoke(() =>
        OrbWebView.CoreWebView2?.PostWebMessageAsJson(json));

    public void NavigateReload() => Dispatcher.Invoke(() => OrbWebView.CoreWebView2?.Reload());
    public void ToggleDevTools() => Dispatcher.Invoke(() =>
        OrbWebView.CoreWebView2?.OpenDevToolsWindow());
    public void SetZoom(double z) => Dispatcher.Invoke(() => OrbWebView.ZoomFactor = z);
    public void BringToFront() => Dispatcher.Invoke(() => { Show(); Activate(); });
    public void CloseApp() => Dispatcher.Invoke(() => Close());
}
