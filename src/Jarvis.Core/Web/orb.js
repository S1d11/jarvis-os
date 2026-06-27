// ═══════════════════════════════════════════════════════════════
// Jarvis Orb — Floating assistant logic
//
// Compact:  just the orb, floats anywhere, draggable
// Expanded: orb header + chat panel
//
// Click the orb to expand. Drag the orb to move it. Escape to collapse.
// ═══════════════════════════════════════════════════════════════

const Orb = {
  _states: ['idle', 'listening', 'thinking', 'responding'],
  _ready: false,
  _pendingSummon: false,
  _isExpanded: false,
  _isDragging: false,
  _dragStarted: false,
  _mouseDownPos: null,

  init() {
    const compact = document.getElementById('orb-compact');
    const expanded = document.getElementById('orb-expanded');

    // ── Compact orb: click to expand, drag to move ──────────────
    compact.addEventListener('mousedown', (e) => {
      this._mouseDownPos = { x: e.screenX, y: e.screenY };
      this._dragStarted = false;
      // Tell C# to start drag mode (WM_NCHITTEST → HTCAPTION)
      this._post({ action: 'orb.dragStart' });
    });

    compact.addEventListener('mouseup', (e) => {
      this._post({ action: 'orb.dragEnd' });
      // If the mouse didn't move much, treat as click → expand
      if (this._mouseDownPos && !this._dragStarted) {
        const dx = Math.abs(e.screenX - this._mouseDownPos.x);
        const dy = Math.abs(e.screenY - this._mouseDownPos.y);
        if (dx < 5 && dy < 5) {
          this.expand();
        }
      }
      this._mouseDownPos = null;
    });

    // Track mouse movement during drag
    compact.addEventListener('mousemove', (e) => {
      if (this._mouseDownPos) {
        const dx = Math.abs(e.screenX - this._mouseDownPos.x);
        const dy = Math.abs(e.screenY - this._mouseDownPos.y);
        if (dx > 5 || dy > 5) {
          this._dragStarted = true;
        }
      }
    });

    // ── Expanded header: drag to move the whole window ──────────
    const header = document.getElementById('orb-header');
    header.addEventListener('mousedown', (e) => {
      // Don't drag when clicking the close button
      if (e.target.closest('#orb-close')) return;
      this._mouseDownPos = { x: e.screenX, y: e.screenY };
      this._post({ action: 'orb.dragStart' });
    });
    header.addEventListener('mouseup', () => {
      this._post({ action: 'orb.dragEnd' });
      this._mouseDownPos = null;
    });

    // ── Close button (collapse) ─────────────────────────────────
    document.getElementById('orb-close').onclick = () => this.collapse();

    // ── Footer buttons ──────────────────────────────────────────
    document.getElementById('orb-full').onclick = () => this._post({ action: 'orb.openFull' });
    document.getElementById('orb-dismiss').onclick = () => this.dismiss();

    // ── Input ───────────────────────────────────────────────────
    const input = document.getElementById('orb-input');
    input.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); this.send(); }
      if (e.key === 'Escape') { this.collapse(); }
    });
    input.addEventListener('input', () => {
      input.style.height = 'auto';
      input.style.height = Math.min(input.scrollHeight, 80) + 'px';
    });

    // ── Send button ─────────────────────────────────────────────
    document.getElementById('orb-send').onclick = () => this.send();

    // ── Welcome message ─────────────────────────────────────────
    this._addMessage('jarvis', "Hi, I'm Jarvis. How can I help?");

    // ── Mark ready ──────────────────────────────────────────────
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
    if (this._isExpanded) { this.collapse(); return; }
    const compact = document.getElementById('orb-compact');
    compact.classList.add('dismissing');
    compact.classList.remove('summoned');
    setTimeout(() => {
      this._post({ action: 'orb.dismiss' });
    }, 300);
  },

  // ── Expand / Collapse ──────────────────────────────────────
  expand() {
    if (this._isExpanded) return;
    this._isExpanded = true;
    const compact = document.getElementById('orb-compact');
    const expanded = document.getElementById('orb-expanded');

    compact.classList.add('hidden');
    expanded.classList.add('active');
    // Force reflow then animate
    expanded.offsetHeight;
    expanded.classList.add('visible');

    this._post({ action: 'orb.expand' });
    this.setState('listening');

    setTimeout(() => {
      document.getElementById('orb-input').focus();
    }, 300);
  },

  collapse() {
    if (!this._isExpanded) return;
    this._isExpanded = false;
    const compact = document.getElementById('orb-compact');
    const expanded = document.getElementById('orb-expanded');

    expanded.classList.remove('visible');
    setTimeout(() => {
      expanded.classList.remove('active');
      compact.classList.remove('hidden');
    }, 250);

    this._post({ action: 'orb.collapse' });
  },

  // ── State ──────────────────────────────────────────────────
  setState(state) {
    const compact = document.getElementById('orb-compact');
    const label = document.getElementById('orb-state-label');
    this._states.forEach(s => compact.classList.remove(s));
    if (this._states.includes(state)) compact.classList.add(state);

    const labels = {
      idle: '',
      listening: 'Listening…',
      thinking: 'Thinking…',
      responding: 'Responding…',
    };
    if (label) label.textContent = labels[state] || '';
  },

  // ── Send message ───────────────────────────────────────────
  send() {
    const input = document.getElementById('orb-input');
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
    const feed = document.getElementById('orb-chat-feed');
    const div = document.createElement('div');
    div.className = `msg ${role}`;
    div.textContent = text;
    feed.appendChild(div);
    feed.scrollTop = feed.scrollHeight;
    return div;
  },

  _addTyping() {
    const feed = document.getElementById('orb-chat-feed');
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
      case 'summon':
        this.summon();
        break;
      case 'dismiss':
        this.dismiss();
        break;
      case 'expanded':
        // C# confirmed the window resized
        break;
      case 'collapsed':
        // C# confirmed the window resized
        break;
      case 'state':
        if (data && data.state) this.setState(data.state);
        break;
      case 'chat.response':
        // Remove typing indicator
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

// ── Boot ──────────────────────────────────────────────────────
document.addEventListener('DOMContentLoaded', () => Orb.init());

// ── Receive messages from C# ──────────────────────────────────
window.chrome?.webview?.addEventListener('message', (e) => {
  try {
    const msg = JSON.parse(e.data);
    if (msg.event) Orb.onEvent(msg.event, msg);
  } catch (err) { console.warn('parse error', err); }
});
