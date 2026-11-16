/* Existing headings/files only. No requests, file reads, validation replacement or form interception. */
(() => {
    'use strict';
    const root = document.querySelector('[data-record-navigation="true"]');
    if (root && !root.querySelector('.record-section-nav')) {
        const headings = [...root.querySelectorAll('h2')].filter(heading => !heading.closest('[hidden],form,dialog') && heading.getClientRects().length);
        if (headings.length >= 3) {
            const nav = document.createElement('nav'); nav.className = 'record-section-nav'; nav.setAttribute('aria-label', 'On this page');
            const label = document.createElement('span'); label.textContent = 'ON THIS PAGE'; nav.append(label);
            const list = document.createElement('ul'); nav.append(list);
            headings.forEach((heading, index) => {
                if (!heading.id) {
                    let id = `record-section-${index}`;
                    while (document.getElementById(id)) id += '-heading';
                    heading.id = id;
                }
                if (!heading.hasAttribute('tabindex')) heading.tabIndex = -1;
                const item = document.createElement('li'), link = document.createElement('a');
                link.href = `#${heading.id}`; link.textContent = heading.textContent.trim(); link.title = link.textContent;
                link.addEventListener('click', () => requestAnimationFrame(() => heading.focus({ preventScroll: true })));
                item.append(link); list.append(item);
            });
            const header = root.querySelector('.page-heading,.work-heading');
            if (header) header.after(nav); else headings[0].before(nav);
        }
    }
    document.querySelectorAll('main input[type="file"]').forEach((input, index) => {
        if (input.dataset.selectionGuidanceReady === 'true') return;
        input.dataset.selectionGuidanceReady = 'true';
        const summary = document.createElement('div'); summary.className = 'upload-selection'; summary.hidden = true;
        summary.id = `upload-selection-${index}`;
        const status = document.createElement('div'); status.setAttribute('role', 'status'); status.setAttribute('aria-live', 'polite'); status.setAttribute('aria-atomic', 'true');
        const list = document.createElement('ul'), note = document.createElement('p');
        note.textContent = 'Selection only. Files are checked when you submit; nothing has been uploaded yet.';
        status.append(list, note);
        const clear = document.createElement('button'); clear.type = 'button'; clear.textContent = 'Clear selection';
        const name = input.labels?.[0]?.textContent.trim();
        if (name) clear.setAttribute('aria-label', `Clear selection: ${name}`);
        summary.append(status, clear); input.after(summary);
        input.setAttribute('aria-describedby', [input.getAttribute('aria-describedby'), summary.id].filter(Boolean).join(' '));
        const update = () => {
            list.replaceChildren(); const files = [...(input.files || [])]; summary.hidden = files.length === 0;
            files.forEach(file => {
                const item = document.createElement('li'), title = document.createElement('span'), size = document.createElement('span');
                title.textContent = file.name;
                size.textContent = file.size >= 1048576 ? `${(file.size / 1048576).toFixed(2)} MB` : `${(file.size / 1024).toFixed(1)} KB`;
                item.append(title, size); list.append(item);
            });
        };
        input.addEventListener('change', update);
        clear.addEventListener('click', () => {
            input.value = ''; input.dispatchEvent(new Event('input', { bubbles: true })); input.dispatchEvent(new Event('change', { bubbles: true })); input.focus();
        });
        update();
    });
})();
