> **Jarvis is not an application.** It is a native Windows enhancement — invisible until you call it, like Spotlight on macOS.

# Jarvis for Windows

A native Windows AI assistant with a floating glass orb and a Siri-style transparent overlay. It coexists with Windows — it does NOT replace Explorer.

```
Press Win+J           → Siri-style overlay appears centered on screen
Say "Hey Jarvis"      → overlay appears
Click the glass orb   → overlay appears
Escape / click outside → back to your desktop
Drag the orb          → reposition it anywhere
```

## What It Looks Like

**Floating orb** — a dark glass sphere with a sweeping spectral light arc that floats above all windows. Drag it anywhere on screen. It remembers its position.

**Overlay** — a transparent, borderless window that appears centered over whatever you're doing. A glassmorphic input bar at the top. Your queries appear as centered dark pills below it. Quick toggles (Wi-Fi, Bluetooth, Focus, Flashlight, Calculator) sit underneath. Press Escape or click outside and you're back to your desktop.

## Summon Methods

| Method | How | Works everywhere? |
|--------|-----|-------------------|
| **Win+J** | Low-level keyboard hook (`WH_KEYBOARD_LL`) | Yes — intercepts before any app, including fullscreen games |
| **"Hey Jarvis"** | NAudio microphone + energy VAD | Yes — runs independently of foreground app |
| **Click orb** | Floating glass sphere, always visible | Yes — always on top |

## Two UI Surfaces

### Floating Orb — the persistent widget
- 80×80 glass sphere with 6 depth layers (see below)
- Always on top of all windows
- Drag anywhere — position saved to `orb_position.json`
- Click to summon the overlay
- Raw Win32 HWND on dedicated STA thread — zero WPF

### Overlay — the Siri-style assistant
- Transparent, borderless, always-on-top window
- Appears centered over your current desktop/work
- Glassmorphic input bar with sparkle + mic icons
- User messages: centered dark pill bubbles
- Jarvis responses: plain white text
- Quick toggles: Wi-Fi, Bluetooth, Airplane, Focus, Flashlight, Calculator
- Escape or click outside → dismisses instantly
- No taskbar entry, no window chrome, no borders

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

## Coexistence with Windows

Jarvis is a **well-behaved Windows citizen**:

- **Never replaces Explorer** — your desktop, taskbar, and Start menu stay intact
- **No system tray icon** — completely invisible until summoned
- **No taskbar entry** — `WS_EX_TOOLWINDOW` + `ShowInTaskbar=False`
- **No Alt+Tab** — `DWMWA_EXCLUDED_FROM_PEEK` + cloaking
- **Boot service** — registered as a scheduled task via `install.ps1`
- **Works in fullscreen games** — `WH_KEYBOARD_LL` intercepts Win+J before any app sees it

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│  Windows Desktop (Explorer running normally)                   │
│                                                                  │
│  ┌─────────────────┐    ┌───────────────────────────────────┐  │
│  │  Floating Orb   │    │  Transparent Overlay (on demand)  │  │
│  │  (NativeOrbWin) │    │  (MainWindow — borderless,        │  │
│  │  80x80 Win32    │    │   transparent, always-on-top)     │  │
│  │  always visible │    │                                   │  │
│  └────────┬────────┘    │   • Centered input bar            │  │
│           │ click       │   • Conversation feed             │  │
│           └────────────►│   • Quick toggles                 │  │
│                         │   • Escape / click-outside = hide  │  │
│                         └───────────────────────────────────┘  │
├──────────────────────────────────────────────────────────────────┤
│  LowLevelKeyboardHook — WH_KEYBOARD_LL (works in games)        │
├──────────────────────────────────────────────────────────────────┤
│  WakeWordService — NAudio "Hey Jarvis" detection               │
├──────────────────────────────────────────────────────────────────┤
│  Bridge (JSON-RPC) → Shell / SystemControl / Process / Window  │
├──────────────────────────────────────────────────────────────────┤
│  Win32 API — LockWorkStation, shutdown, EnumWindows, etc.         │
├──────────────────────────────────────────────────────────────────┤
│  Windows OS                                                      │
└─────────────────────────────────────────────────────────────┘
```

## Project Structure

```
jarvis-os/
├── install.ps1                  # Background service installer (no shell replacement)
│   # Copies to C:\Program Files\Jarvis
│   # Creates scheduled task (boot, hidden)
│   # -Uninstall: removes everything
├── publish.ps1                  # Build self-contained .exe
├── Jarvis.sln
└── src/
    ├── Jarvis.Core/
    │   ├── Bridge.cs            # JSON-RPC web ↔ C# services
    │   ├── ConfigService.cs     # Persisted settings
    │   ├── Shell/               # Dock, app launching, power
    │   ├── Services/            # System control, process, window
    │   └── Web/                 # Embedded web UI (embedded as resources)
    │       ├── index.html       # Siri-style overlay UI
    │       ├── styles.css       # Glassmorphic overlay styles
    │       ├── app.js           # Overlay logic
    │       ├── orb.html         # Compact orb UI
    │       ├── orb.css          # Glass sphere styles
    │       ├── orb.js           # Orb click → summon overlay
    │       └── manifest.txt     # Resource manifest
    └── Jarvis.Windows/
        ├── App.xaml.cs          # Background-only startup (overlay + orb)
        ├── NativeOrbWindow.cs   # Raw Win32 HWND + WebView2 controller
        ├── MainWindow.xaml(.cs) # Transparent overlay (borderless, topmost)
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

# Run
dotnet run --project src\Jarvis.Windows\Jarvis.Windows.csproj

# Publish self-contained .exe (no .NET runtime needed)
.\publish.ps1
```

## Install

```powershell
# Build
.\publish.ps1

# Install as background service (requires admin)
powershell -ExecutionPolicy Bypass -File install.ps1
```

After installation:
- No desktop shortcut, no Start Menu, no tray icon
- Jarvis starts at login via scheduled task
- Press **Win+J** or say **"Hey Jarvis"** or click the orb

### Uninstall

```powershell
powershell -ExecutionPolicy Bypass -File install.ps1 -Uninstall
```

## Data Location

All user data lives under `%LOCALAPPDATA%\Jarvis\`:
- `config.json` — settings
- `web/` — extracted UI cache
- `orb_position.json` — floating orb position

## License

MIT
