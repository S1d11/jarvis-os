using System;
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

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        // Create data directory
        System.IO.Directory.CreateDirectory(DataDir);

        // Parse command-line args
        foreach (var arg in e.Args)
        {
            if (arg == "--shell")
            {
                Jarvis.Windows.Properties.Settings.Default.ShellMode = true;
            }
            else if (arg == "--tray")
            {
                Jarvis.Windows.Properties.Settings.Default.StartMinimized = true;
            }
            else if (arg == "--settings")
            {
                Jarvis.Windows.Properties.Settings.Default.OpenSettings = true;
            }
        }
    }
}
