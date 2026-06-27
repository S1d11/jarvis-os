// ════════════════════════════════════════════════════════════
// Jarvis Desktop Shell — JavaScript bridge
// ════════════════════════════════════════════════════════════

// ── Bridge: communicate with C# backend via WebView2 ────────
const Bridge = {
  _pending: new Map(),
  _handlers: new Map(),

  // Send a request to C# and await response
  async call(action, payload = {}) {
    const id = `rpc_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`;
    return new Promise((resolve, reject) => {
      this._pending.set(id, { resolve, reject });
      window.chrome.webview.postMessage(JSON.stringify({ id, action, ...payload }));
      // Timeout after 30s
      setTimeout(() => {
        if (this._pending.has(id)) {
          this._pending.get(id).reject(new Error('Request timeout'));
          this._pending.delete(id);
        }
      }, 30000);
    });
  },

  // Register a handler for push events from C#
  on(event, handler) {
    this._handlers.set(event, handler);
  },

  // Called by C# to deliver responses/events
  _receive(json) {
    const msg = JSON.parse(json);
    if (msg.id && this._pending.has(msg.id)) {
      const { resolve, reject } = this._pending.get(msg.id);
      this._pending.delete(msg.id);
      msg.ok ? resolve(msg.data) : reject(new Error(msg.error || 'Unknown error'));
    } else if (msg.event && this._handlers.has(msg.event)) {
      this._handlers.get(msg.event)(msg);
    }
  },
};

// Expose globally so C# can call Bridge._receive()
window.Bridge = Bridge;

// ── Shell state ─────────────────────────────────────────────
const Shell = {
  pinnedApps: [],
  runningApps: [],
  isShellMode: false,

  async refresh() {
    const state = await Bridge.call('shell.getState');
    this.pinnedApps = state.pinnedApps || [];
    this.runningApps = state.runningApps || [];
    this.isShellMode = state.isShellMode || false;
    this.renderDock();
  },

  async launchApp(name) {
    await Bridge.call('shell.launchApp', { name });
  },

  renderDock() {
    // Render pinned apps
    const pinnedEl = document.getElementById('dock-pinned');
    pinnedEl.innerHTML = '';
    for (const app of this.pinnedApps) {
      const el = document.createElement('div');
      el.className = 'dock-item';
      el.title = app.name;
      el.innerHTML = `<span class="dock-icon">${app.name[0] || 'A'}</span>`;
      el.onclick = () => this.launchApp(app.name);
      pinnedEl.appendChild(el);
    }

    // Render running apps
    const runningEl = document.getElementById('dock-running');
    runningEl.innerHTML = '';
    for (const app of this.runningApps) {
      const el = document.createElement('div');
      el.className = 'dock-item running';
      el.title = app.title || app.name;
      el.innerHTML = `<span class="dock-icon">${app.name[0] || 'A'}</span>`;
      el.onclick = () => Bridge.call('win.focus', { hwnd: app.hwnd });
      runningEl.appendChild(el);
    }
  },
};

// ── Assistant (chat) ────────────────────────────────────────
const Assistant = {
  open: false,
  messages: [],

  toggle() {
    this.open = !this.open;
    document.getElementById('assistant-panel').classList.toggle('hidden', !this.open);
    if (this.open) {
      document.getElementById('chat-input').focus();
    }
  },

  async send() {
    const input = document.getElementById('chat-input');
    const text = input.value.trim();
    if (!text) return;

    this.addMessage('user', text);
    input.value = '';

    // Show typing indicator
    const typing = this.addMessage('jarvis', 'Thinking...');

    try {
      // For now, just echo back. The AI backend will be connected later.
      const response = await this.getAIResponse(text);
      typing.querySelector('.msg-text').textContent = response;
    } catch (err) {
      typing.querySelector('.msg-text').textContent = `Error: ${err.message}`;
    }
  },

  async getAIResponse(text) {
    // Placeholder — will connect to Ollama or local LLM
    // For now, respond with system command results if applicable
    if (text.toLowerCase().startsWith('run ')) {
      const cmd = text.substring(5);
      const result = await Bridge.call('sys.powershell', { command: cmd });
      return `Exit code: ${result.exitCode}\n\n${result.stdout || result.stderr}`;
    }
    if (text.toLowerCase().startsWith('cmd ')) {
      const cmd = text.substring(4);
      const result = await Bridge.call('sys.cmd', { command: cmd });
      return `Exit code: ${result.exitCode}\n\n${result.stdout || result.stderr}`;
    }
    return `You said: "${text}". AI backend not yet connected.`;
  },

  addMessage(role, text) {
    const feed = document.getElementById('chat-feed');
    const el = document.createElement('div');
    el.className = `msg ${role}`;
    el.innerHTML = `<div class="msg-text"></div>`;
    el.querySelector('.msg-text').textContent = text;
    feed.appendChild(el);
    feed.scrollTop = feed.scrollHeight;
    return el;
  },
};

// ── Quick Settings ──────────────────────────────────────────
const QuickSettings = {
  open: false,

  toggle() {
    this.open = !this.open;
    document.getElementById('quick-settings').classList.toggle('hidden', !this.open);
    // Close other panels
    if (this.open) {
      document.getElementById('power-menu').classList.add('hidden');
      document.getElementById('start-menu').classList.add('hidden');
    }
  },

  async initVolume() {
    try {
      const vol = await Bridge.call('sys.getVolume');
      document.getElementById('qs-volume').value = vol;
    } catch { /* non-fatal */ }
  },
};

// ── Power Menu ──────────────────────────────────────────────
const PowerMenu = {
  open: false,

  toggle() {
    this.open = !this.open;
    document.getElementById('power-menu').classList.toggle('hidden', !this.open);
    if (this.open) {
      document.getElementById('quick-settings').classList.add('hidden');
      document.getElementById('start-menu').classList.add('hidden');
    }
  },

  async action(act) {
    this.toggle();
    switch (act) {
      case 'lock': await Bridge.call('shell.lockScreen'); break;
      case 'restart': await Bridge.call('shell.restart'); break;
      case 'sleep': await Bridge.call('shell.sleep'); break;
      case 'logoff': await Bridge.call('shell.logoff'); break;
      case 'shutdown': await Bridge.call('shell.shutdown'); break;
    }
  },
};

// ── Start Menu ──────────────────────────────────────────────
const StartMenu = {
  open: false,

  toggle() {
    this.open = !this.open;
    document.getElementById('start-menu').classList.toggle('hidden', !this.open);
    if (this.open) {
      document.getElementById('quick-settings').classList.add('hidden');
      document.getElementById('power-menu').classList.add('hidden');
      document.getElementById('start-search-input').focus();
      this.renderApps();
    }
  },

  renderApps(filter = '') {
    const apps = [
      { name: 'Notepad', icon: 'N' },
      { name: 'Calculator', icon: 'C' },
      { name: 'Paint', icon: 'P' },
      { name: 'CMD', icon: '>' },
      { name: 'PowerShell', icon: '>' },
      { name: 'Explorer', icon: 'E' },
      { name: 'Settings', icon: 'S' },
      { name: 'Task Manager', icon: 'T' },
      { name: 'Edge', icon: 'B' },
      { name: 'Steam', icon: 'G' },
      { name: 'Spotify', icon: '♪' },
      { name: 'Discord', icon: 'D' },
    ];

    const filtered = apps.filter(a =>
      a.name.toLowerCase().includes(filter.toLowerCase())
    );

    const grid = document.getElementById('start-apps');
    grid.innerHTML = '';
    for (const app of filtered) {
      const el = document.createElement('div');
      el.className = 'start-app';
      el.innerHTML = `
        <div class="start-app-icon">${app.icon}</div>
        <div class="start-app-name">${app.name}</div>
      `;
      el.onclick = () => {
        Shell.launchApp(app.name);
        this.toggle();
      };
      grid.appendChild(el);
    }
  },
};

// ── Clock Widget ────────────────────────────────────────────
function updateClock() {
  const now = new Date();
  const h = String(now.getHours()).padStart(2, '0');
  const m = String(now.getMinutes()).padStart(2, '0');
  document.getElementById('clock-time').textContent = `${h}:${m}`;

  const days = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'];
  const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
  document.getElementById('clock-date').textContent =
    `${days[now.getDay()]}, ${months[now.getMonth()]} ${now.getDate()}`;
}

// ── Event handlers ──────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
  // Dock clicks
  document.querySelector('.dock-item[data-app="jarvis"]').onclick = () => Assistant.toggle();
  document.querySelector('.dock-item[data-action="settings"]').onclick = () => QuickSettings.toggle();
  document.querySelector('.dock-item[data-action="power"]').onclick = () => PowerMenu.toggle();

  // Assistant
  document.getElementById('assistant-close').onclick = () => Assistant.toggle();
  document.getElementById('chat-send').onclick = () => Assistant.send();
  document.getElementById('chat-input').addEventListener('keydown', (e) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      Assistant.send();
    }
  });

  // Quick settings
  document.getElementById('qs-volume').oninput = (e) => {
    Bridge.call('sys.setVolume', { value: parseInt(e.target.value) });
  };
  document.getElementById('qs-lock').onclick = () => Bridge.call('shell.lockScreen');
  document.getElementById('qs-restart').onclick = () => Bridge.call('shell.restart');
  document.getElementById('qs-shutdown').onclick = () => Bridge.call('shell.shutdown');

  // Power menu
  document.querySelectorAll('.power-item').forEach(item => {
    item.onclick = () => PowerMenu.action(item.dataset.action);
  });

  // Start menu search
  document.getElementById('start-search-input').addEventListener('input', (e) => {
    StartMenu.renderApps(e.target.value);
  });

  // Clock widget — click opens quick settings
  document.getElementById('clock-widget').onclick = () => QuickSettings.toggle();

  // Initialize
  Shell.refresh();
  QuickSettings.initVolume();
  updateClock();
  setInterval(updateClock, 1000);

  // Open assistant by default in window mode
  if (!Shell.isShellMode) {
    // In window mode, show the assistant panel
    Assistant.open = true;
    document.getElementById('assistant-panel').classList.remove('hidden');
  }
});
