using System;
using System.Windows;
using WpfApplication = System.Windows.Application;

namespace Jarvis.Windows;

public partial class App : WpfApplication
{
    public static string AppName => "Jarvis";
    public static string AppVersion => "1.0.0";

    public static string DataDir =>
        System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Jarvis");

    private MainWindow? _mainWindow;
    private OrbWindow? _orbWindow;
    private GlobalHotkeyService? _hotkey;
    private WakeWordService? _wakeWord;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        // Create data directory
        System.IO.Directory.CreateDirectory(DataDir);

        // Parse command-line args
        bool shellMode = false;
        bool trayMode = false;
        bool orbOnly = false;

        foreach (var arg in e.Args)
        {
            if (arg == "--shell") shellMode = true;
            else if (arg == "--tray") trayMode = true;
            else if (arg == "--orb") orbOnly = true;
            else if (arg == "--no-wake-word") Jarvis.Windows.Properties.Settings.Default.WakeWordEnabled = false;
        }

        // Create the orb window (hidden initially)
        _orbWindow = new OrbWindow();
        _orbWindow.Dismissed += OnOrbDismissed;
        _orbWindow.OpenMainWindowRequested += OnOrbOpenMainRequested;
        _orbWindow.Hide();

        // Create the main window (hidden in orb-only or tray mode)
        _mainWindow = new MainWindow();

        if (shellMode)
        {
            Jarvis.Windows.Properties.Settings.Default.ShellMode = true;
            _mainWindow.Show();
        }
        else if (trayMode || orbOnly)
        {
            // Don't show the main window — Jarvis runs in the background
            // The orb window will appear when summoned
            Jarvis.Windows.Properties.Settings.Default.StartMinimized = true;
        }
        else
        {
            _mainWindow.Show();
        }

        // Wire up the orb window to the main window's bridge
        // (They share the same backend services)

        // Register global hotkey (Win+J) to summon the orb
        _hotkey = new GlobalHotkeyService();
        _hotkey.HotkeyPressed += SummonOrb;

        // Register after the main window handle is available
        _mainWindow.SourceInitialized += (_, _) =>
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(_mainWindow).Handle;
            _hotkey.Register(hwnd);
        };

        // Start wake word detection ("Hey Jarvis")
        _wakeWord = new WakeWordService();
        _wakeWord.WakeWordDetected += SummonOrb;
        _wakeWord.Start();
    }

    /// <summary>
    /// Summon the orb — called by hotkey (Win+J) or wake word ("Hey Jarvis").
    /// This is the Siri-like "appear on screen" moment.
    /// </summary>
    private void SummonOrb()
    {
        if (_orbWindow == null) return;

        _orbWindow.Dispatcher.Invoke(() =>
        {
            _orbWindow.Summon();
        });
    }

    private void OnOrbDismissed()
    {
        // Orb was dismissed — nothing to do, it's already hidden
    }

    private void OnOrbOpenMainRequested()
    {
        // User clicked "open full view" in the orb — show the main window
        _mainWindow?.BringToFront();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkey?.Dispose();
        _wakeWord?.Dispose();
        _orbWindow?.Close();
        base.OnExit(e);
    }
}
