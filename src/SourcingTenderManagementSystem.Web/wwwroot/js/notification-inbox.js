(() => {
    'use strict';
    const dropdown = document.querySelector('.notification-dropdown');
    if (!dropdown) return;
    const trigger = dropdown.querySelector('summary');
    const close = () => { dropdown.open = false; };
    document.addEventListener('click', event => { if (!dropdown.contains(event.target)) close(); });
    dropdown.addEventListener('keydown', event => {
        if (event.key === 'Escape' && dropdown.open) { event.preventDefault(); event.stopPropagation(); close(); trigger.focus(); }
    });
    dropdown.addEventListener('focusout', event => { if (event.relatedTarget && !dropdown.contains(event.relatedTarget)) close(); });
    let pending = false, refreshing = false, revision = 0, source = null, timer = null, stopped = false;
    function apply(html, force = false) {
        const template = document.createElement('template'); template.innerHTML = html;
        const next = template.content.querySelector('.notification-menu-content');
        if (!next) throw Error('Notification response unavailable');
        const unread = Number(next.dataset.unread);
        const badge = trigger.querySelector('.notification-count');
        // Unknown is never represented as a zero count.
        if (next.dataset.unread !== '' && Number.isFinite(unread)) {
            badge.hidden = unread <= 0; badge.textContent = unread > 99 ? '99+' : String(unread);
            trigger.setAttribute('aria-label', unread > 0 ? `Notifications, ${unread} unread` : 'Notifications');
        }
        const old = dropdown.querySelector('.notification-menu-content');
        // A background refresh must not remove the link/button a keyboard user is using.
        if (force || !old.contains(document.activeElement)) old.replaceWith(next);
        return dropdown.querySelector('.notification-menu-content');
    }
    function safeUrl(value) {
        if (!value) return null;
        const url = new URL(value, location.href);
        return url.origin === location.origin ? url : null;
    }
    async function refresh() {
        const url = safeUrl(dropdown.dataset.menuUrl);
        if (!url || pending || refreshing || document.hidden || stopped) return;
        refreshing = true; const current = revision;
        url.searchParams.set('returnUrl', location.pathname + location.search);
        try {
            const response = await fetch(url, { credentials: 'same-origin', cache: 'no-store', headers: { 'X-Requested-With': 'XMLHttpRequest' }, signal: AbortSignal.timeout(12000) });
            if (response.status === 401 || response.status === 403) { stop(); return; }
            if (!response.ok || response.headers.get('X-STMS-Notification-Menu') !== '1') return;
            const html = await response.text(); if (!pending && current === revision) apply(html);
        } catch { /* Keep the existing inbox usable when the live channel is unavailable. */ }
        finally { refreshing = false; }
    }
    function poll() {
        clearTimeout(timer);
        if (document.hidden || stopped) return;
        timer = setTimeout(async () => { await refresh(); poll(); }, 30000);
    }
    function stop() { stopped = true; source?.close(); source = null; clearTimeout(timer); }
    function connect() {
        if (document.hidden || stopped || source) return;
        const url = safeUrl(dropdown.dataset.watchUrl);
        if (!url || !window.EventSource) { poll(); return; }
        source = new EventSource(url, { withCredentials: true });
        source.addEventListener('changed', refresh);
        source.addEventListener('reconnect', () => { source?.close(); source = null; timer = setTimeout(connect, 2000); });
        source.addEventListener('revoked', stop);
        source.addEventListener('unavailable', () => { source?.close(); source = null; poll(); });
        source.onerror = () => { source?.close(); source = null; poll(); };
    }
    dropdown.addEventListener('toggle', () => { if (dropdown.open) refresh(); });
    document.addEventListener('visibilitychange', () => {
        if (document.hidden) { source?.close(); source = null; clearTimeout(timer); }
        else { refresh(); connect(); }
    });
    window.addEventListener('pagehide', () => { source?.close(); source = null; clearTimeout(timer); });
    window.addEventListener('pageshow', () => { if (!stopped) connect(); });
    dropdown.addEventListener('submit', async event => {
        const form = event.target.closest('form[data-notification-read]');
        if (!form || !window.fetch) return;
        event.preventDefault(); if (pending) return;
        pending = true; revision++;
        const button = form.querySelector('button'); button.disabled = true;
        try {
            const response = await fetch(form.action, { method: 'POST', body: new FormData(form), credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            if (!response.ok || response.headers.get('X-STMS-Notification-Menu') !== '1') throw Error('Notification update failed');
            const next = apply(await response.text(), true);
            const feedback = next.querySelector('.notification-menu-error');
            feedback.hidden = false; feedback.textContent = 'Notification marked as read.'; feedback.setAttribute('role', 'status');
            (next.querySelector('button, a') || trigger).focus();
        } catch {
            const error = dropdown.querySelector('.notification-menu-error');
            error.hidden = false; error.textContent = 'Could not update this notification. Try again or open all notifications.';
            button.disabled = false;
        } finally { pending = false; }
    });
    connect();
})();
