/* Progressive enhancement only. Authoritative pricing, eligibility and submission stay on the server. */
(() => {
  'use strict';
  const editor = document.querySelector('[data-bid-editor]');
  const status = document.querySelector('[data-bid-form-status]');
  let dirty = false;
  if (editor) {
    const list = editor.querySelector('[data-charge-list]');
    const template = editor.querySelector('[data-charge-template]');
    function reindex() {
      list.querySelectorAll('[data-charge]').forEach((row, index) => {
        row.querySelectorAll('input').forEach(input => {
          const field = input.dataset.costField || input.name.split('.').pop();
          input.name = `Form.Charges[${index}].${field}`;
          input.id = `Form_Charges_${index}__${field}`;
          const label = input.parentElement.querySelector('label');
          if (label) label.htmlFor = input.id;
          const message = input.parentElement.querySelector('[data-valmsg-for]');
          if (message) {
            message.dataset.valmsgFor = input.name;
            const error = message.querySelector('[id]');
            if (error) { error.id = input.id + '-error'; input.setAttribute('aria-describedby', error.id); }
          }
        });
        const remove = row.querySelector('[data-remove-charge]');
        remove.value = String(index);
        remove.setAttribute('aria-label', `Remove additional cost ${index + 1}`);
      });
      // jQuery observes the formnovalidate click before this enhancement cancels it.
      // Do not let that flag bypass validation on the subsequent Save click.
      const validator = window.jQuery?.(editor).data('validator');
      if (validator) validator.cancelSubmit = false;
    }
    // Fixed-point BigInt avoids floating-point drift. Quantities/prices have at most six decimals.
    function fixed(value, positive) {
      if (value.startsWith('.')) value = '0' + value;
      if (!/^\d+(\.\d{1,6})?$/.test(value)) return null;
      const [whole, fraction = ''] = value.split('.');
      const result = BigInt(whole) * 1000000n + BigInt(fraction.padEnd(6, '0'));
      if (result > 999999999999999999n || (positive && result <= 0n)) return null;
      return result;
    }
    function money(value) {
      const whole = value / 1000000000000n;
      let fraction = (value % 1000000000000n).toString().padStart(12, '0').replace(/0+$/, '');
      fraction = fraction.padEnd(3, '0');
      return `${whole.toString()}.${fraction}`;
    }
    function totals() {
      if (typeof BigInt !== 'function') return;
      let total = 0n, valid = true;
      editor.querySelectorAll('[data-bid-line]').forEach(row => {
        const quantity = fixed(row.dataset.quantity, true);
        const price = fixed(row.querySelector('[data-unit-price]').value, true);
        const value = quantity !== null && price !== null ? quantity * price : null;
        row.querySelector('[data-line-total]').textContent = value === null ? '—' : money(value);
        if (value === null) valid = false; else total += value;
      });
      editor.querySelectorAll('[data-charge-amount]').forEach(input => {
        const amount = fixed(input.value, false);
        if (amount === null) valid = false; else total += amount * 1000000n;
      });
      editor.querySelector('[data-bid-total]').textContent = valid ? money(total) : 'Complete valid prices';
    }
    function changed() {
      dirty = true;
      if (status) status.textContent = 'You have unsaved offer changes. Save the draft before managing documents or submitting.';
      totals();
    }
    editor.addEventListener('input', event => {
      // Clear already-reported errors as they are corrected. Waiting until the next button's
      // mousedown/focusout would collapse validation text and move that button mid-click.
      if (window.jQuery?.validator && event.target.matches('.input-validation-error, [aria-invalid=true]'))
        window.jQuery(event.target).valid();
      changed();
    });
    editor.addEventListener('click', event => {
      const add = event.target.closest('[data-add-charge]');
      const remove = event.target.closest('[data-remove-charge]');
      if (add && template && list) {
        event.preventDefault(); list.append(template.content.cloneNode(true)); reindex(); changed();
        list.lastElementChild?.querySelector('input')?.focus();
      }
      if (remove) {
        event.preventDefault(); remove.closest('[data-charge]').remove(); reindex(); changed();
        editor.querySelector('[data-add-charge]')?.focus();
      }
    });
    editor.addEventListener('submit', event => {
      if (event.defaultPrevented) return;
      if (window.jQuery?.validator && !window.jQuery(editor).valid()) return;
      // Native validation still applies, and named submit buttons remain successful controls.
      dirty = false;
    });
    document.querySelectorAll('[data-document-form]').forEach(form => {
      form.addEventListener('submit', event => {
        if (!dirty) return;
        event.preventDefault();
        if (status) { status.textContent = 'Save your offer changes first. Your selected document has not been uploaded or removed.'; status.scrollIntoView({block: 'nearest'}); }
        editor.querySelector('[data-save-draft]')?.focus();
      });
      const input = form.querySelector('input[type=file]');
      input?.addEventListener('change', () => {
        const file = input.files?.[0];
        const invalid = file && (file.size === 0 || file.size > 5 * 1024 * 1024 || !/\.(pdf|jpg|png)$/i.test(file.name));
        input.setCustomValidity(invalid ? 'Choose a nonempty PDF, JPG or PNG file no larger than 5 MB.' : '');
        if (invalid) input.reportValidity();
      });
    });
    document.querySelector('[data-leave-editor]')?.addEventListener('click', event => {
      if (!dirty) return;
      event.preventDefault();
      if (status) status.textContent = 'Save your draft changes before reviewing the final offer.';
      editor.querySelector('[data-save-draft]')?.focus();
    });
    addEventListener('beforeunload', event => { if (dirty) { event.preventDefault(); event.returnValue = ''; } });
    totals();
  }
  const submit = document.querySelector('[data-bid-submit]');
  if (submit) {
    let sending = false;
    submit.addEventListener('submit', event => {
      if (event.defaultPrevented || !submit.checkValidity()) return;
      if (sending) { event.preventDefault(); return; }
      sending = true;
      submit.querySelector('button[type=submit]')?.setAttribute('aria-disabled', 'true');
    });
    addEventListener('pageshow', () => { sending = false; submit.querySelector('button[type=submit]')?.removeAttribute('aria-disabled'); });
  }
})();
