/* Native scroll reveal; no scroll interception, frameworks or animation dependencies. */
(() => {
    const footer = document.querySelector('[data-sticky-footer]');
    if (!footer) return;
    const body = footer.querySelector('.sticky-footer-body');
    const columns = footer.querySelector('.sticky-footer-columns');
    const bottom = footer.querySelector('.sticky-footer-bottom');
    const reduced = matchMedia('(prefers-reduced-motion: reduce)');
    const eligible = matchMedia('(min-width: 900px) and (min-height: 650px)');
    const animations = new Set();
    let frame = 0, keyboard = false;
    function sync() {
        frame = 0;
        const active = eligible.matches && !reduced.matches && CSS.supports('clip-path', 'inset(0)');
        if (active) {
            const style = getComputedStyle(body);
            const needed = columns.offsetHeight + bottom.offsetHeight + parseFloat(style.paddingTop) + parseFloat(style.paddingBottom) + parseFloat(style.rowGap);
            const height = Math.max(440, Math.min(560, innerHeight * .62), needed);
            // Short viewports retain normal document flow rather than inaccessible nested scrolling.
            footer.style.setProperty('--sticky-footer-height', `${Math.ceil(height)}px`);
            footer.classList.toggle('footer-sticky-ready', height <= innerHeight - 40);
        } else footer.classList.remove('footer-sticky-ready');
        if (reduced.matches) { animations.forEach(animation => animation.cancel()); animations.clear(); }
    }
    const schedule = () => { if (!frame) frame = requestAnimationFrame(sync); };
    eligible.addEventListener('change', schedule); reduced.addEventListener('change', schedule);
    window.addEventListener('resize', schedule, { passive: true });
    document.fonts?.ready.then(schedule);
    if ('ResizeObserver' in window) { const observer = new ResizeObserver(schedule); observer.observe(columns); observer.observe(bottom); }
    if ('IntersectionObserver' in window) {
        const observer = new IntersectionObserver(entries => {
            entries.forEach(entry => {
                if (!entry.isIntersecting || entry.intersectionRatio < .12) return;
                const element = entry.target;
                observer.unobserve(element);
                if (reduced.matches || !element.animate) return;
                const index = [...footer.querySelectorAll('[data-footer-reveal]')].indexOf(element);
                const animation = element.animate([
                    { opacity: 0, transform: 'translateY(-8px)', filter: 'blur(4px)' },
                    { opacity: 1, transform: 'translateY(0)', filter: 'blur(0)' }
                ], { duration: 800, delay: 100 + index * 100, easing: 'ease', fill: 'none' });
                animations.add(animation); animation.finished.catch(() => {}).finally(() => animations.delete(animation));
            });
        }, { threshold: .12 });
        footer.querySelectorAll('[data-footer-reveal]').forEach(element => observer.observe(element));
    }
    document.addEventListener('keydown', event => { if (event.key === 'Tab') keyboard = true; });
    document.addEventListener('pointerdown', () => { keyboard = false; });
    footer.addEventListener('focusin', event => {
        if (keyboard && footer.classList.contains('footer-sticky-ready')) {
            animations.forEach(animation => animation.cancel()); animations.clear();
            footer.classList.add('footer-keyboard-flow');
            requestAnimationFrame(() => event.target.scrollIntoView({ block: 'nearest', behavior: 'instant' }));
        }
    });
    footer.addEventListener('focusout', event => { if (event.relatedTarget && !footer.contains(event.relatedTarget)) footer.classList.remove('footer-keyboard-flow'); });
    window.addEventListener('pageshow', schedule);
    sync();
})();
