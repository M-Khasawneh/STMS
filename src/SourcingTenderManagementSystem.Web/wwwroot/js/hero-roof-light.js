/* One native SVG highlight follows the exact roof coordinates of the existing artwork. */
(() => {
    'use strict';
    const path = document.querySelector('.ref-roof-light'), toggle = document.querySelector('[data-hero-light-toggle]');
    if (!path?.animate || !toggle) return;
    const reduced = matchMedia('(prefers-reduced-motion: reduce)');
    const motion = path.animate([{strokeDashoffset:'100'},{strokeDashoffset:'0'}], {duration:6500,iterations:Infinity,easing:'linear'});
    motion.pause(); let visible = true, paused = false, onPage = true;
    function sync() {
        const active = !reduced.matches && visible && !document.hidden && !paused && onPage;
        if (active) motion.play(); else motion.pause();
        path.style.opacity = reduced.matches ? '0' : '1';
        toggle.hidden = reduced.matches; toggle.setAttribute('aria-pressed', String(paused));
        toggle.textContent = paused ? 'Resume lighting' : 'Pause lighting';
    }
    toggle.addEventListener('click', () => { paused = !paused; sync(); });
    reduced.addEventListener('change', sync); document.addEventListener('visibilitychange', sync);
    if ('IntersectionObserver' in window) new IntersectionObserver(entries => {visible=entries[0].isIntersecting;sync();}).observe(path.closest('.ref-showcase'));
    window.addEventListener('pagehide', () => {onPage=false;sync();});
    window.addEventListener('pageshow', () => {onPage=true;sync();});
    sync();
})();
