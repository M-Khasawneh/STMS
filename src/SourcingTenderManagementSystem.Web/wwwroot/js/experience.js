/* Accessible multi-role selection, retaining the original MVC select as the successful form control. */
(() => {
  'use strict';
  document.querySelectorAll('select[data-role-picker]').forEach(select => {
    const form = select.form, update = form.querySelector('[name=UpdateRoles][type=checkbox]');
    const menu = document.createElement('details'); menu.className = 'role-picker';
    const summary = document.createElement('summary'); summary.setAttribute('aria-label', 'Select employee roles');
    const list = document.createElement('fieldset'); const legend = document.createElement('legend'); legend.className = 'visually-hidden'; legend.textContent = 'Employee roles'; list.append(legend);
    const inputs = [];
    for (const option of select.options) {
      const label = document.createElement('label'), check = document.createElement('input'), text = document.createElement('span');
      check.type = 'checkbox'; check.checked = option.selected; check.value = option.value; check.dataset.roleOption = '';
      text.textContent = option.text; label.append(check, text); list.append(label); inputs.push(check);
      check.addEventListener('change', () => { option.selected = check.checked; select.dispatchEvent(new Event('change', {bubbles:true})); render(); });
    }
    const error = document.createElement('p'); error.className = 'text-danger'; error.setAttribute('role','alert'); error.hidden = true;
    function render() {
      const selected = [...select.selectedOptions].map(option => option.text);
      summary.textContent = selected.length ? selected.join(', ') : 'Select employee roles';
      const enabled = !update || update.checked; inputs.forEach(input => input.disabled = !enabled);
      if (selected.length) error.hidden = true;
    }
    menu.append(summary, list); select.after(menu, error); select.hidden = true;
    update?.addEventListener('change', render); render();
    form.addEventListener('submit', event => {
      if ((!update || update.checked) && select.selectedOptions.length === 0) {
        event.preventDefault(); error.textContent = 'Select at least one employee role.'; error.hidden = false; menu.open = true; summary.focus();
      }
    });
    document.addEventListener('click', event => { if (!menu.contains(event.target)) menu.open = false; });
    menu.addEventListener('keydown', event => { if (event.key === 'Escape' && menu.open) { event.preventDefault(); event.stopPropagation(); menu.open = false; summary.focus(); } }, true);
  });
})();
