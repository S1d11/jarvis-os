using System.Collections.Generic;
using System.Diagnostics;

namespace Jarvis.Core.Services;

/// <summary>
/// Process management — list, launch, and kill processes.
/// </summary>
public sealed class ProcessService
{
    public IReadOnlyList<ProcInfo> ListProcesses()
    {
        var result = new List<ProcInfo>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                result.Add(new ProcInfo
                {
                    Pid = p.Id,
                    Name = p.ProcessName,
                    MemoryMB = (int)(p.WorkingSet64 / 1024 / 1024),
                });
            }
            catch { /* skip inaccessible processes */ }
        }
        return result;
    }

    public bool KillProcess(int pid)
    {
        try
        {
            var p = Process.GetProcessById(pid);
            p.Kill(entireProcessTree: true);
            return true;
        }
        catch { return false; }
    }

    public bool Launch(string path, string? args = null)
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
}

public sealed class ProcInfo
{
    public int Pid { get; set; }
    public string Name { get; set; } = "";
    public int MemoryMB { get; set; }
}
