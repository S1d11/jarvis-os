using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Jarvis.Core.Shell;

namespace Jarvis.Windows;

/// <summary>
/// Windows-specific implementation of ISystemAccess.
/// Uses Win32 API for process launching, screen locking, shutdown, etc.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class WindowsSystemAccess : ISystemAccess
{
    public bool LaunchProcess(string path, string args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            };
            if (!string.IsNullOrEmpty(args)) psi.Arguments = args;
            Process.Start(psi);
            return true;
        }
        catch { return false; }
    }

    public IReadOnlyList<RunningApp> GetRunningApps()
    {
        var result = new List<RunningApp>();
        var seen = new HashSet<int>();

        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.MainWindowHandle != IntPtr.Zero && seen.Add(p.Id))
                {
                    result.Add(new RunningApp
                    {
                        Pid = p.Id,
                        Name = p.ProcessName,
                        Title = p.MainWindowTitle,
                        Hwnd = p.MainWindowHandle.ToInt64(),
                    });
                }
            }
            catch { /* skip inaccessible */ }
        }
        return result;
    }

    public bool LockScreen()
    {
        try { LockWorkStation(); return true; }
        catch { return false; }
    }

    public bool Shutdown()
    {
        try
        {
            var psi = new ProcessStartInfo("shutdown.exe", "/s /t 0")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            Process.Start(psi);
            return true;
        }
        catch { return false; }
    }

    public bool Restart()
    {
        try
        {
            var psi = new ProcessStartInfo("shutdown.exe", "/r /t 0")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            Process.Start(psi);
            return true;
        }
        catch { return false; }
    }

    public bool Sleep()
    {
        try
        {
            SetSuspendState(false, false, false);
            return true;
        }
        catch { return false; }
    }

    public bool Logoff()
    {
        try
        {
            ExitWindowsEx(0, 0);
            return true;
        }
        catch { return false; }
    }

    // ── Win32 P/Invoke ──────────────────────────────────────
    [DllImport("user32.dll")]
    private static extern bool LockWorkStation();

    [DllImport("powrprof.dll")]
    private static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    [DllImport("user32.dll")]
    private static extern bool ExitWindowsEx(uint uFlags, uint dwReason);
}
