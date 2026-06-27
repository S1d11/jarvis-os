using System;
using System.IO;
using Jarvis.Core.Shell;
using Jarvis.Core.Services;

namespace Jarvis.Core;

/// <summary>
/// Platform-agnostic application context. Holds shared services and state.
/// </summary>
public sealed class AppContext
{
    public static AppContext Current { get; private set; } = new();

    public static string AppName => "Jarvis";
    public static string AppVersion => "1.0.0";

    /// <summary>
    /// Per-user data directory (% LOCALAPPDATA %\Jarvis).
    /// </summary>
    public static string DataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Jarvis");

    public ConfigService Config { get; }
    public ShellService? Shell { get; private set; }

    private AppContext()
    {
        Directory.CreateDirectory(DataDir);
        Config = new ConfigService();
    }

    public void SetShellService(ShellService shell) => Shell = shell;
}
