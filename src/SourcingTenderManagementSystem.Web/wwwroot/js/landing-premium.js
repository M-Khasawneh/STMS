/* Native MVC animation system. Existing home.js owns public navigation. */
(() => {
    'use strict';
    const root = document.querySelector('.landing-redesign');
    if (!root) return;
    const reduced = matchMedia('(prefers-reduced-motion: reduce)');
    const fine = matchMedia('(hover: hover) and (pointer: fine)');
    const active = new Set();
    const steps = [...root.querySelectorAll('.lp-steps li')];
    const meter = root.querySelector('.lp-workflow-meter');
    const meterLabel = root.querySelector('[data-workflow-label]');
    let pointerFrame = 0;
    let scrollFrame = 0;

    function animate(element, delay = 0) {
        if (reduced.matches || !element.animate) return;
        const animation = element.animate([
            { opacity: 0, transform: 'translateY(30px)', filter: 'blur(3px)' },
            { opacity: 1, transform: 'translateY(0)', filter: 'blur(0)' }
        ], { duration: 850, delay, easing: 'cubic-bezier(.22,1,.36,1)', fill: 'backwards' });
        active.add(animation);
        animation.finished.then(() => active.delete(animation)).catch(() => active.delete(animation));
    }

    root.querySelectorAll('[data-enter]').forEach((element, index) => animate(element, index * 75));
    const reveal = 'IntersectionObserver' in window ? new IntersectionObserver(entries => {
        entries.forEach(entry => {
            if (!entry.isIntersecting) return;
            reveal.unobserve(entry.target);
            const siblings = [...entry.target.parentElement.children];
            animate(entry.target, Math.min(siblings.indexOf(entry.target), 3) * 75);
        });
    }, { threshold: .12 }) : null;
    root.querySelectorAll('[data-reveal]').forEach(element => reveal?.observe(element));

    function updateWorkflow() {
        scrollFrame = 0;
        const positions = steps.map(step => step.getBoundingClientRect().top);
        const threshold = innerHeight * .48;
        let current = 0;
        positions.forEach((top, index) => { if (top <= threshold) current = index; });
        steps.forEach((step, index) => step.classList.toggle('is-current', index === current));
        meter?.style.setProperty('--workflow-progress', String((current + 1) / steps.length));
        if (meterLabel) meterLabel.textContent = steps[current].querySelector('h3').textContent;
    }
    function scheduleScroll() {
        if (!scrollFrame) scrollFrame = requestAnimationFrame(updateWorkflow);
    }
    window.addEventListener('scroll', scheduleScroll, { passive: true });
    window.addEventListener('resize', scheduleScroll);
    scheduleScroll();

    root.addEventListener('pointermove', event => {
        if (reduced.matches || !fine.matches || pointerFrame) return;
        const card = event.target.closest('.lp-feature');
        if (!card) return;
        const x = event.clientX, y = event.clientY;
        pointerFrame = requestAnimationFrame(() => {
            pointerFrame = 0;
            const box = card.getBoundingClientRect();
            card.style.setProperty('--card-x', `${x - box.left}px`);
            card.style.setProperty('--card-y', `${y - box.top}px`);
        });
    }, { passive: true });
    function resetMotion() {
        cancelAnimationFrame(pointerFrame);
        pointerFrame = 0;
        active.forEach(animation => animation.cancel());
        active.clear();
    }
    reduced.addEventListener('change', resetMotion);
    fine.addEventListener('change', resetMotion);
    window.addEventListener('pagehide', () => {
        resetMotion();
        cancelAnimationFrame(scrollFrame);
        scrollFrame = 0;
    });
    window.addEventListener('pageshow', scheduleScroll);
})();
