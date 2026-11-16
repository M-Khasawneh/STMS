/* Runs before CSS: no network, no framework and no first-paint theme flash. */
(() => {
    'use strict';
    let saved;
    try { saved = localStorage.getItem('stms-appearance'); } catch { /* Storage is optional. */ }
    const theme = saved === 'light' || saved === 'dark' ? saved : (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light');
    document.documentElement.dataset.theme = theme;
    document.documentElement.style.colorScheme = theme;
})();
