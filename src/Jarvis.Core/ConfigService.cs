using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Jarvis.Core;

/// <summary>
/// Persisted application configuration stored in %LOCALAPPDATA%\Jarvis\config.json.
/// </summary>
public sealed class ConfigService
{
    private static readonly string ConfigPath =
        Path.Combine(AppContext.DataDir, "config.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public ConfigData Current { get; private set; } = new();

    public ConfigService()
    {
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                Current = JsonSerializer.Deserialize<ConfigData>(json, JsonOpts) ?? new ConfigData();
            }
        }
        catch { /* corrupt config — use defaults */ }
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(Current, JsonOpts));
        }
        catch { /* non-fatal */ }
    }

    public void Set(string key, JsonElement value)
    {
        var opts = Current;
        switch (key)
        {
            case "theme": opts.Theme = value.GetString() ?? "dark"; break;
            case "accent": opts.Accent = value.GetString() ?? "cyan"; break;
            case "startMinimized": opts.StartMinimized = value.GetBoolean(); break;
            case "closeBehavior": opts.CloseBehavior = value.GetString() ?? "tray"; break;
            case "shellMode": opts.ShellMode = value.GetString() ?? "window"; break;
            case "autoStart": opts.AutoStart = value.GetBoolean(); break;
            case "defaultModel": opts.DefaultModel = value.GetString() ?? "llama3.2:3b"; break;
            case "ollamaUrl": opts.OllamaUrl = value.GetString() ?? "http://localhost:11434"; break;
            case "systemPrompt": opts.SystemPrompt = value.GetString() ?? ""; break;
            case "voiceEnabled": opts.VoiceEnabled = value.GetBoolean(); break;
            case "wakeWord": opts.WakeWord = value.GetString() ?? "Hey Jarvis"; break;
        }
        Save();
    }
}

public sealed class ConfigData
{
    public string Theme { get; set; } = "dark";
    public string Accent { get; set; } = "cyan";
    public bool StartMinimized { get; set; } = false;
    public string CloseBehavior { get; set; } = "tray"; // "tray" | "quit"
    public string ShellMode { get; set; } = "window";   // "window" | "shell"
    public bool AutoStart { get; set; } = true;
    public string DefaultModel { get; set; } = "llama3.2:3b";
    public string OllamaUrl { get; set; } = "http://localhost:11434";
    public string SystemPrompt { get; set; } = "";
    public bool VoiceEnabled { get; set; } = false;
    public string WakeWord { get; set; } = "Hey Jarvis";
}
