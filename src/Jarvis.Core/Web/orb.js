// ═══════════════════════════════════════════════════════════════
// Jarvis Orb — Floating orb + Fullscreen chatbot
//
// Compact:  80x80 floating orb, draggable, click to go fullscreen
// Fullscreen: Full Jarvis chatbot with text + voice input
// ═══════════════════════════════════════════════════════════════

const Orb = {
  _states: ['idle', 'listening', 'thinking', 'responding'],
  _ready: false,
  _pendingSummon: false,
  _isFullscreen: false,
  _inputMode: 'text',     // 'text' or 'voice'
  _mouseDownPos: null,
  _dragStarted: false,

  init() {
    const compact = document.getElementById('orb-compact');

    // ── Compact orb: click to go fullscreen, drag to move ───────
    compact.addEventListener('mousedown', (e) => {
      this._mouseDownPos = { x: e.screenX, y: e.screenY };
      this._dragStarted = false;
      this._post({ action: 'orb.dragStart' });
    });

    compact.addEventListener('mousemove', (e) => {
      if (this._mouseDownPos) {
        const dx = Math.abs(e.screenX - this._mouseDownPos.x);
        const dy = Math.abs(e.screenY - this._mouseDownPos.y);
        if (dx > 5 || dy > 5) this._dragStarted = true;
      }
    });

    compact.addEventListener('mouseup', (e) => {
      this._post({ action: 'orb.dragEnd' });
      if (this._mouseDownPos && !this._dragStarted) {
        const dx = Math.abs(e.screenX - this._mouseDownPos.x);
        const dy = Math.abs(e.screenY - this._mouseDownPos.y);
        if (dx < 5 && dy < 5) this.expand();
      }
      this._mouseDownPos = null;
    });

    // ── Fullscreen: close button ────────────────────────────────
    document.getElementById('fs-close').onclick = () => this.collapse();

    // ── Fullscreen: text input ──────────────────────────────────
    const input = document.getElementById('fs-input');
    input.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); this.send(); }
      if (e.key === 'Escape') { this.collapse(); }
    });
    input.addEventListener('input', () => {
      input.style.height = 'auto';
      input.style.height = Math.min(input.scrollHeight, 120) + 'px';
    });

    document.getElementById('fs-send').onclick = () => this.send();

    // ── Mode toggle: Voice / Type ───────────────────────────────
    document.getElementById('fs-voice-btn').onclick = () => this.setMode('voice');
    document.getElementById('fs-text-btn').onclick = () => this.setMode('text');

    // ── Welcome message ─────────────────────────────────────────
    this._addMessage('jarvis', "Hi, I'm Jarvis. How can I help?");

    // ── Ready ───────────────────────────────────────────────────
    this._ready = true;
    this._post({ action: 'orb.ready' });
    if (this._pendingSummon) {
      this._pendingSummon = false;
      this.summon();
    }
  },

  // ── Summon / Dismiss ───────────────────────────────────────
  summon() {
    if (!this._ready) { this._pendingSummon = true; return; }
    const compact = document.getElementById('orb-compact');
    compact.classList.remove('dismissing');
    compact.classList.add('summoned');
    this.setState('listening');
  },

  dismiss() {
    if (this._isFullscreen) { this.collapse(); return; }
    const compact = document.getElementById('orb-compact');
    compact.classList.add('dismissing');
    compact.classList.remove('summoned');
    setTimeout(() => this._post({ action: 'orb.dismiss' }), 300);
  },

  // ── Expand to fullscreen / Collapse to orb ─────────────────
  expand() {
    if (this._isFullscreen) return;
    this._isFullscreen = true;
    const compact = document.getElementById('orb-compact');
    const fs = document.getElementById('jarvis-fullscreen');

    compact.classList.add('hidden');
    fs.classList.add('active');
    fs.offsetHeight; // force reflow
    fs.classList.add('visible');

    this._post({ action: 'orb.expand' });
    this.setState('listening');

    setTimeout(() => {
      if (this._inputMode === 'text') {
        document.getElementById('fs-input').focus();
      }
    }, 350);
  },

  collapse() {
    if (!this._isFullscreen) return;
    this._isFullscreen = false;
    const compact = document.getElementById('orb-compact');
    const fs = document.getElementById('jarvis-fullscreen');

    fs.classList.remove('visible');
    setTimeout(() => {
      fs.classList.remove('active');
      compact.classList.remove('hidden');
    }, 250);

    this._post({ action: 'orb.collapse' });
  },

  // ── Input mode: text or voice ──────────────────────────────
  setMode(mode) {
    this._inputMode = mode;
    const voiceBtn = document.getElementById('fs-voice-btn');
    const textBtn = document.getElementById('fs-text-btn');
    const input = document.getElementById('fs-input');
    const sendBtn = document.getElementById('fs-send');
    const wrap = document.getElementById('fs-input-wrap');

    if (mode === 'voice') {
      voiceBtn.classList.add('active');
      textBtn.classList.remove('active');
      input.placeholder = 'Voice mode — speak to Jarvis…';
      input.disabled = true;
      sendBtn.style.opacity = '0.4';
      sendBtn.style.pointerEvents = 'none';
      this._post({ action: 'voice.start' });
    } else {
      textBtn.classList.add('active');
      voiceBtn.classList.remove('active');
      input.placeholder = 'Type to Jarvis…';
      input.disabled = false;
      sendBtn.style.opacity = '1';
      sendBtn.style.pointerEvents = 'auto';
      input.focus();
      this._post({ action: 'voice.stop' });
    }
  },

  // ── State ──────────────────────────────────────────────────
  setState(state) {
    const compact = document.getElementById('orb-compact');
    const label = document.getElementById('fs-state-label');
    this._states.forEach(s => compact.classList.remove(s));
    if (this._states.includes(state)) compact.classList.add(state);

    const labels = {
      idle: '', listening: 'Listening…', thinking: 'Thinking…', responding: 'Responding…',
    };
    if (label) label.textContent = labels[state] || '';
  },

  // ── Send message ───────────────────────────────────────────
  send() {
    const input = document.getElementById('fs-input');
    const text = input.value.trim();
    if (!text) return;

    this._addMessage('user', text);
    input.value = '';
    input.style.height = 'auto';

    this._addTyping();
    this.setState('thinking');
    this._post({ action: 'chat.send', message: text });
  },

  // ── Message helpers ────────────────────────────────────────
  _addMessage(role, text) {
    const feed = document.getElementById('fs-chat-feed');
    const div = document.createElement('div');
    div.className = `msg ${role}`;
    div.textContent = text;
    feed.appendChild(div);
    feed.scrollTop = feed.scrollHeight;
    return div;
  },

  _addTyping() {
    const feed = document.getElementById('fs-chat-feed');
    const div = document.createElement('div');
    div.className = 'msg jarvis typing-indicator';
    div.innerHTML = '<div class="typing-dots"><span></span><span></span><span></span></div>';
    feed.appendChild(div);
    feed.scrollTop = feed.scrollHeight;
    return div;
  },

  // ── Bridge ─────────────────────────────────────────────────
  _post(msg) {
    try { window.chrome.webview.postMessage(JSON.stringify(msg)); }
    catch (e) { console.warn('bridge not ready', e); }
  },

  // ── Incoming events from C# ────────────────────────────────
  onEvent(event, data) {
    switch (event) {
      case 'summon': this.summon(); break;
      case 'dismiss': this.dismiss(); break;
      case 'expanded': break;  // C# confirmed fullscreen resize
      case 'collapsed': break; // C# confirmed compact resize
      case 'state':
        if (data && data.state) this.setState(data.state);
        break;
      case 'chat.response':
        const typing = document.querySelector('.typing-indicator');
        if (typing) typing.remove();
        this._addMessage('jarvis', data?.text || data?.message || '');
        this.setState('responding');
        setTimeout(() => this.setState('listening'), 1500);
        break;
      case 'chat.error':
        const t = document.querySelector('.typing-indicator');
        if (t) t.remove();
        this._addMessage('jarvis', `Error: ${data?.message || 'something went wrong'}`);
        this.setState('idle');
        break;
    }
  },
};

document.addEventListener('DOMContentLoaded', () => Orb.init());

window.chrome?.webview?.addEventListener('message', (e) => {
  try {
    const msg = JSON.parse(e.data);
    if (msg.event) Orb.onEvent(msg.event, msg);
  } catch (err) { console.warn('parse error', err); }
});
