using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;

namespace Jarvis.Core.Shell;

/// <summary>
/// Manages the Jarvis shell state — pinned apps, running apps, dock items.
/// Platform-agnostic; the Windows project provides the ISystemAccess implementation.
/// </summary>
public sealed class ShellService
{
    private readonly ISystemAccess _sys;
    private readonly List<PinnedApp> _pinned = new();
    private readonly string _pinFile;

    public IReadOnlyList<PinnedApp> Pinned => _pinned;

    public ShellService(ISystemAccess sys)
    {
        _sys = sys;
        _pinFile = Path.Combine(AppContext.DataDir, "pinned_apps.json");
        LoadPinned();
    }

    public ShellState GetState() => new()
    {
        PinnedApps = _pinned,
        RunningApps = _sys.GetRunningApps(),
        IsShellMode = AppContext.Current.Config.Current.ShellMode == "shell",
    };

    public bool LaunchApp(string name)
    {
        // Check pinned apps first
        foreach (var p in _pinned)
        {
            if (p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                _sys.LaunchProcess(p.Path, p.Args);
                return true;
            }
        }

        // Try common Windows apps
        var (path, args) = name.ToLowerInvariant() switch
        {
            "notepad" => ("notepad.exe", ""),
            "calc" or "calculator" => ("calc.exe", ""),
            "paint" => ("mspaint.exe", ""),
            "cmd" or "terminal" or "command prompt" => ("cmd.exe", ""),
            "powershell" => ("powershell.exe", ""),
            "explorer" or "file explorer" or "files" => ("explorer.exe", ""),
            "settings" => ("ms-settings:", ""),
            "task manager" => ("taskmgr.exe", ""),
            "control panel" => ("control.exe", ""),
            "browser" or "edge" => ("msedge.exe", ""),
            "chrome" => ("chrome.exe", ""),
            "firefox" => ("firefox.exe", ""),
            "steam" => ("steam.exe", ""),
            "spotify" => ("spotify.exe", ""),
            "discord" => ("discord.exe", ""),
            "vscode" or "code" => ("code.exe", ""),
            _ => (name, ""),
        };

        return _sys.LaunchProcess(path, args);
    }

    public bool OpenSettings() => _sys.LaunchProcess("ms-settings:", "");

    public bool LockScreen() => _sys.LockScreen();
    public bool Shutdown() => _sys.Shutdown();
    public bool Restart() => _sys.Restart();
    public bool Sleep() => _sys.Sleep();
    public bool Logoff() => _sys.Logoff();

    public void PinApp(PinnedApp app)
    {
        if (!_pinned.Exists(p => p.Path == app.Path))
        {
            _pinned.Add(app);
            SavePinned();
        }
    }

    public void UnpinApp(string path)
    {
        _pinned.RemoveAll(p => p.Path == path);
        SavePinned();
    }

    private void LoadPinned()
    {
        try
        {
            if (File.Exists(_pinFile))
            {
                var json = File.ReadAllText(_pinFile);
                _pinned.AddRange(System.Text.Json.JsonSerializer.Deserialize<List<PinnedApp>>(json) ?? new());
            }
        }
        catch { /* non-fatal */ }
    }

    private void SavePinned()
    {
        try { File.WriteAllText(_pinFile, System.Text.Json.JsonSerializer.Serialize(_pinned)); }
        catch { /* non-fatal */ }
    }
}

public sealed class ShellState
{
    public IReadOnlyList<PinnedApp> PinnedApps { get; set; } = Array.Empty<PinnedApp>();
    public IReadOnlyList<RunningApp> RunningApps { get; set; } = Array.Empty<RunningApp>();
    public bool IsShellMode { get; set; }
}

public sealed class PinnedApp
{
    public string Name { get; set; } = "";
    public string Path { get; set; } = "";
    public string Args { get; set; } = "";
    public string Icon { get; set; } = "";
}

public sealed class RunningApp
{
    public int Pid { get; set; }
    public string Name { get; set; } = "";
    public string Title { get; set; } = "";
    public long Hwnd { get; set; }
}
