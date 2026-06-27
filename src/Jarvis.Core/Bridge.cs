using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jarvis.Core.Shell;
using Jarvis.Core.Services;

namespace Jarvis.Core;

/// <summary>
/// Marshals JSON messages between the web UI and the C# backend services.
/// Protocol (web -> C#):  { "id": "<rpcId>", "action": "...", ...payload }
/// Protocol (C# -> web):  { "event": "...", ... }  for push events
///                         { "id": "<rpcId>", "ok": bool, "data"|"error": ... } for RPC replies
/// </summary>
public sealed class Bridge
{
    private readonly IBridgeHost _host;
    private readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public ShellService Shell { get; }
    public SystemControlService SystemControl { get; }
    public ProcessService Processes { get; }
    public WindowService Windows { get; }

    public Bridge(IBridgeHost host, ShellService shell, SystemControlService sys, ProcessService proc, WindowService win)
    {
        _host = host;
        Shell = shell;
        SystemControl = sys;
        Processes = proc;
        Windows = win;
    }

    public void PostToWeb(object payload)
        => _host.PostMessage(JsonSerializer.Serialize(payload, _json));

    public async Task HandleMessageAsync(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var id = root.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            var action = root.TryGetProperty("action", out var actEl) ? actEl.GetString() : null;

            object? result = action switch
            {
                "shell.getState" => Shell.GetState(),
                "shell.launchApp" => Shell.LaunchApp(root.GetProperty("name").GetString()!),
                "shell.openSettings" => Shell.OpenSettings(),
                "shell.lockScreen" => Shell.LockScreen(),
                "shell.shutdown" => Shell.Shutdown(),
                "shell.restart" => Shell.Restart(),
                "shell.sleep" => Shell.Sleep(),
                "shell.logoff" => Shell.Logoff(),

                "sys.powershell" => await SystemControl.RunPowerShell(root.GetProperty("command").GetString()!),
                "sys.cmd" => await SystemControl.RunCmd(root.GetProperty("command").GetString()!),
                "sys.getInfo" => await SystemControl.GetSystemInfo(),
                "sys.getVolume" => await SystemControl.GetVolume(),
                "sys.setVolume" => await SetVolumeAsync(root.GetProperty("value").GetInt32()),
                "sys.brightness" => await SystemControl.GetBrightness(),

                "proc.list" => Processes.ListProcesses(),
                "proc.kill" => Processes.KillProcess(root.GetProperty("pid").GetInt32()),
                "proc.launch" => Processes.Launch(root.GetProperty("path").GetString()!,
                                    root.TryGetProperty("args", out var a) ? a.GetString() : null),

                "win.list" => Windows.ListWindows(),
                "win.focus" => Windows.FocusWindow(root.GetProperty("hwnd").GetInt64()),
                "win.close" => Windows.CloseWindow(root.GetProperty("hwnd").GetInt64()),
                "win.minimize" => Windows.MinimizeWindow(root.GetProperty("hwnd").GetInt64()),
                "win.maximize" => Windows.MaximizeWindow(root.GetProperty("hwnd").GetInt64()),
                "win.restore" => Windows.RestoreWindow(root.GetProperty("hwnd").GetInt64()),
                "win.snap" => Windows.SnapWindow(root.GetProperty("hwnd").GetInt64(),
                                    root.GetProperty("direction").GetString()!),

                "config.get" => AppContext.Current.Config.Current,
                "config.set" => SetConfig(root.GetProperty("key").GetString()!, root.GetProperty("value")),

                _ => new { error = $"Unknown action: {action}" },
            };

            if (id != null)
            {
                PostToWeb(new { id, ok = true, data = result });
            }
        }
        catch (Exception ex)
        {
            var id = json.Contains("\"id\"") ? JsonDocument.Parse(json).RootElement.GetProperty("id").GetString() : null;
            PostToWeb(new { id, ok = false, error = ex.Message });
        }
    }

    private object SetConfig(string key, JsonElement value)
    {
        AppContext.Current.Config.Set(key, value);
        return new { ok = true };
    }

    private async Task<object> SetVolumeAsync(int value)
    {
        await SystemControl.SetVolume(value);
        return new { ok = true };
    }
}
