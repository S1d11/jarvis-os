using System;
using System.Windows;
using WpfApplication = System.Windows.Application;

namespace Jarvis.Windows;

/// <summary>
/// Jarvis application entry point.
///
/// By default, Jarvis runs as an invisible background process:
///   - No WPF window (the orb is a raw Win32 HWND on a separate thread)
///   - No system tray icon
///   - No taskbar entry
///   - Not visible in Alt+Tab
///
/// It sits in the background, listening for:
///   1. Win+J (low-level keyboard hook — works even in fullscreen games)
///   2. "Hey Jarvis" (NAudio wake word detection)
///
/// When summoned, the NativeOrbWindow appears — a raw Win32 overlay created
/// via CreateWindowEx, hosting WebView2 directly through its COM controller.
/// No WPF Window, no XAML, no Dispatcher. Just a Win32 HWND.
///
/// Command-line flags:
///   --shell     Replace Explorer as the desktop shell (fullscreen WPF window)
///   --no-wake   Disable wake word detection (hotkey only)
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
    private MainWindow? _mainWindow;
    private bool _shellMode;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        // Create data directory
        System.IO.Directory.CreateDirectory(DataDir);

        // Parse args
        bool noWake = false;
        foreach (var arg in e.Args)
        {
            if (arg == "--shell") _shellMode = true;
            else if (arg == "--no-wake") noWake = true;
        }

        // ── Create the native orb window (raw Win32, hidden) ────────
        // This creates a Win32 HWND on a dedicated thread. No WPF Window.
        _orbWindow = new NativeOrbWindow();
        _orbWindow.Dismissed += OnOrbDismissed;
        _orbWindow.OpenMainWindowRequested += OnOrbOpenMainRequested;

        // ── Shell mode: show the full desktop (WPF MainWindow) ──────
        if (_shellMode)
        {
            _mainWindow = new MainWindow();
            _mainWindow.Show();
        }

        // ── Install low-level keyboard hook (Win+J) ─────────────────
        _keyboardHook = new LowLevelKeyboardHook();
        _keyboardHook.SummonPressed += SummonOrb;
        _keyboardHook.EscapePressed += DismissOrb;
        _keyboardHook.Install();

        // ── Start wake word detection ("Hey Jarvis") ────────────────
        if (!noWake)
        {
            _wakeWord = new WakeWordService();
            _wakeWord.WakeWordDetected += SummonOrb;
            _wakeWord.Start();
        }

        // Jarvis is now running — invisible, part of Windows.
        // The WPF Dispatcher keeps the process alive for the keyboard hook
        // and wake word service. The orb runs on its own Win32 thread.
    }

    private void SummonOrb()
    {
        _orbWindow?.Summon();
    }

    private void DismissOrb()
    {
        _orbWindow?.Dismiss();
    }

    private void OnOrbDismissed() { /* orb faded out */ }

    private void OnOrbOpenMainRequested()
    {
        if (_mainWindow == null)
            _mainWindow = new MainWindow();
        _mainWindow.BringToFront();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _keyboardHook?.Dispose();
        _wakeWord?.Dispose();
        _orbWindow?.Dispose();
        base.OnExit(e);
    }
}
