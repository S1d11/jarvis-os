using System;
using System.Drawing;
using System.Windows;
using System.Windows.Forms;
using WpfApplication = System.Windows.Application;

namespace Jarvis.Windows;

/// <summary>
/// Jarvis application entry point.
///
/// Jarvis coexists with Windows — it does NOT replace Explorer.
/// It runs as a background process with two UI surfaces:
///
///   1. NativeOrbWindow — a small floating glass sphere (raw Win32 HWND)
///      that stays visible on top of all windows. Drag it anywhere.
///      Click it to summon the overlay.
///
///   2. MainWindow (repurposed as Overlay) — a transparent, borderless,
///      always-on-top window that hosts the Siri-style assistant UI.
///      It appears centered over whatever you're doing, like Spotlight.
///
/// Summon methods:
///   - Win+J (low-level keyboard hook — works in fullscreen games)
///   - "Hey Jarvis" (NAudio wake word detection)
///   - Click the floating orb
///
/// A system tray icon is provided in the hidden icons menu so the user
/// can access Jarvis if they accidentally close it. A Start Menu shortcut
/// is also created by install.ps1.
///
/// On startup, NOTHING is visible — not even the orb. The orb only
/// appears after the user presses Win+J or says "Hey Jarvis".
/// </summary>
public partial class App : WpfApplication
{
    public static string AppName => "Jarvis";
    public static string AppVersion => "1.0.0";

    public static string DataDir =>
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Jarvis");

    private NativeOrbWindow? _orbWindow;
    private LowLevelKeyboardHook? _keyboardHook;
    private WakeWordService? _wakeWord;
    private MainWindow? _overlay;
    private NotifyIcon? _trayIcon;
    private bool _overlayVisible;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        // Prevent WPF from shutting down when windows are hidden.
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        // Create data directory
        System.IO.Directory.CreateDirectory(DataDir);

        // Parse args
        bool noWake = false;
        foreach (var arg in e.Args)
        {
            if (arg == "--no-wake") noWake = true;
        }

        // ── Create the overlay window (hidden on startup) ──────────
        _overlay = new MainWindow();
        _overlay.OverlayDismissed += OnOverlayDismissed;

        // ── Create the floating glass orb (raw Win32, HIDDEN) ───────
        // The orb starts hidden. It only appears when summoned via
        // Win+J, "Hey Jarvis", or the tray icon.
        _orbWindow = new NativeOrbWindow();
        _orbWindow.Dismissed += OnOrbDismissed;
        _orbWindow.OpenOverlayRequested += ShowOverlay;

        // ── Create system tray icon (hidden icons menu) ────────────
        CreateTrayIcon();

        // ── Install low-level keyboard hook (Win+J) ─────────────────
        _keyboardHook = new LowLevelKeyboardHook();
        _keyboardHook.SummonPressed += ToggleOverlay;
        _keyboardHook.EscapePressed += () =>
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(HideOverlay));
        _keyboardHook.Install();

        // ── Start wake word detection ("Hey Jarvis") ────────────────
        if (!noWake)
        {
            _wakeWord = new WakeWordService();
            _wakeWord.WakeWordDetected += ShowOverlay;
            _wakeWord.Start();
        }

        // Jarvis is now running in the background. NOTHING is visible:
        //   - Orb is hidden (appears on Win+J or voice)
        //   - Overlay is hidden (appears on Win+J, voice, or orb click)
        //   - Only the tray icon is in the hidden icons menu
    }

    /// <summary>Create the system tray icon for the hidden icons menu.</summary>
    private void CreateTrayIcon()
    {
        _trayIcon = new NotifyIcon
        {
            Icon = CreateJarvisIcon(),
            Text = "Jarvis — Press Win+J or say \"Hey Jarvis\"",
            Visible = true,
        };

        // Right-click context menu
        var menu = new ContextMenuStrip();
        menu.Items.Add("Summon Jarvis (Win+J)", null, (s, e) => ShowOverlay());
        menu.Items.Add("-");
        menu.Items.Add("Settings", null, (s, e) => ShowOverlay());
        menu.Items.Add("-");
        menu.Items.Add("Exit Jarvis", null, (s, e) => ExitJarvis());
        _trayIcon.ContextMenuStrip = menu;

        // Double-click summons
        _trayIcon.DoubleClick += (s, e) => ShowOverlay();
    }

    /// <summary>Create a simple Jarvis icon (purple circle with J).</summary>
    private static Icon CreateJarvisIcon()
    {
        // Try to load an embedded icon, otherwise generate one
        try
        {
            var bitmap = new Bitmap(32, 32);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                // Dark circle background
                using var bgBrush = new SolidBrush(Color.FromArgb(35, 35, 40));
                g.FillEllipse(bgBrush, 2, 2, 28, 28);
                // Purple ring
                using var ringPen = new Pen(Color.FromArgb(140, 120, 200), 2);
                g.DrawEllipse(ringPen, 2, 2, 28, 28);
                // "J" letter
                using var font = new Font("Segoe UI", 14, System.Drawing.FontStyle.Bold);
                using var textBrush = new SolidBrush(Color.FromArgb(220, 220, 230));
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                };
                g.DrawString("J", font, textBrush, 16, 16, sf);
            }
            var handle = bitmap.GetHicon();
            return Icon.FromHandle(handle);
        }
        catch
        {
            // Fallback to system icon
            return SystemIcons.Application;
        }
    }

    /// <summary>Win+J pressed — toggle the overlay.
    /// Called from the low-level keyboard hook callback, so we must return
    /// quickly (Windows removes hooks that block &gt;300ms). Marshal to the
    /// dispatcher asynchronously.</summary>
    private void ToggleOverlay()
    {
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
        {
            if (_overlayVisible)
                HideOverlay();
            else
                ShowOverlay();
        }));
    }

    /// <summary>Show the Siri-style overlay (also called by orb click).</summary>
    private void ShowOverlay()
    {
        _overlay?.ShowOverlay();
        _overlayVisible = true;
    }

    /// <summary>Hide the overlay back to background.</summary>
    private void HideOverlay()
    {
        _overlay?.HideOverlay();
        _overlayVisible = false;
    }

    private void OnOverlayDismissed()
    {
        _overlayVisible = false;
    }

    private void OnOrbDismissed() { /* orb faded out */ }

    /// <summary>Exit Jarvis completely (from tray icon).</summary>
    private void ExitJarvis()
    {
        _trayIcon?.Dispose();
        _keyboardHook?.Dispose();
        _wakeWord?.Dispose();
        _orbWindow?.Dispose();
        _overlay?.ForceClose();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _keyboardHook?.Dispose();
        _wakeWord?.Dispose();
        _orbWindow?.Dispose();
        _overlay?.ForceClose();
        base.OnExit(e);
    }
}
