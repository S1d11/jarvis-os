#!/usr/bin/env bash
# shellcheck disable=SC2034
# Jarvis OS archiso profile definition
# See: https://wiki.archlinux.org/title/archiso

iso_name="jarvis-os"
iso_label="JARVIS_OS_$(date +%Y%m)"
iso_publisher="Jarvis OS Project <https://github.com/S1d11/jarvis-desktop>"
iso_application="Jarvis OS Installer"
iso_version="$(date +%Y.%m.%d)"
install_dir="jarvis"
buildmodes=('iso')
bootmodes=('bios.syslinux.mbr' 'bios.syslinux.eltorito'
           'uefi-x64.systemd-boot.esp' 'uefi-x64.systemd-boot.eltorito')
arch="x86_64"
pacman_conf="pacman.conf"
airootfs_image_type="squashfs"
airootfs_image_tool_options=('-comp' 'xz' '-Xbcj' 'x86' '-b' '1M' '-Xdict-size' '1M')
file_permissions=(
  ["/etc/shadow"]="0:0:400"
  ["/etc/gshadow"]="0:0:400"
  ["/root"]="0:0:750"
  ["/root/.automated_script.sh"]="0:0:755"
  ["/usr/local/bin/jarvis-shell"]="0:0:755"
  ["/usr/local/bin/jarvis-os-installer"]="0:0:755"
  ["/usr/local/bin/enroll-secure-boot"]="0:0:755"
  ["/usr/local/bin/setup-tpm"]="0:0:755"
  ["/etc/sudoers.d"]="0:0:750"
)
