/* Progressive table presentation. No sorting, data changes, requests or form interception. */
(() => {
    'use strict';
    if (!document.body.classList.contains('workspace-theme')) return;
    const main = document.querySelector('main.content-area');
    if (!main) return;
    const hints = [];
    main.querySelectorAll('.table-responsive').forEach((region, index) => {
        const table = region.querySelector('table');
        if (!table) return;
        const columns = [...table.querySelectorAll('thead tr:first-child > th')];
        if (table.tHead?.rows.length === 1 && columns.every(cell => cell.colSpan === 1)) {
            columns.forEach((heading, column) => {
                const label = heading.textContent.trim().toLowerCase();
                const kind = /^(actions?|review)$/.test(label) ? 'table-cell-actions'
                    : /quantity|unit price|total|\bjod\b|weight|points|score/.test(label) ? 'is-numeric'
                    : /date|deadline|submitted|received|recorded|created|sent/.test(label) ? 'is-date' : null;
                if (!kind) return;
                heading.classList.add(kind);
                [...table.tBodies].flatMap(body => [...body.rows]).forEach(row => {
                    if (row.cells.length === columns.length && [...row.cells].every(cell => cell.colSpan === 1)) row.cells[column].classList.add(kind);
                });
            });
        }
        const label = region.getAttribute('aria-label') || table.caption?.textContent.trim() || 'Data table';
        if (!region.hasAttribute('role')) region.setAttribute('role', 'region');
        if (!region.hasAttribute('aria-label') && !region.hasAttribute('aria-labelledby')) region.setAttribute('aria-label', label);
        if (!region.hasAttribute('tabindex')) region.tabIndex = 0;
        const hint = document.createElement('p');
        hint.className = 'table-overflow-hint';
        hint.id = `table-overflow-hint-${index}`;
        hint.hidden = true;
        hint.textContent = 'Scroll horizontally to see all columns. Use arrow keys when the table is focused.';
        region.before(hint);
        const descriptions = region.getAttribute('aria-describedby');
        region.setAttribute('aria-describedby', [descriptions, hint.id].filter(Boolean).join(' '));
        hints.push({ region, hint });
    });
    const tones = {
        draft: 'neutral', read: 'neutral', closed: 'neutral', expired: 'neutral',
        submitted: 'info', underreview: 'info', underevaluation: 'info', awaitingmanagerreview: 'info', awaitingcommitteedecision: 'info', published: 'info',
        approved: 'positive', accepted: 'positive', enabled: 'positive', active: 'positive', open: 'positive', awarded: 'positive',
        pending: 'attention', returned: 'attention', unread: 'attention', locked: 'attention',
        rejected: 'negative', declined: 'negative', suspended: 'negative', disabled: 'negative'
    };
    main.querySelectorAll('.status-pill,.bid-status').forEach(badge => {
        const key = badge.textContent.trim().replace(/\s+/g, '').toLowerCase();
        if (tones[key]) badge.dataset.uiTone = tones[key];
    });
    const update = () => hints.forEach(({ region, hint }) => { hint.hidden = region.scrollWidth <= region.clientWidth + 1; });
    if ('ResizeObserver' in window) {
        const observer = new ResizeObserver(update);
        hints.forEach(({ region }) => { observer.observe(region); observer.observe(region.querySelector('table')); });
    } else window.addEventListener('resize', update, { passive: true });
    update();
})();
