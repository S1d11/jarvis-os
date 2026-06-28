// ═══════════════════════════════════════════════════════════════
// Jarvis Desktop Shell — Application Logic
// ═══════════════════════════════════════════════════════════════

// ── SVG icon library ──────────────────────────────────────────
const Icons = {
  notepad:    '<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"/><path d="M14 2v6h6M8 13h8M8 17h5"/></svg>',
  calc:       '<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><rect x="4" y="2" width="16" height="20" rx="2"/><path d="M8 6h8M8 10h2M14 10h2M8 14h2M14 14h2M8 18h2M14 18h2"/></svg>',
  paint:      '<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M19 11V4a2 2 0 0 0-2-2H4a2 2 0 0 0-2 2v10a2 2 0 0 0 2 2h7"/><path d="M15 22a4 4 0 0 0 4-4c0-3-4-7-4-7s-4 4-4 7a4 4 0 0 0 4 4z"/></svg>',
  cmd:        '<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><rect x="3" y="4" width="18" height="16" rx="2"/><path d="M7 9l3 3-3 3M13 15h4"/></svg>',
  powershell: '<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><rect x="3" y="4" width="18" height="16" rx="2"/><path d="M7 9l3 3-3 3M13 15h4"/></svg>',
  explorer:   '<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M3 7a2 2 0 0 1 2-2h4l2 2h8a2 2 0 0 1 2 2v8a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/></svg>',
  settings:   '<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M12 15a3 3 0 1 0 0-6 3 3 0 0 0 0 6z"/><path d="M19.4 15a1.6 1.6 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.6 1.6 0 0 0-2.7 1.1V21a2 2 0 0 1-4 0v-.2A1.6 1.6 0 0 0 8 19.4l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.6 1.6 0 0 0-1.1-2.7H4a2 2 0 0 1 0-4h.2A1.6 1.6 0 0 0 5.6 8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.6 1.6 0 0 0 2.7-1.1V4a2 2 0 0 1 4 0v.2a1.6 1.6 0 0 0 2.7 1.1l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.6 1.6 0 0 0-1.1 2.7H21a2 2 0 0 1 0 4h-.2a1.6 1.6 0 0 0-1.4 1z"/></svg>',
  taskmgr:    '<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M3 3v18h18"/><path d="M7 14l3-3 3 3 5-5"/></svg>',
  edge:       '<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><circle cx="12" cy="12" r="10"/><path d="M12 2a10 10 0 0 0-8 4c2-1 6-1 8 1 2 2 4 6 6 6"/></svg>',
  steam:      '<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><circle cx="12" cy="12" r="10"/><circle cx="9" cy="14" r="2"/><circle cx="16" cy="9" r="2"/></svg>',
  spotify:    '<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><circle cx="12" cy="12" r="10"/><path d="M8 10c4-1 8 0 8 0M8.5 13c3-1 6 0 6 0M9 16c2-1 4 0 4 0"/></svg>',
  discord:    '<svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><path d="M8 6c-3 1-5 4-5 8 0 0 2 3 6 3M16 6c3 1 5 4 5 8 0 0-2 3-6 3"/><circle cx="9" cy="13" r="1.5"/><circle cx="15" cy="13" r="1.5"/></svg>',
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

// ── Floating panels (Quick Settings, Power, App Grid) ──────────
const Panels = {
  _all: ['quick-settings', 'power-menu', 'app-grid'],

  toggle(id) {
    const el = document.getElementById(id);
    if (!el) return;
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
    setTimeout(() => { el.classList.add('hidden'); el.style.animation = ''; }, 180);
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

// ── Fullscreen Assistant Overlay ──────────────────────────────
const Assistant = {
  open: false,
  initialized: false,

  toggle() {
    const overlay = document.getElementById('assistant-overlay');
    if (!overlay) return;
    this.open = !this.open;

    if (this.open) {
      overlay.classList.remove('hidden');
      // Force reflow
      void overlay.offsetWidth;
      overlay.classList.add('visible');
      if (!this.initialized) { this._showWelcome(); this.initialized = true; }
      setTimeout(() => {
        const input = document.getElementById('chat-input');
        if (input) input.focus();
      }, 350);
    } else {
      overlay.classList.remove('visible');
      setTimeout(() => overlay.classList.add('hidden'), 350);
    }
  },

  _showWelcome() {
    const feed = document.getElementById('chat-feed');
    if (!feed) return;
    feed.innerHTML = `
      <div style="text-align:center;padding:40px 20px;opacity:0.6;">
        <div style="font-size:15px;font-weight:500;margin-bottom:8px;color:var(--text);">Hello, I'm Jarvis</div>
        <div style="font-size:13px;color:var(--text-secondary);">Ask me anything, or try:</div>
        <div style="font-size:12px;color:var(--text-tertiary);margin-top:8px;font-family:monospace;background:var(--bg-elevated);display:inline-block;padding:4px 10px;border-radius:8px;">run Get-Process</div>
      </div>`;
  },

  async send() {
    const input = document.getElementById('chat-input');
    if (!input) return;
    const text = input.value.trim();
    if (!text) return;

    // Remove welcome
    const welcome = document.querySelector('#chat-feed > div[style*="text-align:center"]');
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
    if (!feed) return;
    const el = document.createElement('div');
    el.className = `msg ${role}`;
    if (role === 'jarvis' && window.Markdown) {
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
    if (!feed) return document.createElement('div');
    const el = document.createElement('div');
    el.className = 'typing-indicator';
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

// ── App Grid ──────────────────────────────────────────────────
const AppGrid = {
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
    Panels.toggle('app-grid');
    if (this.open) this.renderApps();
  },

  renderApps() {
    const grid = document.getElementById('app-grid-list');
    if (!grid) return;
    grid.innerHTML = '';
    for (const app of this._apps) {
      const el = document.createElement('div');
      el.className = 'app-grid-item';
      el.innerHTML = `${app.icon}<span>${app.name}</span>`;
      el.onclick = () => { Bridge.call('shell.launchApp', { name: app.name }); this.toggle(); };
      grid.appendChild(el);
    }
  },
};

// ── Clock ─────────────────────────────────────────────────────
function updateClock() {
  const now = new Date();
  const h = String(now.getHours()).padStart(2, '0');
  const m = String(now.getMinutes()).padStart(2, '0');
  const timeEl = document.getElementById('clock-time');
  const dateEl = document.getElementById('clock-date');
  if (timeEl) timeEl.textContent = `${h}:${m}`;
  if (dateEl) {
    const days = ['Sun','Mon','Tue','Wed','Thu','Fri','Sat'];
    const months = ['Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep','Oct','Nov','Dec'];
    dateEl.textContent = `${days[now.getDay()]} ${months[now.getMonth()]} ${now.getDate()}`;
  }
}

// ── Auto-resize textarea ──────────────────────────────────────
function autoResize(el) {
  el.style.height = 'auto';
  el.style.height = Math.min(el.scrollHeight, 120) + 'px';
}

// ── Event wiring ──────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => {
  // Top bar
  const startBtn = document.getElementById('start-button');
  const clockBtn = document.getElementById('clock-widget');
  const qsBtn = document.getElementById('quick-settings-button');
  const powerBtn = document.getElementById('power-button');
  if (startBtn) startBtn.onclick = () => AppGrid.toggle();
  if (clockBtn) clockBtn.onclick = () => QuickSettings.toggle();
  if (qsBtn) qsBtn.onclick = () => QuickSettings.toggle();
  if (powerBtn) powerBtn.onclick = () => PowerMenu.toggle();

  // Dock
  const orbTrigger = document.getElementById('orb-trigger');
  const gridTrigger = document.getElementById('app-grid-trigger');
  if (orbTrigger) orbTrigger.onclick = () => Assistant.toggle();
  if (gridTrigger) gridTrigger.onclick = () => AppGrid.toggle();

  // Assistant overlay
  const assistantClose = document.getElementById('assistant-close');
  const chatSend = document.getElementById('chat-send');
  const chatInput = document.getElementById('chat-input');
  if (assistantClose) assistantClose.onclick = () => Assistant.toggle();
  if (chatSend) chatSend.onclick = () => Assistant.send();
  if (chatInput) {
    chatInput.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); Assistant.send(); }
    });
    chatInput.addEventListener('input', () => autoResize(chatInput));
  }

  // Toggle tiles
  document.querySelectorAll('.qs-tile').forEach(tile => {
    tile.onclick = () => tile.classList.toggle('active');
  });

  // Power menu
  document.querySelectorAll('.power-item').forEach(item => {
    item.onclick = () => PowerMenu.action(item.dataset.action);
  });

  // Click outside to close panels
  document.getElementById('desktop')?.addEventListener('click', () => Panels.closeAll());

  // Clock
  updateClock();
  setInterval(updateClock, 1000);
});
