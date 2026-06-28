using System;
using System.Windows;
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
/// The app has no taskbar icon, no tray icon, no system menu.
/// It feels like part of the OS.
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
    private bool _overlayVisible;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        // Prevent WPF from shutting down when windows are hidden.
        // The process stays alive for the keyboard hook and wake word.
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
        // This is the Siri-style assistant that appears over your desktop.
        // Transparent, borderless, always on top. Not a shell replacement.
        _overlay = new MainWindow();
        _overlay.OverlayDismissed += OnOverlayDismissed;

        // ── Create the floating glass orb (raw Win32, visible) ───────
        // A small persistent widget that floats above all windows.
        // Click it to summon the overlay.
        _orbWindow = new NativeOrbWindow();
        _orbWindow.Dismissed += OnOrbDismissed;
        _orbWindow.OpenOverlayRequested += ShowOverlay;

        // ── Install low-level keyboard hook (Win+J) ─────────────────
        _keyboardHook = new LowLevelKeyboardHook();
        _keyboardHook.SummonPressed += ToggleOverlay;
        _keyboardHook.EscapePressed += HideOverlay;
        _keyboardHook.Install();

        // ── Start wake word detection ("Hey Jarvis") ────────────────
        if (!noWake)
        {
            _wakeWord = new WakeWordService();
            _wakeWord.WakeWordDetected += ShowOverlay;
            _wakeWord.Start();
        }

        // Jarvis is now running in the background, invisible to the user
        // until they press Win+J or click the orb.
    }

    /// <summary>Win+J pressed — toggle the overlay.</summary>
    private void ToggleOverlay()
    {
        if (_overlayVisible)
            HideOverlay();
        else
            ShowOverlay();
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

    private void OnOrbDismissed() { /* orb faded out (not used in coexist mode) */ }

    protected override void OnExit(ExitEventArgs e)
    {
        _keyboardHook?.Dispose();
        _wakeWord?.Dispose();
        _orbWindow?.Dispose();
        _overlay?.ForceClose();
        base.OnExit(e);
    }
}
