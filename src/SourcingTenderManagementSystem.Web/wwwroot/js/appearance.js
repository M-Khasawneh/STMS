/* Appearance is a local preference, never a permission or an account setting. */
(() => {
    'use strict';
    const root = document.documentElement;
    const system = matchMedia('(prefers-color-scheme: dark)');
    const apply = theme => {
        root.dataset.theme = theme;
        root.style.colorScheme = theme;
        document.querySelector('meta[name="theme-color"]')?.setAttribute('content', theme === 'dark' ? '#0b1521' : '#f6f3ed');
        document.querySelectorAll('[data-appearance-toggle]').forEach(button => {
            button.hidden = false;
            button.setAttribute('aria-pressed', String(theme === 'dark'));
            button.setAttribute('aria-label', theme === 'dark' ? 'Switch to light mode' : 'Switch to dark mode');
            button.title = theme === 'dark' ? 'Light mode' : 'Dark mode';
        });
        // Keep popup-first MVC forms in the same appearance as their host page.
        document.querySelectorAll('iframe').forEach(frame => {
            try { const frameRoot = frame.contentDocument?.documentElement; if (frameRoot) { frameRoot.dataset.theme = theme; frameRoot.style.colorScheme = theme; } } catch { /* Same-origin frames only. */ }
        });
    };
    document.querySelectorAll('[data-appearance-toggle]').forEach(button => button.addEventListener('click', () => {
        const theme = root.dataset.theme === 'dark' ? 'light' : 'dark';
        try { localStorage.setItem('stms-appearance', theme); } catch { /* The current page still changes. */ }
        apply(theme);
    }));
    window.addEventListener('storage', event => {
        if (event.key === 'stms-appearance') apply(event.newValue === 'light' || event.newValue === 'dark' ? event.newValue : system.matches ? 'dark' : 'light');
    });
    system.addEventListener('change', () => {
        let saved;
        try { saved = localStorage.getItem('stms-appearance'); } catch { /* Use system preference. */ }
        if (saved !== 'light' && saved !== 'dark') apply(system.matches ? 'dark' : 'light');
    });
    apply(root.dataset.theme || 'light');
})();
