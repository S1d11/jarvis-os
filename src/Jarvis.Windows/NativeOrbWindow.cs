using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Jarvis.Core;
using Jarvis.Core.Shell;
using Jarvis.Core.Services;
using Microsoft.Web.WebView2.Core;

namespace Jarvis.Windows;

/// <summary>
/// Native Win32 orb overlay — zero WPF dependency.
///
/// This is NOT a WPF Window. It's a raw Win32 HWND created via CreateWindowEx,
/// with a custom WndProc that runs on a dedicated message pump thread. WebView2
/// is hosted directly via its COM controller (CoreWebView2Controller), not through
/// the WPF WebView2 wrapper.
///
/// The window is created with these styles to make it a true OS-level overlay:
///   WS_POPUP              — no title bar, no border, no chrome
///   WS_EX_LAYERED         — per-pixel alpha transparency
///   WS_EX_NOACTIVATE      — never steals focus from the current app
///   WS_EX_TOOLWINDOW      — invisible to Alt+Tab, Task View, Win+Tab
///   WS_EX_TOPMOST         — always above all other windows
///   WS_EX_TRANSPARENT     — click-through when hidden (removed when visible)
///
/// Additionally, DwmSetWindowAttribute is used to:
///   - Exclude from Aero Peek (DWMWA_EXCLUDED_FROM_PEEK)
///   - Set dark mode title bar (irrelevant — no title bar, but harmless)
///
/// This is the same approach Windows itself uses for:
///   - The volume/brightness OSD
///   - Game Bar overlay
///   - Input method editor (IME) windows
///   - Toast notification popups
/// </summary>
public sealed class NativeOrbWindow : IBridgeHost, IDisposable
{
    // ── Win32 constants ───────────────────────────────────────
    private const string ClassName = "JarvisOrbOverlay";

    private const uint WS_POPUP = 0x80000000;
    private const uint WS_VISIBLE = 0x10000000;
    private const uint WS_CLIPSIBLINGS = 0x04000000;
    private const uint WS_CLIPCHILDREN = 0x02000000;

    private const int WS_EX_LAYERED = 0x00080000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOPMOST = 0x00000008;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_NOREDIRECTIONBITMAP = 0x00200000;

    private const int GWL_EXSTYLE = -20;
    private const int GWL_STYLE = -16;

    private const uint SWP_NOACTIVATE = 0x0010;
    private const uint SWP_SHOWWINDOW = 0x0040;
    private const uint SWP_HIDEWINDOW = 0x0080;
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;

    private const int HWND_TOPMOST = -1;

    private const uint LWA_ALPHA = 0x02;

    private const int DWMWA_EXCLUDED_FROM_PEEK = 12;

    private const int WM_DESTROY = 0x0002;
    private const int WM_NCDESTROY = 0x0082;
    private const int WM_SIZE = 0x0005;
    private const int WM_KEYDOWN = 0x0100;
    private const int VK_ESCAPE = 0x1B;

    // ── P/Invoke ──────────────────────────────────────────────
    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr CreateWindowEx(int dwExStyle, string lpClassName, string lpWindowName,
        int dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetLayeredWindowAttributes(IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern short UnregisterClass(string lpClassName, IntPtr hInstance);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct WNDCLASSEX
    {
        public int cbSize;
        public int style;
        public WndProcDelegate lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern short RegisterClassEx(ref WNDCLASSEX lpwcx);

    // ── State ─────────────────────────────────────────────────
    private IntPtr _hwnd;
    private IntPtr _hinstance;
    private WndProcDelegate? _wndProc; // prevent GC
    private CoreWebView2Environment? _env;
    private CoreWebView2Controller? _controller;
    private CoreWebView2? _core;
    private readonly Bridge _bridge;
    private readonly Thread _messageThread;
    private bool _disposed;
    private bool _initialized;
    private bool _summonPending;

    public event Action? Dismissed;
    public event Action? OpenMainWindowRequested;

    private const int OrbWidth = 480;
    private const int OrbHeight = 620;

    public NativeOrbWindow()
    {
        var sys = new WindowsSystemAccess();
        var shell = new ShellService(sys);
        var sysCtrl = new SystemControlService(sys);
        var proc = new ProcessService();
        var winSvc = new WindowService(new WindowsWindowAccess());
        _bridge = new Bridge(this, shell, sysCtrl, proc, winSvc);

        // Create the window on a dedicated thread (Win32 requires the
        // creating thread to pump messages for the window)
        _messageThread = new Thread(MessageThreadProc)
        {
            Name = "JarvisOrb",
            IsBackground = true,
        };
        _messageThread.SetApartmentState(ApartmentState.STA);
        _messageThread.Start();

        // Wait for the window to be created
        _createdEvent.WaitOne();
    }

    private readonly AutoResetEvent _createdEvent = new(false);

    private void MessageThreadProc()
    {
        var hMod = GetModuleHandle(null!);
        _hinstance = hMod != IntPtr.Zero ? hMod : Marshal.GetHINSTANCE(typeof(NativeOrbWindow).Assembly.GetModules()[0]);

        // Register the window class
        _wndProc = WndProc;
        var wc = new WNDCLASSEX
        {
            cbSize = Marshal.SizeOf<WNDCLASSEX>(),
            style = 0,
            lpfnWndProc = _wndProc,
            cbClsExtra = 0,
            cbWndExtra = 0,
            hInstance = _hinstance,
            hIcon = IntPtr.Zero,
            hCursor = IntPtr.Zero,
            hbrBackground = IntPtr.Zero, // no background — transparent
            lpszMenuName = null!,
            lpszClassName = ClassName,
            hIconSm = IntPtr.Zero,
        };
        RegisterClassEx(ref wc);

        // Calculate position — bottom center of the work area
        var screen = System.Windows.SystemParameters.WorkArea;
        int x = (int)((screen.Width - OrbWidth) / 2);
        int y = (int)(screen.Height - OrbHeight - 60);

        // Create the window — pure Win32, no WPF
        int exStyle = unchecked((int)(WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE |
                      WS_EX_TOPMOST | WS_EX_NOREDIRECTIONBITMAP));
        int style = unchecked((int)(WS_POPUP | WS_CLIPSIBLINGS | WS_CLIPCHILDREN));

        _hwnd = CreateWindowEx(exStyle, ClassName, "", style,
            x, y, OrbWidth, OrbHeight,
            IntPtr.Zero, IntPtr.Zero, _hinstance, IntPtr.Zero);

        if (_hwnd != IntPtr.Zero)
        {
            // Set full alpha (per-pixel alpha comes from WebView2)
            SetLayeredWindowAttributes(_hwnd, 0, 255, LWA_ALPHA);

            // Exclude from Aero Peek
            int exclude = 1;
            DwmSetWindowAttribute(_hwnd, DWMWA_EXCLUDED_FROM_PEEK, ref exclude, sizeof(int));

            // Initialize WebView2 on this thread
            _ = InitializeWebViewAsync();

            // Signal that the window is created
            _createdEvent.Set();

            // Message pump — this keeps the thread alive
            MSG msg;
            while (GetMessage(out msg, IntPtr.Zero, 0, 0) > 0)
            {
                TranslateMessage(ref msg);
                DispatchMessage(ref msg);
            }
        }
        else
        {
            _createdEvent.Set();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSG
    {
        public IntPtr hWnd;
        public int message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public int pt_x;
        public int pt_y;
    }

    [DllImport("user32.dll")]
    private static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

    [DllImport("user32.dll")]
    private static extern bool TranslateMessage(ref MSG lpMsg);

    [DllImport("user32.dll")]
    private static extern IntPtr DispatchMessage(ref MSG lpMsg);

    private IntPtr WndProc(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_KEYDOWN:
                if (wParam.ToInt32() == VK_ESCAPE)
                {
                    Dismiss();
                }
                break;

            case WM_SIZE:
                // Resize the WebView2 controller to match the window
                if (_controller != null)
                {
                    int width = lParam.ToInt32() & 0xFFFF;
                    int height = (lParam.ToInt32() >> 16) & 0xFFFF;
                    _controller.Bounds = new System.Drawing.Rectangle(0, 0, width, height);
                }
                break;

            case WM_DESTROY:
                PostQuitMessage(0);
                break;
        }

        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    [DllImport("user32.dll")]
    private static extern void PostQuitMessage(int nExitCode);

    // ── WebView2 initialization (direct COM, no WPF) ──────────
    private async Task InitializeWebViewAsync()
    {
        try
        {
            _env = await CoreWebView2Environment.CreateAsync(null, null, null);

            _controller = await _env.CreateCoreWebView2ControllerAsync(_hwnd, null);

            _controller.Bounds = new System.Drawing.Rectangle(0, 0, OrbWidth, OrbHeight);
            _controller.DefaultBackgroundColor = System.Drawing.Color.Transparent;

            _core = _controller.CoreWebView2;

            var webRoot = ExtractOrbAssets();
            _core.SetVirtualHostNameToFolderMapping("jarvis-orb.app", webRoot,
                CoreWebView2HostResourceAccessKind.Allow);

            _core.Settings.AreDevToolsEnabled = false;
            _core.Settings.AreDefaultContextMenusEnabled = false;
            _core.Settings.IsStatusBarEnabled = false;
            _core.Settings.IsZoomControlEnabled = false;
            _core.Settings.UserAgent = "Jarvis-Orb/1.0";

            _core.WebMessageReceived += OnWebMessageReceived;

            var cacheBuster = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            _core.Navigate($"https://jarvis-orb.app/orb.html?v={cacheBuster}");

            _initialized = true;

            // If a summon was requested before we finished initializing, do it now
            if (_summonPending)
            {
                _summonPending = false;
                Summon();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NativeOrb] WebView2 init failed: {ex.Message}");
        }
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
                    // Web UI is ready
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

    public void Summon()
    {
        if (!_initialized)
        {
            _summonPending = true;
            return;
        }

        // Reposition and show on the message thread
        _messageThread.Start(); // no-op if already running

        var screen = System.Windows.SystemParameters.WorkArea;
        int x = (int)((screen.Width - OrbWidth) / 2);
        int y = (int)(screen.Height - OrbHeight - 60);

        SetWindowPos(_hwnd, (IntPtr)HWND_TOPMOST, x, y, OrbWidth, OrbHeight,
            SWP_NOACTIVATE | SWP_SHOWWINDOW);

        // Remove WS_EX_TRANSPARENT so the window receives mouse clicks
        int exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
        exStyle &= ~WS_EX_TRANSPARENT;
        SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle);

        // Tell the web UI to play the summon animation
        PostMessage(System.Text.Json.JsonSerializer.Serialize(new { @event = "summon" }));
    }

    public void Dismiss()
    {
        if (!_initialized || _hwnd == IntPtr.Zero) return;

        // Tell the web UI to play the dismiss animation
        PostMessage(System.Text.Json.JsonSerializer.Serialize(new { @event = "dismiss" }));

        // Hide after the animation completes
        var timer = new System.Threading.Timer(_ =>
        {
            ShowWindow(_hwnd, 0); // SW_HIDE
            // Make click-through again
            int exStyle = GetWindowLong(_hwnd, GWL_EXSTYLE);
            exStyle |= WS_EX_TRANSPARENT;
            SetWindowLong(_hwnd, GWL_EXSTYLE, exStyle);
            Dismissed?.Invoke();
        }, null, 350, Timeout.Infinite);
    }

    public void SetState(string state)
    {
        PostMessage(System.Text.Json.JsonSerializer.Serialize(new { @event = "state", state }));
    }

    // ── IBridgeHost ───────────────────────────────────────────

    public void PostMessage(string json)
    {
        _core?.PostWebMessageAsJson(json);
    }

    public void NavigateReload() => _core?.Reload();
    public void ToggleDevTools() => _core?.OpenDevToolsWindow();
    public void SetZoom(double z) { if (_controller != null) _controller.ZoomFactor = z; }
    public void BringToFront()
    {
        SetWindowPos(_hwnd, (IntPtr)HWND_TOPMOST, 0, 0, 0, 0,
            SWP_NOACTIVATE | SWP_SHOWWINDOW | SWP_NOMOVE | SWP_NOSIZE);
    }
    public void CloseApp() => DestroyWindow(_hwnd);

    // ── Dispose ───────────────────────────────────────────────
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _controller?.Close();
        _controller = null;
        _core = null;

        if (_hwnd != IntPtr.Zero)
        {
            DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }

        UnregisterClass(ClassName, _hinstance);
    }
}
