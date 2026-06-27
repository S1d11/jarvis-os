> **Jarvis is not an application.** It is a system-level AI assistant woven directly into Windows — invisible until you call it.

# Jarvis for Windows

A native Windows AI assistant with a glass sphere orb, Siri-style fullscreen conversation, wake word detection, and deep OS integration.

```
Press Win+J or say "Hey Jarvis" → floating glass orb appears
Click the orb → entire screen becomes a Siri-style chatbot
Drag the orb anywhere → it remembers its position
Escape → back to the floating orb, invisible until next summon
```

## What It Looks Like

**Floating orb** — a dark glass sphere with a sweeping spectral light arc, caustic rim refraction, and a sharp glossy surface highlight. It floats anywhere on screen, always on top, invisible to Alt+Tab and window enumeration.

**Fullscreen** — click the orb and the entire screen becomes Jarvis. True black background. Your queries appear as centered dark pills. Jarvis responds in clean white text. A single dark input bar at the bottom with a mic/text toggle. No borders, no chrome, no window decorations — just the conversation.

## Summon Methods

| Method | How | Works everywhere? |
|--------|-----|-------------------|
| **Win+J** | Low-level keyboard hook (`WH_KEYBOARD_LL`) | Yes — intercepts before any app, including fullscreen games |
| **"Hey Jarvis"** | NAudio microphone + energy VAD | Yes — runs independently of foreground app |

## Two Modes

### Compact — the floating glass orb
- 80x80 glass sphere with 6 depth layers (see below)
- Drag anywhere — position saved to `orb_position.json`
- Click → goes fullscreen
- Invisible to Alt+Tab, Task View, window enumeration
- Raw Win32 HWND on dedicated STA thread — zero WPF

### Fullscreen — Siri-style conversation
- Covers entire screen, no borders, no chrome
- True black `#000000` background
- User queries: centered dark grey pill (`#1C1C1E`)
- Jarvis responses: plain white text, 17px
- Bottom input bar: dark pill with sparkle icon, text input, mic toggle
- Toggle text/voice with the mic icon inside the bar
- Escape → collapse back to floating orb

## The Glass Sphere Orb

The compact orb is a **true volumetric glass sphere** with 6 depth layers:

| Layer | Element | Effect |
|-------|---------|--------|
| 0 | `#orb-bloom` | Large soft spectral glow (20px blur) |
| 1 | `#orb-outer-ring` | 1px subtle white ring, breathing |
| 2 | `#orb-rim` | **Bright caustic rim** — light concentrating at sphere edge |
| 3 | `#orb-core` | Dark glass body with top-left reflection, edge light, deep center |
| 3a | `#orb-caustic` | Internal spectral light sweeping inside (screen blend) |
| 3b | `#orb-caustic-2` | Secondary faint caustic for depth |
| 4 | `#orb-reflection` | **Sharp glossy surface highlight** — crisp white ellipse |
| 5 | `#orb-shimmer` | Soft secondary reflection, drifts subtly |

The spectral colors sweep through the glass: red → orange/yellow → green → blue → violet, at very low opacity (6–22%) with heavy blur. The `mix-blend-mode: screen` on internal caustics makes them glow from within rather than sit on top.

## Deep Windows Integration

Jarvis is not a window. It is a **system component**:

- **No WPF Window** for the orb — raw `CreateWindowEx` on a dedicated STA thread
- **No system tray icon** — completely invisible until summoned
- **No taskbar entry** — `WS_EX_TOOLWINDOW` + `WS_EX_NOACTIVATE`
- **No Alt+Tab** — `DWMWA_EXCLUDED_FROM_PEEK` + cloaking
- **Boot service** — registered as a scheduled task via `install.ps1`
- **Works in fullscreen games** — `WH_KEYBOARD_LL` intercepts Win+J before any app sees it

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  NativeOrbWindow — raw Win32 HWND on dedicated STA thread      │
│  WebView2 via CoreWebView2Controller (COM, not WPF wrapper)    │
│  WS_POPUP | WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW│
│  WS_EX_TOPMOST | WS_EX_NOREDIRECTIONBITMAP                    │
├───────────────────────────────────────────────────────────────┤
│  LowLevelKeyboardHook — WH_KEYBOARD_LL (works in games)        │
├───────────────────────────────────────────────────────────────┤
│  WakeWordService — NAudio 16kHz mono, energy VAD             │
├───────────────────────────────────────────────────────────────┤
│  Bridge (JSON-RPC) → Shell / SystemControl / Process / Window │
├───────────────────────────────────────────────────────────────┤
│  Win32 API — LockWorkStation, shutdown, EnumWindows, etc.       │
├───────────────────────────────────────────────────────────────┤
│  Windows OS                                                    │
└─────────────────────────────────────────────────────────────┘
```

## Project Structure

```
jarvis-os/
├── install.ps1                  # System component installer
│   # Copies to C:\Program Files\Jarvis
│   # Creates scheduled task (boot, hidden)
│   # -ShellMode: replaces Explorer
│   # -Uninstall: restores everything
├── publish.ps1                  # Build self-contained .exe
├── Jarvis.sln
└── src/
    ├── Jarvis.Core/
    │   ├── Bridge.cs            # JSON-RPC web ↔ C# services
    │   ├── ConfigService.cs     # Persisted settings
    │   ├── Shell/               # Dock, app launching, power
    │   ├── Services/            # System control, process, window
    │   └── Web/                 # Embedded web UI (embedded as resources)
    │       ├── index.html       # Full shell UI (--shell mode)
    │       ├── styles.css
    │       ├── app.js
    │       ├── orb.html         # Orb UI (compact + fullscreen)
    │       ├── orb.css          # Glass sphere + Siri conversation styles
    │       ├── orb.js           # Orb state machine, drag, expand/collapse
    │       └── manifest.txt     # Resource manifest
    └── Jarvis.Windows/
        ├── App.xaml.cs          # Background-only startup (no window, no tray)
        ├── NativeOrbWindow.cs   # Raw Win32 HWND + WebView2 controller
        ├── MainWindow.xaml(.cs) # Full desktop shell (--shell mode)
        ├── LowLevelKeyboardHook.cs  # WH_KEYBOARD_LL for Win+J
        ├── WakeWordService.cs   # NAudio "Hey Jarvis" detection
        ├── WindowsSystemAccess.cs   # Win32 lock, shutdown, sleep, launch
        └── WindowsWindowAccess.cs   # Win32 enumerate, focus, snap windows
```

## Build

```powershell
# Requirements: .NET 10 SDK, Windows 10/11 x64, WebView2 Runtime

# Build
dotnet build src\Jarvis.Windows\Jarvis.Windows.csproj -c Release

# Run in debug mode
dotnet run --project src\Jarvis.Windows\Jarvis.Windows.csproj

# Publish self-contained .exe (no .NET runtime needed)
.\publish.ps1
```

## Install

```powershell
# Build
.\publish.ps1

# Install as system component (requires admin)
powershell -ExecutionPolicy Bypass -File install.ps1
```

After installation:
- No desktop shortcut, no Start Menu, no tray icon
- Jarvis starts at login via scheduled task
- Press **Win+J** or say **"Hey Jarvis"**

### Shell Mode

Replace Windows Explorer entirely:

```powershell
powershell -ExecutionPolicy Bypass -File install.ps1 -ShellMode
```

Boots directly into Jarvis — no taskbar, no Start menu, no Explorer desktop.

### Uninstall

```powershell
powershell -ExecutionPolicy Bypass -File install.ps1 -Uninstall
```

## Data Location

All user data lives under `%LOCALAPPDATA%\Jarvis\`:
- `config.json` — settings
- `web/` — extracted UI cache
- `orb_position.json` — floating orb position
- `pinned_apps.json` — dock pinned apps

## License

MIT
