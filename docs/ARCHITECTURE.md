# Jarvis OS — Architecture

## Overview

Jarvis OS is a custom Linux-based operating system that uses Jarvis Desktop
as its primary desktop shell. It is built on the Linux kernel (which has
native TPM 2.0 and Secure Boot support), uses Wine + Proton for Windows
app/game compatibility, and replaces traditional desktop environments
(GNOME, KDE, XFCE) with the Jarvis AI-powered shell.

```
┌─────────────────────────────────────────────────────────┐
│                    User Applications                     │
│   Native Linux apps    │   Windows apps (Wine/Proton)    │
├────────────────────────┴────────────────────────────────┤
│                    Jarvis Desktop Shell                   │
│  (AI-powered DE — window manager, dock, widgets, apps)   │
├───────────────────────────────────────────────────────────┤
│              Display Server (X11 / Wayland)               │
├───────────────────────────────────────────────────────────┤
│                    System Services                        │
│  systemd │ NetworkManager │ PipeWire │ BlueZ │ tpm2-abrmd │
├───────────────────────────────────────────────────────────┤
│              Linux Kernel (x86_64, 6.x)                   │
│  TPM 2.0 │ Secure Boot │ GPU drivers │ Btrfs │ LUKS      │
├───────────────────────────────────────────────────────────┤
│              UEFI Firmware (Secure Boot)                  │
├───────────────────────────────────────────────────────────┤
│                    Hardware (x86_64)                      │
└─────────────────────────────────────────────────────────┘
```

## Component Breakdown

### 1. Kernel — Linux 6.x

Jarvis OS uses the mainline Linux kernel, which includes:

- **TPM 2.0**: Native `tpm_tis`, `tpm_crb`, `tpm2_ibm` drivers. The kernel
  exposes `/dev/tpmrm0` (resource manager) and `/dev/tpm0` (direct access).
  PCR values are available via `tpm2_pcrread`.

- **Secure Boot**: The kernel is built with `CONFIG_LOCK_DOWN_IN_EFI_SECURE_BOOT=y`,
  which enforces kernel lockdown (confidentiality mode) when booted with
  Secure Boot. This prevents userspace from accessing raw kernel memory,
  loading unsigned modules, or using `/dev/mem`.

- **GPU Drivers**: All three major GPU vendors are supported:
  - NVIDIA: Proprietary `nvidia-dkms` + `nvidia-drm.modeset=1`
  - AMD: Open-source `amdgpu` (built into kernel)
  - Intel: Open-source `i915` / `xe` (built into kernel)

- **Filesystems**: Btrfs (default, with zstd compression), ext4, XFS, NTFS
  (via ntfs-3g for Windows partition access), exFAT, F2FS.

### 2. Init System — systemd

Jarvis OS uses systemd as the init system and service manager. Key services:

| Service | Purpose |
|---------|---------|
| `sddm` | Display manager (login screen + autologin) |
| `jarvis-shell.service` | Jarvis Desktop as a systemd service |
| `NetworkManager` | Network management (WiFi, Ethernet, VPN) |
| `bluetooth` | Bluetooth (BlueZ) |
| `pipewire` | Audio server (replaces PulseAudio) |
| `tpm2-abrmd` | TPM 2.0 Access Broker & Resource Manager |
| `firewalld` | Firewall management |
| `apparmor` | Mandatory Access Control |

### 3. Desktop Shell — Jarvis Desktop

Jarvis Desktop replaces the traditional desktop environment. It provides:

- **Window manager**: Manages application windows (move, resize, minimize,
  maximize, snap layouts, virtual desktops)
- **Dock/taskbar**: Application launcher and running app switcher
- **Widget layer**: Floating widgets (clock, weather, calendar, etc.)
- **AI assistant**: Voice-activated assistant with local LLM inference
- **System tray**: Quick settings, connectivity, storage info
- **Lock screen**: Secure lock screen with fade animation
- **App store**: Install/remove applications
- **File manager**: Browse files (with NTFS support for Windows partitions)
- **Browser**: Built-in web browser
- **Settings**: Full system configuration panel

Jarvis runs in `--shell` mode, which means it IS the desktop. There is no
separate window manager (like Mutter or KWin) — Jarvis handles window
management directly via Qt's windowing capabilities and X11/Wayland protocols.

### 4. Windows App Compatibility — Wine + Proton

| Layer | Technology | What it runs |
|-------|-----------|--------------|
| Wine | Wine 9.x | General Windows applications (.exe, .msi) |
| Proton | Proton-GE | Windows games via Steam |
| DXVK | Vulkan-based DirectX | DirectX 9/10/11 games |
| VKD3D | Vulkan-based DX12 | DirectX 12 games |
| Wine-Mono | .NET replacement | .NET Windows apps |
| Wine-Gecko | HTML engine | Internet Explorer-based apps |

**32-bit support**: The `multilib` repository is enabled, providing 32-bit
libraries (`lib32-*`) required by many Windows games and older applications.

**GPU acceleration**: DXVK and VKD3D translate DirectX calls to Vulkan,
providing near-native GPU performance for Windows games.

### 5. Security

#### Secure Boot

```
UEFI Firmware
    ↓ verifies
Shim (signed by Microsoft, enrolled in firmware)
    ↓ verifies
GRUB (signed by Jarvis OS keys)
    ↓ verifies
Linux Kernel (signed by Jarvis OS keys)
    ↓ enforces
Kernel Lockdown (confidentiality mode)
```

The `enroll-secure-boot` script:
1. Generates a Platform Key (PK), Key Exchange Key (KEK), and database key (db)
2. Signs the kernel and GRUB bootloader
3. Enrolls the keys into UEFI firmware
4. Optionally includes Microsoft's keys for dual-boot compatibility

#### TPM 2.0

The `setup-tpm` script:
1. Loads TPM kernel modules
2. Starts `tpm2-abrmd` (resource manager)
3. Reads Platform Configuration Registers (PCRs)
4. Enables TPM-backed LUKS disk encryption

**PCR 7** is used for LUKS key sealing — the disk encryption key is sealed
to the current Secure Boot state. If the bootloader or kernel is modified,
PCR 7 changes and the disk cannot be decrypted, preventing offline attacks.

#### Disk Encryption

LUKS2 encryption with TPM sealing:
```
User boots → UEFI verifies signatures → Kernel loads → PCR 7 measured
→ tpm2-abrmd unseals LUKS key → Disk decrypted → Jarvis shell starts
```

### 6. Boot Process

```
Power On
    ↓
UEFI Firmware (Secure Boot verification)
    ↓
GRUB Bootloader (signed, verified by shim)
    ↓
Linux Kernel (signed, lockdown enforced)
    ↓
initramfs (loads TPM, LUKS, Btrfs modules)
    ↓
systemd (PID 1)
    ↓
NetworkManager, PipeWire, BlueZ, tpm2-abrmd
    ↓
SDDM (display manager — autologin as 'jarvis' user)
    ↓
jarvis-shell (Jarvis Desktop in --shell mode)
    ↓
User sees Jarvis desktop
```

### 7. Filesystem Layout

```
/                    Btrfs subvolume @ (root)
├── home/            Btrfs subvolume @home (user data)
├── var/             Btrfs subvolume @var (logs, caches)
├── tmp/             Btrfs subvolume @tmp (temporary files)
├── boot/
│   └── efi/         FAT32 EFI System Partition
└── opt/
    └── jarvis-desktop/   Jarvis Desktop source
```

### 8. User Model

- **`jarvis`** user: The primary desktop user. Member of `wheel` (sudo),
  `video`, `audio`, `input`, `storage`, `network`, `plugdev` groups.
- **`root`**: System administrator (disabled by default, use `sudo`).
- Autologin as `jarvis` via SDDM.

### 9. Package Management

- **pacman**: Arch Linux package manager (core repos + multilib)
- **AUR**: Arch User Repository (via `yay` or `paru`)
- **Flatpak**: Sandboxed desktop apps
- **Wine**: Windows `.exe` and `.msi` installers
- **Steam/Proton**: Windows games

### 10. Build System

The ISO is built using `archiso` (Arch Linux's ISO builder):

```
archiso profile (packages.x86_64, profiledef.sh, airootfs/)
    ↓
mkarchiso
    ↓
SquashFS root filesystem + kernel + initramfs
    ↓
GRUB bootloader (UEFI) + syslinux (BIOS)
    ↓
jarvis-os-YYYY.MM.DD-x86_64.iso
```

The build runs in a Docker container (`archlinux:latest`) via GitHub Actions,
producing a downloadable ISO on every tagged release.
