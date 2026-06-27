using System;
using System.Windows;
using WpfApplication = System.Windows.Application;

namespace Jarvis.Windows;

/// <summary>
/// Jarvis application entry point.
///
/// By default, Jarvis runs as an invisible background process:
///   - No window
///   - No system tray icon
///   - No taskbar entry
///   - Not visible in Alt+Tab
///
/// It sits in the background, listening for:
///   1. Win+J (low-level keyboard hook — works even in fullscreen games)
///   2. "Hey Jarvis" (NAudio wake word detection)
///
/// When summoned, the OrbWindow appears — a true Win32 overlay that
/// doesn't steal focus and is invisible to window enumeration.
///
/// Command-line flags:
///   --shell     Replace Explorer as the desktop shell (fullscreen)
///   --orb       Background-only mode (default behavior, just makes it explicit)
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

    private OrbWindow? _orbWindow;
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

        // ── Create the orb window (hidden, waiting to be summoned) ──
        _orbWindow = new OrbWindow();
        _orbWindow.Dismissed += OnOrbDismissed;
        _orbWindow.OpenMainWindowRequested += OnOrbOpenMainRequested;
        _orbWindow.Hide();

        // ── Shell mode: show the full desktop ───────────────────────
        if (_shellMode)
        {
            _mainWindow = new MainWindow();
            _mainWindow.Show();
        }

        // ── Install low-level keyboard hook (Win+J) ─────────────────
        // This works even when fullscreen games have exclusive input.
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

        // That's it. No window shown, no tray icon, no UI.
        // Jarvis is now part of Windows — invisible until summoned.
        // The Dispatcher keeps the process alive listening for events.
    }

    /// <summary>
    /// Summon the orb — called by Win+J or "Hey Jarvis".
    /// This is the Siri moment: the orb appears over whatever you're doing.
    /// </summary>
    private void SummonOrb()
    {
        _orbWindow?.Summon();
    }

    /// <summary>
    /// Dismiss the orb — called by Escape key.
    /// </summary>
    private void DismissOrb()
    {
        _orbWindow?.Dismiss();
    }

    private void OnOrbDismissed()
    {
        // Orb faded out — nothing to do, it's hidden
    }

    private void OnOrbOpenMainRequested()
    {
        // User clicked "open full view" — show the main window
        if (_mainWindow == null)
        {
            _mainWindow = new MainWindow();
        }
        _mainWindow.BringToFront();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _keyboardHook?.Dispose();
        _wakeWord?.Dispose();
        _orbWindow?.Close();
        base.OnExit(e);
    }
}
