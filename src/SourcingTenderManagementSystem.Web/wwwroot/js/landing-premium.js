/* Native motion adaptation of the 21st hero/nav references. No runtime dependencies. */
(() => {
    'use strict';
    if (!document.body.classList.contains('premium-landing')) return;
    const reduced = matchMedia('(prefers-reduced-motion: reduce)');
    const pointer = matchMedia('(hover: hover) and (pointer: fine)');
    const hero = document.querySelector('.stms-hero');
    const cards = [...document.querySelectorAll('[data-landing-reveal]')];
    const active = new Set();
    let frame = 0;

    // Content stays visible until an actual animation starts. Failed or absent APIs leave it readable.
    const observer = 'IntersectionObserver' in window ? new IntersectionObserver(entries => {
        entries.forEach(entry => {
            if (!entry.isIntersecting) return;
            observer.unobserve(entry.target);
            if (reduced.matches || !entry.target.animate) return;
            const animation = entry.target.animate([
                { opacity: 0, transform: 'translateY(28px) scale(.98)' },
                { opacity: 1, transform: 'translateY(0) scale(1)' }
            ], { duration: 700, delay: cards.indexOf(entry.target) * 80, easing: 'cubic-bezier(.22,1,.36,1)' });
            active.add(animation);
            animation.finished.then(() => active.delete(animation)).catch(() => active.delete(animation));
        });
    }, { threshold: .12 }) : null;
    cards.forEach(card => observer?.observe(card));

    const trackPointer = event => {
        if (reduced.matches || !pointer.matches || frame) return;
        const card = event.target.closest('[data-landing-reveal]');
        const target = card || (hero?.contains(event.target) ? hero : null);
        if (!target) return;
        const x = event.clientX, y = event.clientY;
        frame = requestAnimationFrame(() => {
            frame = 0;
            const bounds = target.getBoundingClientRect();
            if (card) {
                card.style.setProperty('--card-x', `${x - bounds.left}px`);
                card.style.setProperty('--card-y', `${y - bounds.top}px`);
            } else {
                hero.style.setProperty('--spot-x', `${(x - bounds.left - bounds.width / 2) * .12}px`);
                hero.style.setProperty('--spot-y', `${(y - bounds.top - bounds.height / 2) * .1}px`);
            }
        });
    };
    document.addEventListener('pointermove', trackPointer, { passive: true });
    const reset = () => {
        cancelAnimationFrame(frame);
        frame = 0;
        active.forEach(animation => animation.cancel());
        active.clear();
        hero?.style.removeProperty('--spot-x');
        hero?.style.removeProperty('--spot-y');
        cards.forEach(card => { card.style.removeProperty('--card-x'); card.style.removeProperty('--card-y'); });
    };
    reduced.addEventListener('change', reset);
    pointer.addEventListener('change', reset);
    window.addEventListener('pagehide', () => { reset(); observer?.disconnect(); });
})();
