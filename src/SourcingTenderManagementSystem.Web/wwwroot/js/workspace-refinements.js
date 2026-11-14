(() => {
    document.querySelectorAll('.profile-picture').forEach(image => image.addEventListener('error', () => image.remove(), { once: true }));
    document.querySelectorAll('.table-responsive').forEach(region => {
        if (!region.hasAttribute('tabindex')) region.tabIndex = 0;
        if (!region.hasAttribute('role')) region.setAttribute('role', 'region');
        if (!region.hasAttribute('aria-label') && !region.hasAttribute('aria-labelledby')) region.setAttribute('aria-label', 'Scrollable data table');
    });
    // Keep the real current stage in view without moving the page or changing workflow state.
    document.querySelectorAll('.status-track-viewport, .requisition-history[aria-label="Change history"]').forEach(viewport => {
        const current = viewport.querySelector('[aria-current="step"]');
        if (current && viewport.scrollWidth > viewport.clientWidth) {
            viewport.scrollLeft += current.getBoundingClientRect().left - viewport.getBoundingClientRect().left - (viewport.clientWidth - current.clientWidth) / 2;
        }
    });
    const feedbackKey = 'stms-feedback-once';
    const owner = document.querySelector('.account-name')?.textContent.trim() || '';
    window.workspaceFeedback = {
        remember(message) {
            try { sessionStorage.setItem(feedbackKey, JSON.stringify({ message, owner, at: Date.now() })); } catch { /* Inline feedback remains available. */ }
        }
    };
    try {
        const saved = JSON.parse(sessionStorage.getItem(feedbackKey) || 'null');
        sessionStorage.removeItem(feedbackKey);
        if (saved && saved.owner === owner && Date.now() - saved.at < 20000 && typeof saved.message === 'string' && !document.querySelector('main .alert-success')) {
            const alert = document.createElement('div');
            alert.className = 'alert alert-success'; alert.setAttribute('role', 'status'); alert.textContent = saved.message;
            document.querySelector('main')?.prepend(alert);
        }
    } catch { /* Storage is optional. */ }
    const alerts = Array.from(document.querySelectorAll('main .alert-success'));
    if (!alerts.length) return;
    const message = alerts.map(alert => alert.textContent.trim()).filter(Boolean).join(' ');
    if (!message) return;
    // Retain the existing accessible inline messages and modal completion signals.
    const toast = document.createElement('div');
    toast.className = 'feedback-toast';
    toast.setAttribute('aria-hidden', 'true');
    toast.textContent = message;
    document.body.append(toast);
    window.setTimeout(() => toast.remove(), 7000);
})();
