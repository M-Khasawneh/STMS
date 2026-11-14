/* Phase A public navigation. Links, browser history, authentication and forms stay native. */
(() => {
  'use strict';
  const body = document.body;
  if (!body.classList.contains('public-shell')) return;
  const toggle = document.querySelector('.home-menu-toggle');
  const nav = document.getElementById('home-navigation');
  const header = document.querySelector('.topbar');
  const compact = matchMedia('(max-width: 1050px)');
  const reduced = matchMedia('(prefers-reduced-motion: reduce)');
  if (toggle && nav) {
    body.classList.add('home-nav-ready');
    function close(restoreFocus = false) {
      body.classList.remove('home-nav-open');
      toggle.setAttribute('aria-expanded', 'false');
      toggle.setAttribute('aria-label', 'Open public navigation');
      nav.inert = compact.matches;
      if (restoreFocus) toggle.focus();
    }
    function sync() { toggle.hidden = !compact.matches; close(); schedule(); }
    toggle.addEventListener('click', () => {
      if (body.classList.contains('home-nav-open')) return close();
      body.classList.add('home-nav-open');
      toggle.setAttribute('aria-expanded', 'true');
      toggle.setAttribute('aria-label', 'Close public navigation');
      nav.inert = false;
    });
    nav.addEventListener('click', event => { if (event.target.closest('a') && compact.matches) close(); });
    document.addEventListener('keydown', event => { if (event.key === 'Escape' && body.classList.contains('home-nav-open')) close(true); });
    document.addEventListener('click', event => { if (body.classList.contains('home-nav-open') && !event.target.closest('.topbar')) close(); });
    header.addEventListener('focusout', event => {
      if (!compact.matches) return;
      if (event.relatedTarget) { if (!header.contains(event.relatedTarget)) close(); }
      else requestAnimationFrame(() => { if (!header.contains(document.activeElement)) close(); });
    });
    compact.addEventListener('change', sync);
    const normalizePath = path => {
      const normalized = path.toLowerCase().replace(/\/+$/, '') || '/';
      return normalized === '/home/index' ? '/' : normalized;
    };
    const currentPath = normalizePath(location.pathname);
    const links = [...nav.querySelectorAll('a')];
    const pageLink = links.find(link => {
      const url = new URL(link.href);
      return normalizePath(url.pathname) === currentPath && !url.hash;
    }) || links.find(link => normalizePath(new URL(link.href).pathname) === currentPath);
    const sectionLinks = links.filter(link => {
      const url = new URL(link.href);
      return url.origin === location.origin && normalizePath(url.pathname) === currentPath && url.hash && document.getElementById(url.hash.slice(1));
    });
    const sections = sectionLinks.map(link => document.getElementById(new URL(link.href).hash.slice(1)));
    let active = pageLink, preview = null, raf = 0;
    function indicator(link) {
      if (!link) return;
      nav.style.setProperty('--nav-active-x', `${link.offsetLeft}px`);
      nav.style.setProperty('--nav-active-y', `${link.offsetTop}px`);
      nav.style.setProperty('--nav-active-width', `${link.offsetWidth}px`);
      nav.style.setProperty('--nav-active-height', `${link.offsetHeight}px`);
      nav.classList.add('nav-indicator-ready');
    }
    function update() {
      raf = 0;
      // Measurements first, then writes; no wheel/touch interception.
      const y = scrollY, total = Math.max(1, document.documentElement.scrollHeight - innerHeight);
      const bounds = sections.map(section => section.getBoundingClientRect());
      const threshold = Math.min(240, innerHeight * .35);
      let next = pageLink, nearest = -Infinity;
      // Dedicated page links retain their page state; only existing sections on this document scroll-track.
      bounds.forEach((rect, i) => {
        if (rect.top <= threshold && rect.bottom > threshold && rect.top > nearest) {
          next = sectionLinks[i]; nearest = rect.top;
        }
      });
      if (y > 64) body.classList.add('home-is-scrolled');
      else if (y < 20) body.classList.remove('home-is-scrolled');
      body.style.setProperty('--home-scroll', `${Math.min(1, y / total)}`);
      if (next) {
        active = next;
        links.forEach(link => { link.classList.toggle('is-current', link === active); if (link === active) link.setAttribute('aria-current', new URL(link.href).hash ? 'location' : 'page'); else link.removeAttribute('aria-current'); });
      }
      indicator(preview || active);
    }
    function schedule() { if (!raf) raf = requestAnimationFrame(update); }
    links.forEach(link => {
      link.addEventListener('pointerenter', event => { if (event.pointerType !== 'touch') { preview = link; indicator(link); } });
      link.addEventListener('focus', () => { preview = link; indicator(link); });
    });
    nav.addEventListener('pointerleave', () => { preview = null; indicator(active); });
    nav.addEventListener('focusout', () => { queueMicrotask(() => { preview = links.includes(document.activeElement) ? document.activeElement : null; indicator(preview || active); }); });
    if ('ResizeObserver' in window) { const resize = new ResizeObserver(schedule); resize.observe(header); resize.observe(nav); }
    window.addEventListener('scroll', schedule, { passive:true });
    window.addEventListener('resize', schedule);
    window.addEventListener('pageshow', schedule);
    window.addEventListener('pagehide', () => { cancelAnimationFrame(raf); raf = 0; });
    sync();
  }
  function syncMotion() {
    const svg = document.querySelector('.stms-connections');
    if (reduced.matches) svg?.pauseAnimations?.(); else svg?.unpauseAnimations?.();
  }
  reduced.addEventListener('change', syncMotion);
  syncMotion();
})();
