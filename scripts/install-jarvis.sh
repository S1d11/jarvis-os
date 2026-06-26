#!/usr/bin/env bash
# install-jarvis.sh — Install Jarvis Desktop into the Jarvis OS live image
#
# This script runs inside the archiso build process to install
# Jarvis Desktop and all its dependencies into the airootfs.
# It's called by build-iso.sh before mkarchiso.

set -euo pipefail

AIROOTFS="${1:-/tmp/jarvis-os-build/airootfs}"

if [ ! -d "$AIROOTFS" ]; then
    echo "ERROR: airootfs not found at $AIROOTFS" >&2
    exit 1
fi

echo "[install-jarvis] Installing Jarvis Desktop into $AIROOTFS..."

# ── Install Python dependencies ──────────────────────────────
echo "[install-jarvis] Installing Python packages..."
arch-chroot "$AIROOTFS" pip install --system \
    pyqt6 \
    pyqt6-webengine \
    numpy \
    pillow \
    requests \
    aiohttp \
    pydantic \
    pyyaml \
    orjson \
    rich \
    httpx \
    websockets \
    pydub \
    pyaudio \
    opencv-python \
    onnxruntime \
    2>/dev/null || echo "[install-jarvis] Some pip packages failed (non-fatal)"

# ── Clone and install Jarvis ─────────────────────────────────
echo "[install-jarvis] Cloning Jarvis Desktop..."
JARVIS_SRC="$AIROOTFS/opt/jarvis-desktop"
if [ -d "$JARVIS_SRC" ]; then
    rm -rf "$JARVIS_SRC"
fi

git clone --depth 1 https://github.com/S1d11/jarvis-desktop "$JARVIS_SRC" || {
    echo "[install-jarvis] WARNING: Failed to clone Jarvis. ISO will boot without Jarvis."
    exit 0
}

# ── Install Jarvis into the airootfs ─────────────────────────
echo "[install-jarvis] Installing Jarvis..."
cd "$JARVIS_SRC"
pip install --root="$AIROOTFS" --no-deps . 2>/dev/null || {
    echo "[install-jarvis] WARNING: pip install failed. Trying manual install."
    # Manual install: copy the jarvis package
    mkdir -p "$AIROOTFS/usr/lib/python3.12/site-packages"
    cp -r jarvis "$AIROOTFS/usr/lib/python3.12/site-packages/"
}

# ── Create jarvis binary symlink ─────────────────────────────
mkdir -p "$AIROOTFS/usr/bin"
cat > "$AIROOTFS/usr/bin/jarvis" <<'JARVIS_BIN'
#!/usr/bin/env python3
import sys
from jarvis.__main__ import main
if __name__ == "__main__":
    sys.exit(main())
JARVIS_BIN
chmod +x "$AIROOTFS/usr/bin/jarvis"

# ── Create Jarvis config directory ───────────────────────────
mkdir -p "$AIROOTFS/etc/jarvis"
cat > "$AIROOTFS/etc/jarvis/default-config.yaml" <<'CFG'
# Default Jarvis OS configuration
user:
  name: "User"

llm:
  backend: "local"
  model: "llama3.2:3b"
  reasoning: false

voice:
  wake_word_enabled: false
  tts_voice: "en-US-AriaNeural"

ui:
  theme: "dark"
  accent: "cyan"

automation:
  invisible_input: false
  safe_mode: true

features:
  brain_visualizer: true
  widget_board: true
  app_store: true
  browser: true
  file_manager: true
  calendar: true
  weather: true
  clock: true
  calculator: true
  task_manager: true
  clipboard_manager: true
  screenshot: true
  uninstall_manager: true
  display_settings: true
  power_menu: true
  run_dialog: true
  accessibility: true
  connectivity: true
  storage_info: true
  user_profile: true
  desktop_shortcuts: true
  file_associations: true
  recycle_bin: true
  session_manager: true
  theme_engine: true
  dock: true
  widget_layer: true
  snap_layouts: true
  virtual_desktops: true
  quick_settings: true
  context_menus: true
  window_manager: true
  workspace: true
  system_tray: true
  lock_screen: true
  shell_settings: true
  installer: true
  updater: true
  onboarding: true
  mcp_server: true
  langgraph: true
  gnn_memory: true
  semantic_memory: true
  proactive: true
  vision: true
  web_search: true
  web_scraper: true
  omniparser: true
  headless_browser: true
  persistent_browser: true
  automation_input: true
  automation_windows: true
  universal_launcher: true
  app_switcher: true
  system_apps: true
  system_control: true
  ai_interaction: true
  control_hooks: true
  voice_wake: true
  voice_engine: true
  voice_tts: true
  voice_stt: true
  voice_porcupine: true
  local_llm: true
  local_vision: true
  memory_store: true
  memory_gnn: true
  memory_semantic: true
  mcp_tools: true
  feature_flags: true
  config_manager: true
  event_bus: true
  orchestrator: true
  shell_apps: true
  shell_ui: true
  shell_core: true
CFG

echo "[install-jarvis] Jarvis Desktop installed successfully."
