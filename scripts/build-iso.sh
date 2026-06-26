#!/usr/bin/env bash
# build-iso.sh — Build the Jarvis OS bootable ISO
#
# This script runs on an Arch Linux system (or CI container) with
# archiso installed. It produces a bootable ISO that can be:
#   - Burned to a USB drive (dd or Rufus/balenaEtcher)
#   - Booted in a VM (QEMU, VirtualBox, VMware)
#   - Installed to a physical machine
#
# Usage:
#   ./scripts/build-iso.sh [--output-dir /path/to/output]
#
# Requirements:
#   - Arch Linux (or archlinux Docker container)
#   - archiso package: sudo pacman -S archiso
#   ~20 GB free disk space
#   ~30 minutes build time

set -euo pipefail

# ── Configuration ────────────────────────────────────────────
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
PROFILE_DIR="$PROJECT_ROOT/archiso"
OUTPUT_DIR="${1:-$PROJECT_ROOT/output}"
WORK_DIR="/tmp/jarvis-os-build"

# ── Colors ───────────────────────────────────────────────────
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

info()  { echo -e "${CYAN}[INFO]${NC}  $*"; }
ok()    { echo -e "${GREEN}[OK]${NC}    $*"; }
warn()  { echo -e "${YELLOW}[WARN]${NC}  $*"; }
error() { echo -e "${RED}[ERROR]${NC} $*"; exit 1; }

# ── Pre-flight checks ────────────────────────────────────────
info "Pre-flight checks..."

# Check if archiso is installed
if ! command -v mkarchiso >/dev/null 2>&1; then
    error "archiso is not installed. Run: sudo pacman -S archiso"
fi

# Check if running as root
if [ "$(id -u)" -ne 0 ]; then
    warn "Not running as root. Some operations may fail."
    warn "Recommended: sudo ./scripts/build-iso.sh"
fi

# Check disk space
AVAILABLE_GB=$(df -BG /tmp | awk 'NR==2 {gsub("G","",$4); print $4}')
if [ "$AVAILABLE_GB" -lt 15 ]; then
    error "Need at least 15 GB free in /tmp. Available: ${AVAILABLE_GB} GB"
fi

ok "Pre-flight checks passed."

# ── Prepare output directory ─────────────────────────────────
mkdir -p "$OUTPUT_DIR"

# ── Clean previous build ─────────────────────────────────────
info "Cleaning previous build..."
rm -rf "$WORK_DIR"
mkdir -p "$WORK_DIR"

# ── Install Jarvis into the airootfs ─────────────────────────
info "Preparing Jarvis Desktop for inclusion in ISO..."

# Clone Jarvis Desktop into the airootfs build area
JARVIS_BUILD="$PROFILE_DIR/airootfs/opt/jarvis"
mkdir -p "$JARVIS_BUILD"

# If Jarvis source is available locally, copy it. Otherwise clone from GitHub.
if [ -d "$PROJECT_ROOT/../jarvis-desktop" ]; then
    info "Copying Jarvis from local source..."
    cp -r "$PROJECT_ROOT/../jarvis-desktop"/* "$JARVIS_BUILD/"
elif [ -d "$PROJECT_ROOT/jarvis-desktop" ]; then
    info "Copying Jarvis from local source..."
    cp -r "$PROJECT_ROOT/jarvis-desktop"/* "$JARVIS_BUILD/"
else
    info "Cloning Jarvis from GitHub..."
    git clone --depth 1 https://github.com/S1d11/jarvis-desktop "$JARVIS_BUILD" || {
        warn "Failed to clone Jarvis. ISO will boot without Jarvis pre-installed."
        warn "Users can install Jarvis after booting the ISO."
    }
fi

# Create a PKGBUILD for Jarvis (so it can be installed via pacman)
cat > "$PROFILE_DIR/airootfs/usr/share/jarvis-os/PKGBUILD" <<'PKGBUILD'
pkgname=jarvis-desktop
pkgver=7.1.4
pkgrel=1
pkgdesc="Jarvis Desktop — AI-powered desktop environment and shell"
arch=('x86_64')
url="https://github.com/S1d11/jarvis-desktop"
license=('MIT')
depends=(
    'python' 'python-pip' 'python-pyqt6' 'python-pyqt6-webengine'
    'python-numpy' 'python-pillow' 'python-requests' 'python-aiohttp'
    'python-pydantic' 'python-yaml' 'python-orjson'
    'pipewire' 'pipewire-pulse' 'wireplumber'
    'xorg-server' 'xorg-xinit'
    'mesa' 'vulkan-icd-loader'
    'noto-fonts' 'ttf-dejavu'
)
makedepends=('python-build' 'python-wheel' 'python-setuptools')
source=("$pkgname::git+https://github.com/S1d11/jarvis-desktop.git")
sha256sums=('SKIP')

build() {
    cd "$srcdir/$pkgname"
    python -m build --wheel --no-isolation
}

package() {
    cd "$srcdir/$pkgname"
    pip install --root="$pkgdir" --no-deps dist/*.whl

    # Install shell launcher
    install -Dm755 "$srcdir/$pkgname/scripts/jarvis-shell" "$pkgdir/usr/local/bin/jarvis-shell"

    # Install session files
    install -Dm644 /dev/stdin "$pkgdir/usr/share/xsessions/jarvis.desktop" <<EOF
[Desktop Entry]
Name=Jarvis
Comment=Jarvis Desktop Shell
Exec=/usr/local/bin/jarvis-shell
TryExec=/usr/local/bin/jarvis-shell
Type=Application
DesktopNames=jarvis
EOF
}
PKGBUILD

# ── Build the ISO ────────────────────────────────────────────
echo ""
info "Building Jarvis OS ISO..."
info "Profile: $PROFILE_DIR"
info "Work dir: $WORK_DIR"
info "Output: $OUTPUT_DIR"
echo ""

mkarchiso \
    -v \
    -w "$WORK_DIR" \
    -o "$OUTPUT_DIR" \
    "$PROFILE_DIR"

# ── Verify the ISO ───────────────────────────────────────────
ISO_FILE=$(ls "$OUTPUT_DIR"/jarvis-os-*.iso 2>/dev/null | head -1)
if [ -z "$ISO_FILE" ]; then
    error "ISO was not created. Check build output above."
fi

ISO_SIZE=$(du -h "$ISO_FILE" | awk '{print $1}')
echo ""
ok "ISO built successfully!"
ok "File: $ISO_FILE"
ok "Size: $ISO_SIZE"
echo ""

# ── Generate checksums ───────────────────────────────────────
info "Generating checksums..."
cd "$OUTPUT_DIR"
sha256sum jarvis-os-*.iso > sha256sum.txt
md5sum jarvis-os-*.iso > md5sum.txt
ok "Checksums generated."

# ── Print usage instructions ─────────────────────────────────
echo ""
echo -e "${CYAN}========================================${NC}"
echo -e "${CYAN}  Jarvis OS ISO Build Complete${NC}"
echo -e "${CYAN}========================================${NC}"
echo ""
echo "To write to USB:"
echo "  sudo dd if=$ISO_FILE of=/dev/sdX bs=4M status=progress"
echo "  sync"
echo ""
echo "Or use balenaEtcher / Rufus to write the ISO to a USB drive."
echo ""
echo "To boot in QEMU:"
echo "  qemu-system-x86_64 -m 4096 -enable-kvm -cdrom $ISO_FILE -boot d"
echo ""
echo "To boot in VirtualBox:"
echo "  Create a new VM, select the ISO as the optical disk, boot."
echo ""
