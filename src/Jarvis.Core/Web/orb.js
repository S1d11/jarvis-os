// ═══════════════════════════════════════════════════════════════
// Jarvis Orb — Siri-like floating overlay logic
// ═══════════════════════════════════════════════════════════════

const Orb = {
  _states: ['idle', 'listening', 'thinking', 'responding'],

  init() {
    const overlay = document.getElementById('orb-overlay');

    // Tell C# we're ready
    this._post({ action: 'orb.ready' });

    // Button handlers
    document.getElementById('orb-send').onclick = () => this.send();
    document.getElementById('orb-dismiss').onclick = () => this._post({ action: 'orb.dismiss' });
    document.getElementById('orb-full').onclick = () => this._post({ action: 'orb.openFull' });

    // Input
    const input = document.getElementById('orb-input');
    input.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); this.send(); }
      if (e.key === 'Escape') { this._post({ action: 'orb.dismiss' }); }
    });
    input.addEventListener('input', () => {
      input.style.height = 'auto';
      input.style.height = Math.min(input.scrollHeight, 80) + 'px';
    });

    // Welcome message
    this._addMessage('jarvis', "Hi, I'm Jarvis. How can I help?");
  },

  // ── Summon / Dismiss animations ─────────────────────────────
  summon() {
    const overlay = document.getElementById('orb-overlay');
    overlay.classList.remove('dismissing');
    overlay.classList.add('summoned');
    this.setState('listening');
    setTimeout(() => {
      document.getElementById('orb-input').focus();
    }, 600);
  },

  dismiss() {
    const overlay = document.getElementById('orb-overlay');
    overlay.classList.remove('summoned');
    overlay.classList.add('dismissing');
  },

  // ── Orb state ───────────────────────────────────────────────
  setState(state) {
    const overlay = document.getElementById('orb-overlay');
    for (const s of this._states) overlay.classList.remove(s);
    if (state !== 'idle') overlay.classList.add(state);

    const label = document.getElementById('orb-state-label');
    label.textContent = {
      idle: '',
      listening: 'Listening…',
      thinking: 'Thinking…',
      responding: 'Responding…',
    }[state] || '';
  },

  // ── Chat ────────────────────────────────────────────────────
  async send() {
    const input = document.getElementById('orb-input');
    const text = input.value.trim();
    if (!text) return;

    this._addMessage('user', text);
    input.value = '';
    input.style.height = 'auto';

    this.setState('thinking');
    const typing = this._addTyping();

    try {
      const response = await this._getAIResponse(text);
      typing.remove();
      this.setState('responding');
      this._addMessage('jarvis', response);

      // Return to listening after a short delay
      setTimeout(() => this.setState('listening'), 1500);
    } catch (err) {
      typing.remove();
      this._addMessage('jarvis', `Error: ${err.message}`);
      this.setState('listening');
    }
  },

  async _getAIResponse(text) {
    const lower = text.toLowerCase();

    if (lower.startsWith('run ') || lower.startsWith('ps ')) {
      const cmd = text.substring(text.indexOf(' ') + 1);
      const result = await this._call('sys.powershell', { command: cmd });
      let out = result.stdout || '';
      if (result.stderr) out += (out ? '\n' : '') + result.stderr;
      return `Exit: ${result.exitCode}\n\n\`\`\`\n${out.trim() || '(no output)'}\n\`\`\``;
    }
    if (lower.startsWith('cmd ')) {
      const cmd = text.substring(4);
      const result = await this._call('sys.cmd', { command: cmd });
      let out = result.stdout || '';
      if (result.stderr) out += (out ? '\n' : '') + result.stderr;
      return `Exit: ${result.exitCode}\n\n\`\`\`\n${out.trim() || '(no output)'}\n\`\`\``;
    }

    // Quick responses for common requests
    if (lower.match(/open|launch|start/)) {
      const app = text.replace(/.*?(open|launch|start)\s*/i, '').trim();
      if (app) {
        await this._call('shell.launchApp', { name: app });
        return `Opening ${app}…`;
      }
    }
    if (lower.match(/lock/)) { await this._call('shell.lockScreen'); return 'Locking your screen.'; }
    if (lower.match(/shut.?down/)) { await this._call('shell.shutdown'); return 'Shutting down.'; }
    if (lower.match(/restart|reboot/)) { await this._call('shell.restart'); return 'Restarting.'; }
    if (lower.match(/sleep/)) { await this._call('shell.sleep'); return 'Going to sleep.'; }

    return `You said: "${text}". AI backend not yet connected.`;
  },

  _addMessage(role, text) {
    const feed = document.getElementById('orb-chat-feed');
    const el = document.createElement('div');
    el.className = `msg ${role}`;
    el.textContent = text;
    feed.appendChild(el);
    feed.scrollTop = feed.scrollHeight;
    return el;
  },

  _addTyping() {
    const feed = document.getElementById('orb-chat-feed');
    const el = document.createElement('div');
    el.className = 'msg jarvis';
    el.innerHTML = '<div class="typing-dots"><span></span><span></span><span></span></div>';
    feed.appendChild(el);
    feed.scrollTop = feed.scrollHeight;
    return el;
  },

  // ── Bridge ──────────────────────────────────────────────────
  _pending: new Map(),

  async _call(action, payload = {}) {
    const id = `orb_${Date.now()}_${Math.random().toString(36).slice(2, 6)}`;
    return new Promise((resolve, reject) => {
      this._pending.set(id, { resolve, reject });
      this._post({ id, action, ...payload });
      setTimeout(() => {
        if (this._pending.has(id)) {
          this._pending.get(id).reject(new Error('Timeout'));
          this._pending.delete(id);
        }
      }, 30000);
    });
  },

  _post(msg) {
    window.chrome.webview.postMessage(JSON.stringify(msg));
  },

  _receive(json) {
    const msg = JSON.parse(json);
    if (msg.id && this._pending.has(msg.id)) {
      const { resolve, reject } = this._pending.get(msg.id);
      this._pending.delete(msg.id);
      msg.ok ? resolve(msg.data) : reject(new Error(msg.error));
    } else if (msg.event) {
      switch (msg.event) {
        case 'summon': this.summon(); break;
        case 'dismiss': this.dismiss(); break;
        case 'state': this.setState(msg.state); break;
      }
    }
  },
};

window.Orb = Orb;
window.Bridge = { _receive: (json) => Orb._receive(json) };

document.addEventListener('DOMContentLoaded', () => Orb.init());
