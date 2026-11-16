/* Enlarges the existing protected QR endpoint. No new URLs, permissions or external services. */
(() => {
    'use strict';
    if (!window.HTMLDialogElement || !document.querySelector('[data-qr-preview]')) return;
    const dialog = document.createElement('dialog');
    dialog.className = 'qr-preview'; dialog.setAttribute('aria-labelledby', 'qr-preview-title');
    const header = document.createElement('header'), title = document.createElement('h2'), close = document.createElement('button');
    title.id = 'qr-preview-title'; title.textContent = 'Scan this record';
    close.type = 'button'; close.className = 'qr-preview-close'; close.textContent = '×'; close.setAttribute('aria-label', 'Close QR preview');
    header.append(title, close);
    const description = document.createElement('p'); description.textContent = 'Sign-in and the existing record permissions are required.';
    const image = document.createElement('img'); image.className = 'qr-preview-image'; image.alt = 'Enlarged QR code for this record';
    const status = document.createElement('p'); status.setAttribute('role', 'status');
    const download = document.createElement('a'); download.className = 'btn btn-outline-secondary'; download.textContent = 'Download QR code'; download.setAttribute('download', '');
    dialog.append(header, description, image, status, download); document.body.append(dialog);
    let trigger;
    const dismiss = () => dialog.close();
    close.addEventListener('click', dismiss);
    dialog.addEventListener('click', event => {
        const r = dialog.getBoundingClientRect();
        if (event.target === dialog && (event.clientX < r.left || event.clientX > r.right || event.clientY < r.top || event.clientY > r.bottom)) dismiss();
    });
    dialog.addEventListener('close', () => { image.removeAttribute('src'); trigger?.focus({preventScroll:true}); });
    image.addEventListener('load', () => { status.textContent = ''; image.hidden = false; });
    image.addEventListener('error', () => { image.hidden = true; status.textContent = 'The QR code could not be loaded. Close this preview and try again.'; });
    document.querySelectorAll('[data-qr-preview]').forEach(link => link.addEventListener('click', event => {
        if (event.button || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        const url = new URL(link.href, location.href);
        if (url.origin !== location.origin) return;
        event.preventDefault(); trigger = link; image.hidden = true;
        status.textContent = 'Loading QR code…'; download.href = url.href; image.src = url.href;
        if (!dialog.open) dialog.showModal(); close.focus();
    }));
})();
