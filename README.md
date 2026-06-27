# Jarvis

Jarvis is not an application. It's a system-level AI assistant woven directly into Windows — invisible until you call it.

Press **Win+J** or say **"Hey Jarvis"** and a floating orb appears over whatever you're doing. Drag it anywhere on screen — it stays where you put it. **Click the orb and it goes fullscreen** — the entire screen becomes Jarvis, no borders, no chrome, just a full chatbot with text and voice input. Press Escape to collapse back to the floating orb. No window, no tray icon, no Alt+Tab entry, no Start Menu shortcut. It's just there, like a part of the OS.

## How It Works

```
Windows boots
  └── Scheduled task launches Jarvis.exe (hidden, no window)
        ├── LowLevelKeyboardHook (WH_KEYBOARD_LL) — listens for Win+J
        ├── WakeWordService (NAudio) — listens for "Hey Jarvis"
        └── NativeOrbWindow (raw Win32 HWND, hidden)
              └── Waiting…

User presses Win+J (or says "Hey Jarvis")
  └── NativeOrbWindow.Summon()
        ├── Win32 overlay appears at its saved position (invisible to Alt+Tab)
        ├── Orb animates in (compact mode — just the floating orb, 80x80)
        └── State: "Listening…"

User clicks the orb
  └── NativeOrbWindow.Expand()
        ├── Window resizes from 80x80 to FULLSCREEN (entire screen)
        ├── Jarvis chatbot appears — no borders, no chrome
        ├── Ambient orb glow in background
        ├── Chat feed (centered, max-width 800px)
        ├── Input bar with text + voice toggle
        └── Text input auto-focuses

User drags the orb (compact mode)
  └── WM_NCHITTEST returns HTCAPTION → Win32 native drag
        └── Position saved to orb_position.json on release

User types or speaks a request
  └── Bridge → ShellService / SystemControlService / ProcessService
        ├── Launch apps, run PowerShell, manage windows, control power
        └── Return result to chat feed

User presses Escape (or clicks the back arrow)
  └── Collapse back to compact floating orb at its saved position
```

## Two Modes

### Compact — the floating orb
- Just the orb (80x80), floats anywhere on screen, always on top
- **Drag it** anywhere — position is saved between sessions
- **Click it** → goes fullscreen Jarvis chatbot
- Invisible to Alt+Tab, Task View, window enumeration

### Fullscreen — the Jarvis chatbot
- Covers the entire screen — no borders, no chrome, no title bar
- Ambient orb glow pulsing in the background
- Top bar: mini orb + "Jarvis" title + state label + back button
- Chat feed: centered, max-width 800px, user/Jarvis message bubbles
- Input bar at the bottom with:
  - **Text mode**: Type to Jarvis (textarea + send button)
  - **Voice mode**: Speak to Jarvis (mic icon, disables text input)
- Toggle between text and voice with the buttons below the input
- **Escape** or back arrow → collapse back to the floating orb
        ├── Orb fades out + shrinks
        └── Window hides — Jarvis goes back to sleep
```

## Summon Methods

| Method | How | Works in fullscreen games? |
|--------|-----|---------------------------|
| **Win+J** | Low-level keyboard hook (`WH_KEYBOARD_LL`) | Yes — intercepts before any app sees the key |
| **"Hey Jarvis"** | NAudio microphone capture + energy VAD | Yes — runs independently of foreground app |

## The Orb

The orb is a **true Win32 overlay**, not a window:
- `WS_EX_NOACTIVATE` — never steals focus from the current app
- `WS_EX_TOOLWINDOW` — invisible to Alt+Tab and Task View
- `WS_EX_LAYERED` — per-pixel alpha transparency
- `DWMWA_EXCLUDED_FROM_PEEK` — excluded from Aero Peek
- `Topmost=True` — always above all other windows

It appears at the bottom-center of the screen, like Siri on macOS.

### Orb States

| State | Visual | When |
|-------|--------|------|
| **Summoned** | Orb scales in from 0 with snap-back bounce | Win+J or wake word |
| **Listening** | Core pulses, glow speeds up | Waiting for input |
| **Thinking** | Core spins, outer ring accelerates | Processing request |
| **Responding** | Core glow expands | Showing response |
| **Dismissed** | Orb shrinks to 0 and fades out | Escape, click-away, or "Dismiss" |

## Installation

Jarvis is installed as a **system component**, not an application:

```powershell
# Build the .exe
powershell -ExecutionPolicy Bypass -File publish.ps1

# Install into Windows (requires admin)
powershell -ExecutionPolicy Bypass -File install.ps1
```

The install script:
1. Copies `Jarvis.exe` to `C:\Program Files\Jarvis\`
2. Creates a scheduled task that starts Jarvis at user login (hidden, no window)
3. Optionally replaces Explorer with Jarvis as the shell (`-ShellMode`)

After installation:
- No desktop shortcut
- No Start Menu entry
- No system tray icon
- No visible window
- Just press **Win+J** or say **"Hey Jarvis"**

### Shell Mode

To replace Windows Explorer entirely (Jarvis becomes the desktop):

```powershell
powershell -ExecutionPolicy Bypass -File install.ps1 -ShellMode
```

This sets `HKLM\...\Winlogon\Shell` to `Jarvis.exe --shell`. On reboot, Windows boots directly into Jarvis — no taskbar, no Start menu, no Explorer desktop. Just the Jarvis dock, panels, and orb.

### Uninstall

```powershell
powershell -ExecutionPolicy Bypass -File install.ps1 -Uninstall
```

Restores Explorer if the shell was replaced, removes the scheduled task, deletes all files and user data.

## Build from Source

```powershell
# Requirements: .NET 10 SDK, Windows 10/11 x64

# Build and run in debug mode
dotnet run --project src\Jarvis.Windows\Jarvis.Windows.csproj

# Publish self-contained .exe (~170 MB, no .NET runtime needed)
.\publish.ps1

# Install into Windows
.\install.ps1
```

## Architecture

```
┌─────────────────────────────────────────────────┐
│  OrbWindow (true Win32 overlay)                  │
│  WebView2 → orb.html + orb.css + orb.js          │
│  Transparent · Topmost · No Alt+Tab · No focus   │
├───────────────────────────────────────────────────┤
│  LowLevelKeyboardHook (WH_KEYBOARD_LL)            │
│  Intercepts Win+J system-wide (even in games)     │
├───────────────────────────────────────────────────┤
│  WakeWordService (NAudio 16kHz mono)              │
│  Energy VAD + 2-syllable "Jar-vis" pattern match  │
├───────────────────────────────────────────────────┤
│  Bridge (JSON RPC)                                │
│  ShellService · SystemControlService ·            │
│  ProcessService · WindowService                   │
├───────────────────────────────────────────────────┤
│  Win32 API (P/Invoke)                             │
│  LockWorkStation · shutdown · EnumWindows ·       │
│  SetForegroundWindow · MoveWindow · PostMessage   │
├───────────────────────────────────────────────────┤
│  Windows OS                                       │
└─────────────────────────────────────────────────┘
```

## Project Structure

```
jarvis-os/
├── install.ps1                  # system component installer (no .exe installer)
├── publish.ps1                  # build self-contained .exe
├── Jarvis.sln
├── src/
│   ├── Jarvis.Core/             # platform-agnostic core
│   │   ├── Bridge.cs            # JSON RPC: web <-> C# services
│   │   ├── ConfigService.cs     # persisted settings
│   │   ├── Shell/               # dock, app launching, power actions
│   │   ├── Services/            # system control, process, window management
│   │   └── Web/                 # embedded web UI
│   │       ├── index.html       # full shell UI (dock, panels, widgets)
│   │       ├── styles.css       # premium dark theme
│   │       ├── app.js           # shell logic
│   │       ├── orb.html         # Siri-like orb overlay
│   │       ├── orb.css          # orb animations
│   │       └── orb.js           # orb state machine
│   └── Jarvis.Windows/          # Windows-specific
│       ├── App.xaml.cs          # background-only startup (no window, no tray)
│       ├── OrbWindow.xaml(.cs)  # true Win32 overlay (invisible to Alt+Tab)
│       ├── MainWindow.xaml(.cs) # full shell window (--shell mode only)
│       ├── LowLevelKeyboardHook.cs  # WH_KEYBOARD_LL for Win+J
│       ├── WakeWordService.cs   # NAudio "Hey Jarvis" detection
│       ├── WindowsSystemAccess.cs   # Win32: lock, shutdown, sleep, launch
│       └── WindowsWindowAccess.cs   # Win32: enumerate, focus, snap windows
└── README.md
```

## Data Location

All user data lives under `%LOCALAPPDATA%\Jarvis\`:
- `config.json` — settings
- `web/` — extracted UI cache (regenerated each launch)
- `pinned_apps.json` — dock pinned apps

## License

MIT
