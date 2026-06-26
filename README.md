# Jarvis OS

A custom Linux-based operating system with Jarvis as the desktop shell.

Jarvis OS replaces the traditional desktop environment (GNOME, KDE, XFCE)
with the Jarvis AI-powered shell. It boots directly into Jarvis, which
serves as the window manager, dock, widget layer, and AI assistant —
all in one.

## Features

- **Jarvis Desktop as the shell** — no GNOME/KDE/XFCE. Jarvis IS the desktop.
- **Windows app compatibility** — Wine + Proton pre-installed. Run any
  Windows `.exe` or game via Steam/Proton with near-native GPU performance.
- **TPM 2.0** — native kernel support. TPM-backed LUKS disk encryption
  with PCR 7 sealing (keys are tied to Secure Boot state).
- **Secure Boot** — kernel and bootloader signed with your own keys.
  Enrollment script included. Microsoft keys optional (for dual-boot).
- **All GPU drivers** — NVIDIA (proprietary), AMD (amdgpu), Intel (i915/xe).
- **Btrfs** — default filesystem with zstd compression, snapshots, and
  subvolumes for root/home/var/tmp.
- **32-bit support** — multilib enabled for older Windows games and apps.
- **AI on-device** — local LLM inference, voice recognition, vision —
  no cloud required.

## Quick Start

### 1. Download the ISO

Download the latest `jarvis-os-*.iso` from
[Releases](https://github.com/S1d11/jarvis-os/releases).

### 2. Write to USB

**Linux:**
```bash
sudo dd if=jarvis-os-*.iso of=/dev/sdX bs=4M status=progress
sync
```

**Windows:** Use [Rufus](https://rufus.ie) or [balenaEtcher](https://etcher.balena.io).

**macOS:**
```bash
diskutil unmountDisk /dev/diskN
sudo dd if=jarvis-os-*.iso of=/dev/rdiskN bs=4m
diskutil eject /dev/diskN
```

### 3. Boot

1. Insert the USB drive and boot from it (enable UEFI in BIOS if needed).
2. Select "Jarvis OS" from the boot menu.
3. The live environment boots directly into Jarvis Desktop.

### 4. Install to disk

Once booted into the live environment, open a terminal and run:

```bash
sudo jarvis-os-installer
```

Follow the prompts to:
- Select a target disk
- Choose filesystem (Btrfs recommended)
- Enable TPM 2.0 disk encryption (optional)
- Install NVIDIA drivers (optional)
- Install Wine + Proton (recommended)
- Create a user account

### 5. Post-install

**Secure Boot:**
```bash
sudo enroll-secure-boot
```

**TPM 2.0 disk encryption:**
```bash
sudo setup-tpm
```

## Running Windows Apps

### General Windows applications
```bash
wine /path/to/application.exe
```

### Windows games via Steam
```bash
steam
```
Steam's Proton compatibility layer runs Windows games automatically.
In Steam → Settings → Compatibility → "Enable Steam Play for all titles".

### Windows games directly (without Steam)
```bash
proton run /path/to/game.exe
```

### Winetricks (install Windows dependencies)
```bash
winetricks d3dx9 vcrun2019 dotnet48
```

## Architecture

```
┌─────────────────────────────────────────────────────┐
│              User Applications                       │
│   Native Linux apps  │  Windows apps (Wine/Proton)  │
├──────────────────────┴──────────────────────────────┤
│              Jarvis Desktop Shell                    │
│  (AI-powered DE — WM, dock, widgets, assistant)     │
├──────────────────────────────────────────────────────┤
│           Display Server (X11 / Wayland)             │
├──────────────────────────────────────────────────────┤
│              System Services                         │
│  systemd │ NetworkManager │ PipeWire │ TPM2 │ BlueZ  │
├──────────────────────────────────────────────────────┤
│           Linux Kernel 6.x (x86_64)                  │
│  TPM 2.0 │ Secure Boot │ GPU drivers │ Btrfs │ LUKS  │
├──────────────────────────────────────────────────────┤
│           UEFI Firmware (Secure Boot)                │
└──────────────────────────────────────────────────────┘
```

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for full details.

## Build from Source

### Prerequisites
- Arch Linux (or use the Docker-based CI build)
- `archiso` package: `sudo pacman -S archiso`
- ~20 GB free disk space

### Build the ISO
```bash
git clone https://github.com/S1d11/jarvis-os
cd jarvis-os
sudo ./scripts/build-iso.sh
```

The ISO will be in `output/jarvis-os-YYYY.MM.DD-x86_64.iso`.

### Build via Docker (any Linux host)
```bash
docker run --rm --privileged \
  -v "$PWD:/work" \
  -v "$PWD/output:/output" \
  archlinux:latest \
  bash -c 'pacman -Syu --noconfirm archiso git && cd /work && mkarchiso -v -w /tmp/build -o /output archiso/'
```

### Build via GitHub Actions
Push a tag (`git tag v1.0.0 && git push origin v1.0.0`) and the CI
workflow will build the ISO and attach it to a GitHub Release.

## Project Structure

```
jarvis-os/
├── archiso/                    # archiso profile
│   ├── profiledef.sh           # ISO metadata
│   ├── packages.x86_64         # Package list (400+ packages)
│   ├── pacman.conf             # pacman config (multilib + AUR)
│   ├── airootfs/               # Root filesystem overlay
│   │   ├── etc/
│   │   │   ├── systemd/system/ # systemd services
│   │   │   │   └── jarvis-shell.service
│   │   │   ├── sddm.conf.d/    # Display manager autologin
│   │   │   └── security/       # Security policies
│   │   ├── usr/
│   │   │   ├── local/bin/      # System scripts
│   │   │   │   ├── jarvis-shell          # Shell launcher
│   │   │   │   ├── jarvis-os-installer   # OS installer
│   │   │   │   ├── enroll-secure-boot    # Secure Boot setup
│   │   │   │   └── setup-tpm             # TPM 2.0 setup
│   │   │   └── share/
│   │   │       ├── xsessions/jarvis.desktop
│   │   │       └── wayland-sessions/jarvis.desktop
│   │   └── root/.config/jarvis/  # Default Jarvis config
│   ├── syslinux/               # BIOS boot config
│   └── boot/grub/              # UEFI boot config
├── scripts/
│   ├── build-iso.sh            # ISO build script
│   └── install-jarvis.sh       # Jarvis installation into ISO
├── docs/
│   └── ARCHITECTURE.md         # Full architecture document
└── .github/workflows/
    └── build-iso.yml           # CI: build ISO on tag push
```

## Security

### Secure Boot Flow
```
UEFI → Shim (MS-signed) → GRUB (Jarvis-signed) → Kernel (Jarvis-signed)
→ Kernel Lockdown (confidentiality) → TPM PCR 7 measured
```

### TPM 2.0 Disk Encryption
```
Boot → PCR 7 measured → tpm2-abrmd unseals LUKS key → Disk decrypted
```

If the kernel or bootloader is tampered with, PCR 7 changes and the disk
cannot be decrypted, preventing offline attacks.

## Compatibility

| What | Status |
|------|--------|
| Windows .exe apps | Wine 9.x |
| Windows games (Steam) | Proton-GE |
| DirectX 9/10/11 games | DXVK (Vulkan) |
| DirectX 12 games | VKD3D (Vulkan) |
| .NET apps | Wine-Mono |
| NVIDIA GPUs | Proprietary drivers |
| AMD GPUs | amdgpu (open-source) |
| Intel GPUs | i915 / xe (open-source) |
| TPM 2.0 | Kernel native |
| Secure Boot | sbctl + shim |
| Bluetooth | BlueZ |
| WiFi | NetworkManager + wpa_supplicant |
| Audio | PipeWire (PulseAudio-compatible) |
| NTFS partitions | ntfs-3g (read/write) |

## License

MIT — see [LICENSE](LICENSE).

## Credits

- Linux kernel by Linus Torvalds and contributors
- archiso by Arch Linux
- Wine by WineHQ
- Proton by Valve
- Jarvis Desktop by Siddharth Reddy Kota
