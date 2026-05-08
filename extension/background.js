const SESSION_PREFIX = 'proctorQuiz:';
const widgetRefreshBySession = new Map();
const EMBED_WIDGET_URL = 'https://example.com/';
const EMBED_WIDGET_TITLE = 'Embedded content';

/**
 * @param {number} tabId
 * @param {number} [frameId]
 */
function sessionKey(tabId, frameId = 0) {
  return `${SESSION_PREFIX}${tabId}-${frameId}`;
}

/**
 * @param {number} tabId
 */
async function injectMsnWidget(tabId) {
  await chrome.scripting.executeScript({
    target: { tabId },
    args: [EMBED_WIDGET_URL, EMBED_WIDGET_TITLE],
    func: function injectWidget(embedUrl, embedTitle) {
      const widgetId = 'd2l-lti-proctor-msn-widget';
      if (document.getElementById(widgetId)) {
        return;
      }

      const host = document.createElement('section');
      host.id = widgetId;
      host.setAttribute('aria-label', 'MSN widget');
      host.style.cssText = [
        'position:fixed',
        'top:12px',
        'right:12px',
        'width:min(420px,calc(100vw - 24px))',
        'height:min(320px,calc(100vh - 24px))',
        'z-index:2147483647',
        'border:1px solid #c8c8c8',
        'border-radius:10px',
        'overflow:hidden',
        'background:#ffffff',
        'box-shadow:0 8px 24px rgba(0,0,0,0.3)',
      ].join(';');

      const header = document.createElement('div');
      header.style.cssText = [
        'height:34px',
        'display:flex',
        'align-items:center',
        'justify-content:space-between',
        'padding:0 10px',
        'font-family:Arial,sans-serif',
        'font-size:12px',
        'font-weight:600',
        'background:#f3f3f3',
        'border-bottom:1px solid #d7d7d7',
      ].join(';');
      header.textContent = 'Quick widget';

      const frame = document.createElement('iframe');
      frame.src = embedUrl;
      frame.setAttribute('title', embedTitle);
      frame.setAttribute('loading', 'eager');
      frame.style.cssText = [
        'display:block',
        'width:100%',
        'height:calc(100% - 34px)',
        'border:0',
      ].join(';');

      host.appendChild(header);
      host.appendChild(frame);
      (document.body || document.documentElement).appendChild(host);
    },
  });
}

/**
 * @param {string} key
 */
function clearWidgetRefreshListener(key) {
  const existing = widgetRefreshBySession.get(key);
  if (!existing) return;
  chrome.tabs.onUpdated.removeListener(existing.onUpdated);
  widgetRefreshBySession.delete(key);
}

/**
 * @param {string} key
 * @param {number} tabId
 */
function ensureWidgetRefreshListener(key, tabId) {
  clearWidgetRefreshListener(key);

  const onUpdated = (updatedTabId, info) => {
    if (updatedTabId !== tabId || info.status !== 'complete') return;
    void injectMsnWidget(tabId).catch(() => {});
  };

  chrome.tabs.onUpdated.addListener(onUpdated);
  widgetRefreshBySession.set(key, { tabId, onUpdated });
}

/**
 * @param {number} tabId
 */
async function injectMsnWidgetWhenReady(tabId) {
  const maybeInject = async () => {
    try {
      await injectMsnWidget(tabId);
      return true;
    } catch {
      return false;
    }
  };

  if (await maybeInject()) {
    return;
  }

  return new Promise((resolve) => {
    let settled = false;
    const timeoutId = setTimeout(() => {
      if (settled) return;
      settled = true;
      chrome.tabs.onUpdated.removeListener(onUpdated);
      resolve();
    }, 12000);

    const onUpdated = async (updatedTabId, info) => {
      if (updatedTabId !== tabId || info.status !== 'complete' || settled) return;
      if (await maybeInject()) {
        settled = true;
        clearTimeout(timeoutId);
        chrome.tabs.onUpdated.removeListener(onUpdated);
        resolve();
      }
    };

    chrome.tabs.onUpdated.addListener(onUpdated);
  });
}

/**
 * @param {number} tabId
 */
async function injectPauseOverlay(tabId) {
  await chrome.scripting.executeScript({
    target: { tabId, allFrames: true },
    func: function inject() {
      const id = 'd2l-lti-proctor-pause-overlay';
      if (document.getElementById(id)) return;
      const d = document.createElement('div');
      d.id = id;
      d.setAttribute('aria-hidden', 'true');
      d.setAttribute('role', 'presentation');
      d.style.cssText = [
        'position:fixed',
        'inset:0',
        'z-index:2147483646',
        'background:rgba(0,0,0,0.45)',
        'pointer-events:auto',
        'user-select:none',
        '-webkit-user-select:none',
      ].join(';');
      (document.body || document.documentElement).appendChild(d);
    },
  });
}

/**
 * @param {number} tabId
 */
async function clearPauseOverlay(tabId) {
  try {
    await chrome.scripting.executeScript({
      target: { tabId, allFrames: true },
      func: function clearOvl() {
        const id = 'd2l-lti-proctor-pause-overlay';
        const n = document.getElementById(id);
        if (n && n.remove) n.remove();
      },
    });
  } catch {
    // some frames may be inaccessible; main frame usually still runs
  }
}

/**
 * @param {number} openerTabId
 * @param {number} frameId
 * @param {string} text
 * @param {boolean} [overlayVisible]
 */
function notifyLtiPage(openerTabId, frameId, text, overlayVisible) {
  const m = {
    type: 'proctorStatus',
    text,
    overlayVisible: Boolean(overlayVisible),
  };
  if (frameId > 0) {
    void chrome.tabs.sendMessage(openerTabId, m, { frameId }).catch(() => {});
  } else {
    void chrome.tabs.sendMessage(openerTabId, m).catch(() => {});
  }
}

chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
  if (!msg || msg.type !== 'proctor') {
    return undefined;
  }

  const tabId = sender.tab?.id;
  if (tabId == null) {
    sendResponse({ ok: false, error: 'no_sender_tab' });
    return false;
  }
  const frameId = typeof sender.frameId === 'number' ? sender.frameId : 0;
  const key = sessionKey(tabId, frameId);

  (async () => {
    if (msg.op === 'launch') {
      const testRunnerUrl = String(msg.testRunnerUrl || '').trim();
      if (!testRunnerUrl) {
        return { ok: false, error: 'missing_url' };
      }
      const existing = (await chrome.storage.session.get(key))[key];
      if (typeof existing === 'number') {
        clearWidgetRefreshListener(key);
        try {
          await chrome.tabs.remove(existing);
        } catch {
          // ignore
        }
      }
      const created = await chrome.tabs.create({ url: testRunnerUrl, active: true });
      if (created.id == null) {
        return { ok: false, error: 'create_failed' };
      }
      ensureWidgetRefreshListener(key, created.id);
      await injectMsnWidgetWhenReady(created.id);
      await chrome.storage.session.set({ [key]: created.id });
      notifyLtiPage(tabId, frameId, 'Running (quiz tab)', false);
      return { ok: true, quizTabId: created.id };
    }

    const store = (await chrome.storage.session.get(key))[key];
    const quizTabId = typeof store === 'number' ? store : null;
    if (quizTabId == null) {
      notifyLtiPage(tabId, frameId, 'No quiz tab — use Open quiz first', false);
      return { ok: false, error: 'no_quiz_tab' };
    }

    let quiz;
    try {
      quiz = await chrome.tabs.get(quizTabId);
    } catch {
      await chrome.storage.session.remove(key);
      notifyLtiPage(tabId, frameId, 'Quiz tab was closed', false);
      return { ok: false, error: 'quiz_tab_gone' };
    }

    if (msg.op === 'play') {
      await clearPauseOverlay(quizTabId);
      await chrome.tabs.update(quizTabId, { active: true });
      if (typeof quiz.windowId === 'number') {
        await chrome.windows.update(quiz.windowId, { focused: true });
      }
      notifyLtiPage(tabId, frameId, 'Running (quiz tab)', false);
      return { ok: true };
    }
    if (msg.op === 'pause') {
      await injectPauseOverlay(quizTabId);
      await chrome.tabs.update(quizTabId, { active: true });
      if (typeof quiz.windowId === 'number') {
        await chrome.windows.update(quiz.windowId, { focused: true });
      }
      notifyLtiPage(
        tabId,
        frameId,
        'Paused (interaction blocked in quiz tab)',
        true,
      );
      return { ok: true };
    }
    if (msg.op === 'stop') {
      try {
        await clearPauseOverlay(quizTabId);
        await chrome.tabs.remove(quizTabId);
      } catch {
        // may already be closed
      }
      clearWidgetRefreshListener(key);
      await chrome.storage.session.remove(key);
      notifyLtiPage(tabId, frameId, 'Stopped (quiz tab closed)', false);
      return { ok: true };
    }

    return { ok: false, error: 'unknown_op' };
  })()
    .then((r) => {
      try {
        sendResponse(r);
      } catch {
        // channel closed
      }
    })
    .catch((e) => {
      try {
        sendResponse({
          ok: false,
          error: e instanceof Error ? e.message : String(e),
        });
      } catch {
        // channel closed
      }
    });

  return true;
});

chrome.tabs.onRemoved.addListener(async (removedTabId) => {
  const all = await chrome.storage.session.get(null);
  for (const [k, v] of Object.entries(all)) {
    if (k.startsWith(SESSION_PREFIX) && v === removedTabId) {
      clearWidgetRefreshListener(k);
      const body = k.slice(SESSION_PREFIX.length);
      const li = body.lastIndexOf('-');
      if (li > 0) {
        const openerTab = Number.parseInt(body.slice(0, li), 10);
        const fId = Number.parseInt(body.slice(li + 1), 10) || 0;
        if (!Number.isNaN(openerTab)) {
          notifyLtiPage(
            openerTab,
            fId,
            'Quiz tab was closed',
            false,
          );
        }
      }
      await chrome.storage.session.remove(k);
    }
  }
});
