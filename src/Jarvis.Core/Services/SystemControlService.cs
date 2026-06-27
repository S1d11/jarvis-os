using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Jarvis.Core.Shell;

namespace Jarvis.Core.Services;

/// <summary>
/// Raw OS access — run PowerShell / CMD commands, get system info, control volume.
/// </summary>
public sealed class SystemControlService
{
    private readonly ISystemAccess _sys;

    public SystemControlService(ISystemAccess sys) => _sys = sys;

    public async Task<CommandResult> RunPowerShell(string command)
    {
        return await RunProcess("powershell.exe", $"-NoProfile -NonInteractive -Command \"{command}\"", timeoutSec: 30);
    }

    public async Task<CommandResult> RunCmd(string command)
    {
        return await RunProcess("cmd.exe", $"/c {command}", timeoutSec: 30);
    }

    public Task<SystemInfo> GetSystemInfo()
    {
        return Task.FromResult(new SystemInfo
        {
            MachineName = Environment.MachineName,
            UserName = Environment.UserName,
            OsVersion = Environment.OSVersion.ToString(),
            ProcessorCount = Environment.ProcessorCount,
            Is64Bit = Environment.Is64BitOperatingSystem,
            RuntimeVersion = Environment.Version.ToString(),
        });
    }

    public Task<int> GetVolume()
    {
        // Volume control is platform-specific; on Windows we use CoreAudio via P/Invoke
        // For now, return a placeholder — the Windows project overrides this
        return Task.FromResult(50);
    }

    public Task SetVolume(int value)
    {
        // Clamp 0-100
        value = Math.Clamp(value, 0, 100);
        // Platform-specific implementation in Windows project
        return Task.CompletedTask;
    }

    public Task<int> GetBrightness()
    {
        // Platform-specific; Windows uses WMI
        return Task.FromResult(100);
    }

    private static async Task<CommandResult> RunProcess(string fileName, string arguments, int timeoutSec)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            using var p = Process.Start(psi);
            if (p == null) return new CommandResult { ExitCode = -1, Stderr = "Failed to start process" };

            var stdoutTask = p.StandardOutput.ReadToEndAsync();
            var stderrTask = p.StandardError.ReadToEndAsync();

            if (!p.WaitForExit(timeoutSec * 1000))
            {
                p.Kill(entireProcessTree: true);
                return new CommandResult { ExitCode = -1, Stderr = $"Timeout after {timeoutSec}s" };
            }

            return new CommandResult
            {
                ExitCode = p.ExitCode,
                Stdout = await stdoutTask,
                Stderr = await stderrTask,
            };
        }
        catch (Exception ex)
        {
            return new CommandResult { ExitCode = -1, Stderr = ex.Message };
        }
    }
}

public sealed class CommandResult
{
    public int ExitCode { get; set; }
    public string Stdout { get; set; } = "";
    public string Stderr { get; set; } = "";
}

public sealed class SystemInfo
{
    public string MachineName { get; set; } = "";
    public string UserName { get; set; } = "";
    public string OsVersion { get; set; } = "";
    public int ProcessorCount { get; set; }
    public bool Is64Bit { get; set; }
    public string RuntimeVersion { get; set; } = "";
}
