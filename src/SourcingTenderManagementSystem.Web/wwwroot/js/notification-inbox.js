(() => {
    const dropdown = document.querySelector('.notification-dropdown');
    if (!dropdown) return;
    const trigger = dropdown.querySelector('summary');
    const close = () => { dropdown.open = false; };
    document.addEventListener('click', event => { if (!dropdown.contains(event.target)) close(); });
    dropdown.addEventListener('keydown', event => {
        if (event.key === 'Escape' && dropdown.open) { event.preventDefault(); event.stopPropagation(); close(); trigger.focus(); }
    });
    dropdown.addEventListener('focusout', event => { if (event.relatedTarget && !dropdown.contains(event.relatedTarget)) close(); });
    let pending = false;
    dropdown.addEventListener('submit', async event => {
        const form = event.target.closest('form[data-notification-read]');
        if (!form || !window.fetch) return;
        event.preventDefault();
        if (pending) return;
        pending = true;
        const button = form.querySelector('button'); button.disabled = true;
        try {
            const response = await fetch(form.action, { method: 'POST', body: new FormData(form), credentials: 'same-origin', headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            if (!response.ok || response.headers.get('X-STMS-Notification-Menu') !== '1') throw Error('Notification update failed');
            const template = document.createElement('template'); template.innerHTML = await response.text();
            const next = template.content.querySelector('.notification-menu-content');
            if (!next) throw Error('Notification response unavailable');
            dropdown.querySelector('.notification-menu-content').replaceWith(next);
            const unread = Number(next.dataset.unread);
            const badge = trigger.querySelector('.notification-count');
            badge.hidden = !Number.isFinite(unread) || unread <= 0;
            badge.textContent = unread > 99 ? '99+' : String(unread);
            trigger.setAttribute('aria-label', unread > 0 ? `Notifications, ${unread} unread` : 'Notifications');
            const feedback = next.querySelector('.notification-menu-error');
            feedback.hidden = false; feedback.textContent = 'Notification marked as read.'; feedback.setAttribute('role', 'status');
            // Restore keyboard focus after the submitted form is replaced.
            const target = next.querySelector('button, a'); (target || trigger).focus();
        } catch {
            const error = dropdown.querySelector('.notification-menu-error');
            error.hidden = false; error.textContent = 'Could not update this notification. Try again or open all notifications.';
            button.disabled = false;
        } finally { pending = false; }
    });
})();
