/* Same-origin MVC form dialogs. No AJAX mutations, copied forms, token rewriting or business rules. */
(() => {
  'use strict';
  if (!document.body.classList.contains('workspace') || !window.HTMLDialogElement) return;
  const controller = document.body.dataset.workspaceController;
  const action = document.body.dataset.workspaceAction;
  const controllers = new Set(['Departments', 'Employees', 'Suppliers', 'Requisitions', 'Tenders', 'Bids']);
  const editorActions = new Set(['Create', 'Edit', 'Criteria', 'Invitations', 'Submit']);
  const route = url => {
    const segments = url.pathname.split('/').filter(Boolean);
    return {controller: segments[0] || 'Home', action: segments[1] || 'Index'};
  };
  const isEditor = url => { const r = route(url); return r.controller === 'Profile' && r.action === 'Index' || controllers.has(r.controller) && editorActions.has(r.action); };
  const formKey = form => { const url = new URL(form.action, location.href); return url.pathname + url.search; };
  const main = document.querySelector('main');
  const forms = () => [...main.querySelectorAll('form')].filter(form => form.method.toLowerCase() === 'post');
  let frame;
  try { frame = window.frameElement; } catch { return; }

  if (frame?.matches('iframe[data-workspace-popup]')) {
    document.documentElement.classList.add('workspace-popup-embed');
    // The authenticated route was rendered normally and all original validation/page scripts already ran.
    // For an inline decision, retain its original DOM form (and listeners) and relevant validation feedback.
    const key = frame.dataset.formKey;
    if (key) {
      const selected = forms().filter(form => formKey(form) === key)[Number(frame.dataset.formIndex || 0)];
      if (selected) {
        const heading = main.querySelector('h1');
        const notices = [...main.querySelectorAll('.validation-summary-errors, .alert, [role="alert"]')].filter(element => !selected.contains(element) && element.textContent.trim());
        const host = document.createElement('section'); host.className = 'workspace-inline-form';
        if (heading) host.append(heading.cloneNode(true));
        notices.forEach(element => host.append(element.cloneNode(true)));
        host.append(selected); main.replaceChildren(host);
        selected.hidden = false;
      }
    }
    let dirty = frame.dataset.unsaved === 'true' || !!main.querySelector('.validation-summary-errors, .field-validation-error');
    main.addEventListener('input', event => {
      dirty = true;
      // Clear reported errors while correcting a value, rather than moving the next
      // submit button when focus leaves the field during a click.
      if (window.jQuery?.validator && event.target.matches('.input-validation-error, [aria-invalid=true]')) window.jQuery(event.target).valid();
    });
    main.addEventListener('change', () => { dirty = true; });
    window.workspacePopupIsDirty = () => dirty;
    document.addEventListener('submit', event => {
      if (event.defaultPrevented || event.target.method?.toLowerCase() !== 'post') return;
      if (!event.submitter?.formNoValidate && window.jQuery?.validator && !window.jQuery(event.target).valid()) return;
      dirty = false;
      const button = event.submitter;
      const transient = button && (['AddItem', 'AddCharge'].includes(button.value) || ['removeItem', 'removeCharge'].includes(button.name));
      frame.dataset.unsaved = transient ? 'true' : 'false';
      if (!transient) frame.dataset.submitted = 'true';
    });
    document.addEventListener('keydown', event => {
      if (event.key !== 'Escape' || event.defaultPrevented) return;
      event.preventDefault(); parent.workspacePopup?.close();
    });
    document.addEventListener('click', event => {
      const link = event.target.closest('a');
      if (!link || event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey || link.target || link.hasAttribute('download')) return;
      const url = new URL(link.href, location.href);
      if (url.origin !== location.origin || url.hash || /\/(Document|Summary)(\/|$)/i.test(url.pathname)) return;
      if (isEditor(url)) { frame.dataset.formKey = ''; return; }
      event.preventDefault(); parent.workspacePopup?.navigate(url.href);
    });
    // Success redirects to list/details return control to the workspace. Validation and ongoing
    // editing stay in the dialog, including add-item commands, file uploads and criteria configuration.
    const success = !!main.querySelector('.alert-success');
    const ended = controller !== 'Profile' && !isEditor(new URL(location.href)) && !forms().some(form => !key || formKey(form) === key);
    if (frame.dataset.submitted === 'true' && (key ? success || ended : ended)) {
      dirty = false;
      parent.workspacePopup?.complete(main.querySelector('.alert-success')?.textContent.trim());
    }
    return;
  }

  let dialog, iframe, title, loading, opener;
  function ensureDialog() {
    if (dialog) return;
    dialog = document.createElement('dialog'); dialog.className = 'workspace-popup';
    dialog.setAttribute('aria-labelledby', 'workspace-popup-title');
    const header = document.createElement('header'); header.className = 'workspace-popup-header';
    const copy = document.createElement('div');
    const eyebrow = document.createElement('span'); eyebrow.className = 'workspace-popup-eyebrow'; eyebrow.textContent = 'STMS / WORKSPACE';
    title = document.createElement('h2'); title.id = 'workspace-popup-title';
    copy.append(eyebrow, title);
    const close = document.createElement('button'); close.type = 'button'; close.className = 'workspace-popup-close';
    close.setAttribute('aria-label', 'Close form'); close.textContent = '×'; close.addEventListener('click', () => closePopup());
    header.append(copy, close);
    loading = document.createElement('div'); loading.className = 'workspace-popup-loading'; loading.setAttribute('role', 'status'); loading.textContent = 'Opening secure form…';
    iframe = document.createElement('iframe'); iframe.dataset.workspacePopup = 'true'; iframe.name = 'stms-workspace-form';
    iframe.addEventListener('load', () => {
      if (!dialog.open) return;
      loading.hidden = true; iframe.style.visibility = 'visible';
      try {
        const doc = iframe.contentDocument;
        // Login and access-denied responses never render as an authenticated workspace.
        if (!doc.body.classList.contains('workspace')) return;
        title.textContent = doc.querySelector('h1')?.textContent.trim() || 'Workspace form';
        iframe.title = title.textContent;
      } catch { /* Native frame error stays visible; never inspect another origin. */ }
    });
    dialog.append(header, loading, iframe); document.body.append(dialog);
    dialog.addEventListener('cancel', event => { event.preventDefault(); closePopup(); });
    dialog.addEventListener('click', event => {
      if (event.target !== dialog) return;
      const rect = dialog.getBoundingClientRect();
      if (event.clientX < rect.left || event.clientX > rect.right || event.clientY < rect.top || event.clientY > rect.bottom) closePopup();
    });
  }
  function mayLeave() {
    try {
      if (iframe?.contentWindow?.workspacePopupIsDirty?.()) return window.confirm('Discard unsaved changes and close this form?');
    } catch { /* A navigation/login error has no editable same-origin form. */ }
    return true;
  }
  function closePopup(force = false) {
    if (!dialog?.open || !force && !mayLeave()) return;
    const refresh = iframe.dataset.submitted === 'true';
    dialog.close(); document.documentElement.classList.remove('workspace-popup-open');
    iframe.src = 'about:blank';
    opener?.focus();
    if (refresh) location.reload();
  }
  function openPopup(url, label, element, key = '', index = 0) {
    if (url.origin !== location.origin) return;
    ensureDialog(); opener = element;
    dialog.dataset.size = key || route(url).controller === 'Departments' ? 'compact' : 'wide';
    iframe.dataset.formKey = key; iframe.dataset.formIndex = String(index); iframe.dataset.submitted = 'false'; iframe.dataset.unsaved = 'false';
    title.textContent = label || 'Workspace form'; iframe.title = title.textContent;
    loading.hidden = false; iframe.style.visibility = 'hidden';
    document.documentElement.classList.add('workspace-popup-open');
    dialog.showModal(); iframe.src = url.href;
  }
  window.workspacePopup = {
    close: () => closePopup(),
    complete: message => {
      if (message) window.workspaceFeedback?.remember(message);
      // A direct editor URL remains a working fallback. After a terminal action, do not
      // reload an editor that has just become read-only (for example a submitted bid).
      if (editorActions.has(action) && !iframe.dataset.formKey) {
        try {
          const url = new URL(iframe.contentWindow.location.href);
          if (url.origin === location.origin && !isEditor(url)) {
            dialog.close(); document.documentElement.classList.remove('workspace-popup-open'); location.assign(url.href); return;
          }
        } catch { /* Same-origin reload below is always safe. */ }
      }
      closePopup(true);
    },
    navigate: href => {
      const url = new URL(href, location.href);
      if (url.origin !== location.origin || !mayLeave()) return;
      dialog.close(); document.documentElement.classList.remove('workspace-popup-open'); location.assign(url.href);
    }
  };
  document.addEventListener('click', event => {
    const link = event.target.closest('a');
    if (!link || event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey || link.target || link.hasAttribute('download')) return;
    const url = new URL(link.href, location.href);
    if (url.origin !== location.origin || !isEditor(url)) return;
    event.preventDefault();
    openPopup(url, link.getAttribute('aria-label') || link.textContent.trim(), link);
  });
  // Form pages already opened from links stay together (items, documents and validation).
  // Only inline workflow forms on details/list pages become launch buttons.
  if (controllers.has(controller) && action !== 'Register' && action !== 'Confirmation') {
    const all = forms();
    all.forEach(form => {
      if (editorActions.has(action) && !form.hasAttribute('data-popup-inline')) return;
      const submitters = [...form.querySelectorAll('button[type=submit], input[type=submit]')];
      if (!submitters.length) return;
      const key = formKey(form); const index = all.filter(other => formKey(other) === key).indexOf(form);
      const label = submitters.length > 1 ? 'Open ' + (form.querySelector('legend')?.textContent.trim() || 'available actions') : submitters[0].textContent.trim() || submitters[0].value || 'Open form';
      const button = document.createElement('button'); button.type = 'button';
      button.className = 'btn btn-outline-secondary workspace-popup-launch'; button.textContent = label;
      button.setAttribute('aria-haspopup', 'dialog');
      button.addEventListener('click', () => openPopup(new URL(location.href), label, button, key, index));
      form.before(button); form.classList.add('workspace-popup-source'); form.hidden = true;
    });
  }
})();
