/* Progressive motion enhancement. All navigation and forms remain native MVC. */
(() => {
  'use strict';
  const reduced = matchMedia('(prefers-reduced-motion: reduce)');
  const compact = matchMedia('(max-width: 800px)');
  const root = document.documentElement;
  root.classList.add('js-enabled');
  const menu = document.querySelector('.menu-toggle');
  const sidebar = document.querySelector('.sidebar');
  const scrim = document.querySelector('.sidebar-scrim');
  function closeMenu(restoreFocus = false) {
    root.classList.remove('nav-open');
    menu?.setAttribute('aria-expanded', 'false');
    menu?.setAttribute('aria-label', 'Open navigation');
    if (scrim) scrim.hidden = true;
    if (sidebar) sidebar.inert = compact.matches;
    if (restoreFocus) menu?.focus();
  }
  function syncMenu() {
    if (!menu) return;
    menu.hidden = !compact.matches;
    closeMenu();
  }
  menu?.addEventListener('click', () => {
    if (root.classList.contains('nav-open')) return closeMenu(true);
    root.classList.add('nav-open');
    sidebar.inert = false;
    menu.setAttribute('aria-expanded', 'true');
    menu.setAttribute('aria-label', 'Close navigation');
    scrim.hidden = false;
    sidebar.querySelector('a')?.focus();
  });
  scrim?.addEventListener('click', () => closeMenu(true));
  compact.addEventListener('change', syncMenu);
  syncMenu();
  document.addEventListener('keydown', event => {
    if (!root.classList.contains('nav-open')) return;
    if (event.key === 'Escape') closeMenu(true);
    if (event.key === 'Tab') {
      const focusable = [...sidebar.querySelectorAll('a, button')];
      const first = focusable[0], last = focusable.at(-1);
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last.focus(); }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first.focus(); }
    }
  });
  const animations = new Set();
  function animate(element, frames, options = {}) {
    if (reduced.matches || !element?.animate) return;
    const animation = element.animate(frames, {duration: 720, easing: 'cubic-bezier(.22,1,.36,1)', ...options});
    animations.add(animation);
    animation.finished.catch(() => {}).finally(() => animations.delete(animation));
    return animation;
  }
  function enter() {
    const parts = document.querySelectorAll('.breadcrumb-line, .page-heading, .collection-heading, .form-panel > .eyebrow, .form-panel > h1, .form-panel > p, .form-panel form, .login-panel > *, .login-story h2, .hero-copy > *, .hero-art, .document-panel, .message-panel, .table-responsive');
    parts.forEach((element, index) => animate(element, [{opacity: 0, transform: 'translateY(22px)', filter: 'blur(3px)'}, {opacity: 1, transform: 'translateY(0)', filter: 'blur(0)'}], {delay: Math.min(index * 65, 430)}));
    animate(document.querySelector('.side-link.active'), [{clipPath:'inset(0 100% 0 0 round 9px)'},{clipPath:'inset(0 0 0 0 round 9px)'}], {duration: 650});
  }
  enter();
  let observer;
  if ('IntersectionObserver' in window && !reduced.matches) {
    observer = new IntersectionObserver(entries => {
      entries.forEach(entry => {
        if (!entry.isIntersecting) return;
        const element = entry.target;
        observer.unobserve(element);
        if (element.matches('[data-count]')) {
          const total = Number(element.dataset.count);
          if (!Number.isSafeInteger(total) || total < 1) return;
          element.style.minWidth = `${String(total).length}ch`;
          const start = performance.now();
          function tick(now) {
            if (reduced.matches) { element.textContent = total; return; }
            const progress = Math.min((now - start) / 850, 1);
            element.textContent = Math.round(total * (1 - Math.pow(1 - progress, 3)));
            if (progress < 1) requestAnimationFrame(tick);
          }
          requestAnimationFrame(tick);
        } else animate(element, [{opacity: .25, transform:'translateY(12px)'},{opacity:1, transform:'translateY(0)'}], {duration:500, delay:Math.min((element.sectionRowIndex || 0) * 45, 300)});
      });
    }, {threshold: .12});
    document.querySelectorAll('tbody tr, [data-count], .process-card, .public-section .section-heading, .about-section, .contact-section, .supplier-section').forEach(element => observer.observe(element));
  }
  const art = document.querySelector('.hero-art');
  let pointerFrame;
  if (art && matchMedia('(hover: hover) and (pointer: fine)').matches) {
    art.addEventListener('pointermove', event => {
      if (reduced.matches || pointerFrame) return;
      const x = event.clientX, y = event.clientY;
      pointerFrame = requestAnimationFrame(() => {
        const rect = art.getBoundingClientRect();
        const px = (x - rect.left) / rect.width, py = (y - rect.top) / rect.height;
        art.style.setProperty('--light-x', `${px * 100}%`);
        art.style.setProperty('--light-y', `${py * 100}%`);
        art.style.transform = `rotateX(${(0.5-py)*5}deg) rotateY(${(px-0.5)*5}deg)`;
        pointerFrame = null;
      });
    });
    art.addEventListener('pointerleave', () => { art.style.transform = ''; });
  }
  // Do not prevent, resubmit, disable form fields, or alter posted values.
  document.addEventListener('submit', event => {
    const form = event.target;
    if (!(form instanceof HTMLFormElement) || event.defaultPrevented) return;
    if (window.jQuery?.validator && !window.jQuery(form).valid()) return;
    const button = event.submitter;
    if (button) {
      button.setAttribute('aria-busy', 'true');
      setTimeout(() => button.removeAttribute('aria-busy'), 12000);
    }
  });
  // Exit choreography runs alongside native navigation, with no delays or history interception.
  document.addEventListener('click', event => {
    const link = event.target.closest('a');
    if (!link || event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey || link.target || link.hasAttribute('download')) return;
    const target = new URL(link.href, location.href);
    if (target.origin !== location.origin || target.hash || target.pathname === location.pathname && target.search === location.search) return;
    animate(document.querySelector('main'), [{opacity:1, transform:'translateY(0)'},{opacity:.4, transform:'translateY(-7px)'}], {duration:160});
  });
  addEventListener('pageshow', event => {
    document.querySelectorAll('[aria-busy=true]').forEach(element => element.removeAttribute('aria-busy'));
    if (event.persisted) enter();
  });
  reduced.addEventListener('change', () => {
    if (!reduced.matches) return;
    animations.forEach(animation => animation.cancel());
    observer?.disconnect();
    if (art) art.style.transform = '';
    document.querySelectorAll('[data-count]').forEach(element => element.textContent = element.dataset.count);
  });
})();
