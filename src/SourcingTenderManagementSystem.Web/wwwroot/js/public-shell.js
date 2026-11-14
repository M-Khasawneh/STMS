(() => {
    'use strict';
    const toggle = document.querySelector('.password-toggle');
    const field = document.getElementById(toggle?.getAttribute('aria-controls'));
    if (toggle && field) {
        toggle.hidden = false;
        toggle.addEventListener('click', () => {
            const show = field.type === 'password';
            field.type = show ? 'text' : 'password';
            toggle.textContent = show ? 'Hide' : 'Show';
            toggle.setAttribute('aria-pressed', String(show));
        });
        // Never retain the revealed state in a browser-history snapshot.
        window.addEventListener('pagehide', () => { field.type = 'password'; toggle.textContent = 'Show'; toggle.setAttribute('aria-pressed', 'false'); });
    }
})();
