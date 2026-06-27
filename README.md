# Jarvis for Windows

A native Windows desktop application that serves as an AI-powered desktop shell.
Built with WPF (.NET 10) and WebView2 — no Electron, no Python runtime required.

Jarvis can run as a normal desktop app **or replace Windows Explorer as the
desktop shell** (like a custom Windows DE). When in shell mode, Jarvis boots
directly into the desktop with a dock, widgets, quick settings, power menu,
and an AI assistant — no taskbar, no Start menu, just Jarvis.

## Features

- **Dock** — app launcher with pinned and running apps (like macOS dock)
- **AI Assistant** — chat panel with orb animation, markdown rendering
- **Quick Settings** — volume, brightness, WiFi/Bluetooth toggles, power options
- **Start Menu** — searchable app launcher
- **Power Menu** — lock, sleep, restart, shut down, sign out
- **Clock Widget** — live clock and date
- **System Tray** — minimize to tray, context menu with settings and power
- **Shell Replacement** — replace `explorer.exe` with Jarvis as the Windows shell
- **System Control** — run PowerShell/CMD commands from the assistant
- **Window Management** — list, focus, close, minimize, maximize, snap windows
- **Process Management** — list and kill running processes
- **Dark Theme** — native dark title bar, dark UI throughout

## Requirements

- Windows 10/11 (x64)
- WebView2 Runtime (preinstalled on Windows 11; bundled with Edge on Windows 10)
- .NET 10 SDK (only to build — the published `.exe` is self-contained)

## Build & run from source

```powershell
dotnet run --project src\Jarvis.Windows\Jarvis.Windows.csproj -c Debug
```

## Publish a self-contained single-file `.exe`

```powershell
powershell -ExecutionPolicy Bypass -File publish.ps1
```

Produces `publish\Jarvis.exe` (~170 MB, no .NET runtime needed on the target).

## Build the `.exe` installer

1. Install [Inno Setup](https://jrsoftware.org/isdl.php).
2. Run:

   ```powershell
   powershell -ExecutionPolicy Bypass -File publish.ps1 -MakeInstaller
   ```

   This produces `installer\Output\Jarvis-Setup-1.0.0.exe` — a standard Windows
   installer with Start Menu / desktop / startup shortcuts and a clean
   uninstaller that restores Explorer if the shell was replaced.

## Shell Replacement

Jarvis can replace Windows Explorer as the desktop shell. After installation:

1. Open the tray icon → right-click → "Replace Explorer shell"
2. Reboot
3. Windows boots directly into Jarvis (no Explorer desktop, no taskbar)

To restore Explorer:
- Right-click the tray icon → uncheck "Replace Explorer shell"
- Or run: `reg add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Winlogon" /v Shell /t REG_SZ /d "explorer.exe" /f`
- Or boot into Safe Mode and run the registry command above
- The uninstaller automatically restores Explorer

## Architecture

```
┌─────────────────────────────────────────────┐
│           Web UI (HTML/CSS/JS)               │
│  Dock · Assistant · Quick Settings · Widgets │
├───────────────────────────────────────────────┤
│         WebView2 (Chromium rendering)         │
├───────────────────────────────────────────────┤
│              Bridge (JSON RPC)                │
├───────────────────────────────────────────────┤
│              Jarvis.Core                      │
│  ShellService · SystemControl · ProcessMgr    │
│  WindowService · ConfigService                │
├───────────────────────────────────────────────┤
│              Jarvis.Windows                   │
│  WPF MainWindow · Win32 P/Invoke · Tray       │
│  WindowsSystemAccess · WindowsWindowAccess    │
├───────────────────────────────────────────────┤
│              Windows OS (Win32)               │
└───────────────────────────────────────────────┘
```

## Project Structure

```
jarvis-os/
├── Jarvis.sln
├── publish.ps1                  # build .exe (+ optional installer)
├── installer/setup.iss          # Inno Setup script
├── src/
│   ├── Jarvis.Core/             # shared core (platform-agnostic)
│   │   ├── Jarvis.Core.csproj
│   │   ├── AppContext.cs        # app state, data dir
│   │   ├── Bridge.cs            # JSON RPC: web <-> C# services
│   │   ├── IBridgeHost.cs       # platform interface
│   │   ├── ConfigService.cs     # persisted settings
│   │   ├── Shell/
│   │   │   ├── ShellService.cs  # dock, pinned apps, power actions
│   │   │   └── ISystemAccess.cs # platform-specific system access
│   │   ├── Services/
│   │   │   ├── SystemControlService.cs  # PowerShell, CMD, system info
│   │   │   ├── ProcessService.cs        # process list, launch, kill
│   │   │   └── WindowService.cs         # window list, focus, snap
│   │   └── Web/                 # embedded web UI
│   │       ├── index.html       # shell layout (dock, panels, widgets)
│   │       ├── styles.css       # dark theme
│   │       ├── app.js           # bridge, shell logic, UI events
│   │       ├── md.js            # markdown renderer
│   │       └── manifest.txt     # list of embedded resources
│   └── Jarvis.Windows/          # Windows-specific WPF app
│       ├── Jarvis.Windows.csproj
│       ├── App.xaml(.cs)        # app bootstrap, arg parsing
│       ├── MainWindow.xaml(.cs) # WebView2 host, tray, dark title bar
│       ├── NotifyIconHelper.cs  # system tray (WinForms NotifyIcon)
│       ├── WindowsSystemAccess.cs    # Win32: lock, shutdown, sleep, launch
│       ├── WindowsWindowAccess.cs    # Win32: enumerate, focus, snap windows
│       ├── GlobalUsings.cs
│       ├── app.manifest         # DPI awareness, UAC
│       └── Properties/Settings  # shell mode, start minimized
└── .github/workflows/           # CI (to be added)
```

## Data Location

All user data lives under `%LOCALAPPDATA%\Jarvis\`:
- `config.json` — settings
- `web/` — extracted UI cache (regenerated each launch)
- `pinned_apps.json` — dock pinned apps

## License

MIT — see [LICENSE](LICENSE).
