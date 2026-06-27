// ═══════════════════════════════════════════════════════════════
// Jarvis Desktop Shell — Application Logic
// ═══════════════════════════════════════════════════════════════

// ── SVG icon library ──────────────────────────────────────────
const Icons = {
  notepad:   '<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><path d="M14 2v6h6M8 13h8M8 17h5"/></svg>',
  calc:      '<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><rect x="4" y="2" width="16" height="20" rx="2"/><path d="M8 6h8M8 10h2M14 10h2M8 14h2M14 14h2M8 18h2M14 18h2"/></svg>',
  paint:     '<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M19 11V4a2 2 0 0 0-2-2H4a2 2 0 0 0-2 2v10a2 2 0 0 0 2 2h7"/><path d="M15 22a4 4 0 0 0 4-4c0-3-4-7-4-7s-4 4-4 7a4 4 0 0 0 4 4z"/></svg>',
  cmd:       '<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><rect x="3" y="4" width="18" height="16" rx="2"/><path d="M7 9l3 3-3 3M13 15h4"/></svg>',
  powershell:'<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><rect x="3" y="4" width="18" height="16" rx="2"/><path d="M7 9l3 3-3 3M13 15h4"/></svg>',
  explorer:  '<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/></svg>',
  settings:  '<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6z"/><path d="M19.4 15a1.6 1.6 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.6 1.6 0 0 0-2.7 1.1V21a2 2 0 0 1-4 0v-.2A1.6 1.6 0 0 0 8 19.4l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.6 1.6 0 0 0-1.1-2.7H4a2 2 0 0 1 0-4h.2A1.6 1.6 0 0 0 5.6 8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.6 1.6 0 0 0 2.7-1.1V4a2 2 0 0 1 4 0v.2a1.6 1.6 0 0 0 2.7 1.1l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.6 1.6 0 0 0-1.1 2.7H21a2 2 0 0 1 0 4h-.2a1.6 1.6 0 0 0-1.4 1z"/></svg>',
  taskmgr:   '<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M3 3v18h18"/><path d="M7 14l3-3 3 3 5-5"/></svg>',
  edge:      '<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><circle cx="12" cy="12" r="10"/><path d="M12 2a10 10 0 0 0-8 4c2-1 6-1 8 1 2 2 4 6 6 6"/></svg>',
  steam:     '<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><circle cx="12" cy="12" r="10"/><circle cx="9" cy="14" r="2"/><circle cx="16" cy="9" r="2"/></svg>',
  spotify:   '<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><circle cx="12" cy="12" r="10"/><path d="M8 10c4-1 8 0 8 0M8.5 13c3-1 6 0 6 0M9 16c2-1 4 0 4 0"/></svg>',
  discord:   '<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M8 6c-3 1-5 4-5 8 0 0 2 3 6 3M16 6c3 1 5 4 5 8 0 0-2 3-6 3"/><circle cx="9" cy="13" r="1.5"/><circle cx="15" cy="13" r="1.5"/></svg>',
};

// ── Bridge: communicate with C# backend ───────────────────────
const Bridge = {
  _pending: new Map(),
  _handlers: new Map(),

  async call(action, payload = {}) {
    const id = `rpc_${Date.now()}_${Math.random().toString(36).slice(2, 8)}`;
    return new Promise((resolve, reject) => {
      this._pending.set(id, { resolve, reject });
      window.chrome.webview.postMessage(JSON.stringify({ id, action, ...payload }));
      setTimeout(() => {
        if (this._pending.has(id)) {
          this._pending.get(id).reject(new Error('Request timeout'));
          this._pending.delete(id);
        }
      }, 30000);
    });
  },

  on(event, handler) { this._handlers.set(event, handler); },

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
window.Bridge = Bridge;

// ── Panel manager (only one open at a time, with smooth close) ─
const Panels = {
  _all: ['assistant-panel', 'quick-settings', 'power-menu', 'start-menu'],

  toggle(id) {
    const el = document.getElementById(id);
    if (el.classList.contains('hidden')) {
      this.closeAll();
      el.classList.remove('hidden');
    } else {
      this.close(id);
    }
  },

  close(id) {
    const el = document.getElementById(id);
    if (!el || el.classList.contains('hidden')) return;
    el.style.animation = 'panel-in 0.2s reverse both';
    setTimeout(() => {
      el.classList.add('hidden');
      el.style.animation = '';
    }, 180);
  },

  closeAll() {
    for (const id of this._all) {
      const el = document.getElementById(id);
      if (el && !el.classList.contains('hidden')) {
        el.style.animation = 'panel-in 0.15s reverse both';
        setTimeout(() => { el.classList.add('hidden'); el.style.animation = ''; }, 140);
      }
    }
  },
};

// ── Shell state ───────────────────────────────────────────────
const Shell = {
  pinnedApps: [],
  runningApps: [],
  isShellMode: false,

  async refresh() {
    try {
      const state = await Bridge.call('shell.getState');
      this.pinnedApps = state.pinnedApps || [];
      this.runningApps = state.runningApps || [];
      this.isShellMode = state.isShellMode || false;
      this.renderDock();
    } catch { /* non-fatal */ }
  },

  async launchApp(name) {
    try { await Bridge.call('shell.launchApp', { name }); }
    catch { /* non-fatal */ }
  },

  renderDock() {
    const pinnedEl = document.getElementById('dock-pinned');
    pinnedEl.innerHTML = '';
    for (const app of this.pinnedApps) {
      pinnedEl.appendChild(this._dockItem(app.name, app.name[0] || 'A'));
    }

    const sepRunning = document.getElementById('dock-sep-running');
    const runningEl = document.getElementById('dock-running');
    runningEl.innerHTML = '';

    if (this.runningApps.length > 0 && (this.pinnedApps.length > 0 || true)) {
      sepRunning.style.display = this.pinnedApps.length > 0 ? 'block' : 'none';
    }

    for (const app of this.runningApps) {
      const item = this._dockItem(app.title || app.name, app.name[0] || 'A');
      item.classList.add('running');
      item.onclick = () => Bridge.call('win.focus', { hwnd: app.hwnd });
      runningEl.appendChild(item);
    }

    if (this.runningApps.length === 0) {
      sepRunning.style.display = 'none';
    }
  },

  _dockItem(label, iconChar) {
    const el = document.createElement('div');
    el.className = 'dock-item';
    el.title = label;
    el.innerHTML = `<span style="font-size:16px;font-weight:600;color:var(--accent);">${iconChar}</span>`;
    el.onclick = () => this.launchApp(label);
    return el;
  },
};

// ── Assistant ─────────────────────────────────────────────────
const Assistant = {
  open: false,
  initialized: false,

  toggle() {
    this.open = !this.open;
    Panels.toggle('assistant-panel');
    if (this.open) {
      if (!this.initialized) { this._showWelcome(); this.initialized = true; }
      setTimeout(() => document.getElementById('chat-input').focus(), 300);
    }
  },

  _showWelcome() {
    const feed = document.getElementById('chat-feed');
    feed.innerHTML = `
      <div class="chat-welcome">
        <div class="chat-welcome-orb"></div>
        <div class="chat-welcome-title">Hello, I'm Jarvis</div>
        <div class="chat-welcome-sub">Ask me anything, or try:<br><code style="font-family:var(--mono);font-size:12px;background:var(--surface-2);padding:2px 6px;border-radius:4px;">run Get-Process</code></div>
      </div>`;
  },

  async send() {
    const input = document.getElementById('chat-input');
    const text = input.value.trim();
    if (!text) return;

    // Remove welcome
    const welcome = document.querySelector('.chat-welcome');
    if (welcome) welcome.remove();

    this._addMessage('user', text);
    input.value = '';
    input.style.height = 'auto';

    // Typing indicator
    const typing = this._addTyping();

    try {
      const response = await this._getAIResponse(text);
      typing.remove();
      this._addMessage('jarvis', response);
    } catch (err) {
      typing.remove();
      this._addMessage('jarvis', `Error: ${err.message}`);
    }
  },

  async _getAIResponse(text) {
    const lower = text.toLowerCase();
    if (lower.startsWith('run ') || lower.startsWith('ps ')) {
      const cmd = text.substring(text.indexOf(' ') + 1);
      const result = await Bridge.call('sys.powershell', { command: cmd });
      let out = result.stdout || '';
      if (result.stderr) out += (out ? '\n' : '') + result.stderr;
      return `**Exit code:** ${result.exitCode}\n\n\`\`\`\n${out.trim() || '(no output)'}\n\`\`\``;
    }
    if (lower.startsWith('cmd ')) {
      const cmd = text.substring(4);
      const result = await Bridge.call('sys.cmd', { command: cmd });
      let out = result.stdout || '';
      if (result.stderr) out += (out ? '\n' : '') + result.stderr;
      return `**Exit code:** ${result.exitCode}\n\n\`\`\`\n${out.trim() || '(no output)'}\n\`\`\``;
    }
    return `You said: "${text}".\n\nAI backend not yet connected. Prefix commands with \`run\` or \`cmd\` to execute system commands.`;
  },

  _addMessage(role, text) {
    const feed = document.getElementById('chat-feed');
    const el = document.createElement('div');
    el.className = `msg ${role}`;
    if (role === 'jarvis') {
      el.innerHTML = window.Markdown.render(text);
    } else {
      el.textContent = text;
    }
    feed.appendChild(el);
    feed.scrollTop = feed.scrollHeight;
    return el;
  },

  _addTyping() {
    const feed = document.getElementById('chat-feed');
    const el = document.createElement('div');
    el.className = 'msg jarvis';
    el.innerHTML = '<div class="typing-dots"><span></span><span></span><span></span></div>';
    feed.appendChild(el);
    feed.scrollTop = feed.scrollHeight;
    return el;
  },
};

// ── Quick Settings ────────────────────────────────────────────
const QuickSettings = {
  open: false,
  toggle() { this.open = !this.open; Panels.toggle('quick-settings'); },

  async initVolume() {
    try {
      const vol = await Bridge.call('sys.getVolume');
      const slider = document.getElementById('qs-volume');
      slider.value = vol;
      document.getElementById('qs-volume-value').textContent = vol;
    } catch { /* non-fatal */ }
  },
};

// ── Power Menu ────────────────────────────────────────────────
const PowerMenu = {
  open: false,
  toggle() { this.open = !this.open; Panels.toggle('power-menu'); },

  async action(act) {
    this.toggle();
    const map = {
      lock: 'shell.lockScreen', restart: 'shell.restart', sleep: 'shell.sleep',
      logoff: 'shell.logoff', shutdown: 'shell.shutdown',
    };
    if (map[act]) { try { await Bridge.call(map[act]); } catch { /* non-fatal */ } }
  },
};

// ── Start Menu ────────────────────────────────────────────────
const StartMenu = {
  open: false,
  _apps: [
    { name: 'Notepad', icon: Icons.notepad },
    { name: 'Calculator', icon: Icons.calc },
    { name: 'Paint', icon: Icons.paint },
    { name: 'CMD', icon: Icons.cmd },
    { name: 'PowerShell', icon: Icons.powershell },
    { name: 'Explorer', icon: Icons.explorer },
    { name: 'Settings', icon: Icons.settings },
    { name: 'Task Manager', icon: Icons.taskmgr },
    { name: 'Edge', icon: Icons.edge },
    { name: 'Steam', icon: Icons.steam },
    { name: 'Spotify', icon: Icons.spotify },
    { name: 'Discord', icon: Icons.discord },
  ],

  toggle() {
    this.open = !this.open;
    Panels.toggle('start-menu');
    if (this.open) {
      setTimeout(() => document.getElementById('start-search-input').focus(), 300);
      this.renderApps('');
    }
  },

  renderApps(filter) {
    const filtered = this._apps.filter(a =>
      a.name.toLowerCase().includes(filter.toLowerCase())
    );
    const grid = document.getElementById('start-apps');
    grid.innerHTML = '';
    for (const app of filtered) {
      const el = document.createElement('div');
      el.className = 'start-app';
      el.innerHTML = `
        <div class="start-app-icon">${app.icon}</div>
        <div class="start-app-name">${app.name}</div>`;
      el.onclick = () => { Shell.launchApp(app.name); this.toggle(); };
      grid.appendChild(el);
    }
    if (filtered.length === 0) {
      grid.innerHTML = `<div style="grid-column:1/-1;text-align:center;padding:24px;color:var(--text-dim);font-size:13px;">No apps found</div>`;
    }
  },
};

// ── Clock ─────────────────────────────────────────────────────
function updateClock() {
  const now = new Date();
  const h = String(now.getHours()).padStart(2, '0');
  const m = String(now.getMinutes()).padStart(2, '0');
  document.getElementById('clock-time').textContent = `${h}:${m}`;
  const days = ['Sun','Mon','Tue','Wed','Thu','Fri','Sat'];
  const months = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];
  document.getElementById('clock-date').textContent =
    `${days[now.getDay()]} ${months[now.getMonth()]} ${now.getDate()}`;
}

// ── Auto-resize textarea ──────────────────────────────────────
function autoResize(el) {
  el.style.height = 'auto';
  el.style.height = Math.min(el.scrollHeight, 100) + 'px';
}

// ── Event wiring ──────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
  // Top bar
  document.getElementById('start-button').onclick = () => StartMenu.toggle();
  document.getElementById('clock-widget').onclick = () => QuickSettings.toggle();
  document.getElementById('quick-settings-button').onclick = () => QuickSettings.toggle();
  document.getElementById('power-button').onclick = () => PowerMenu.toggle();

  // Dock
  document.querySelector('.dock-item-jarvis').onclick = () => Assistant.toggle();
  document.querySelector('.dock-item[data-action="start"]').onclick = () => StartMenu.toggle();

  // Assistant
  document.getElementById('assistant-close').onclick = () => Assistant.toggle();
  document.getElementById('chat-send').onclick = () => Assistant.send();
  const chatInput = document.getElementById('chat-input');
  chatInput.addEventListener('keydown', (e) => {
    if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); Assistant.send(); }
  });
  chatInput.addEventListener('input', () => autoResize(chatInput));

  // Quick settings
  const volSlider = document.getElementById('qs-volume');
  volSlider.addEventListener('input', (e) => {
    document.getElementById('qs-volume-value').textContent = e.target.value;
    Bridge.call('sys.setVolume', { value: parseInt(e.target.value) });
  });
  const brightSlider = document.getElementById('qs-brightness');
  brightSlider.addEventListener('input', (e) => {
    document.getElementById('qs-brightness-value').textContent = e.target.value;
  });

  // Toggle tiles
  document.querySelectorAll('.qs-tile').forEach(tile => {
    tile.onclick = () => tile.classList.toggle('active');
  });
  document.getElementById('qs-lock').onclick = () => Bridge.call('shell.lockScreen');
  document.getElementById('qs-shutdown').onclick = () => Bridge.call('shell.shutdown');

  // Power menu
  document.querySelectorAll('.power-item').forEach(item => {
    item.onclick = () => PowerMenu.action(item.dataset.action);
  });

  // Start menu search
  document.getElementById('start-search-input').addEventListener('input', (e) => {
    StartMenu.renderApps(e.target.value);
  });

  // Click outside to close panels
  document.getElementById('desktop').addEventListener('click', () => Panels.closeAll());

  // Init
  Shell.refresh();
  QuickSettings.initVolume();
  updateClock();
  setInterval(updateClock, 1000);

  // Open assistant by default in window mode
  setTimeout(() => {
    if (!Shell.isShellMode) { Assistant.toggle(); }
  }, 400);
});
