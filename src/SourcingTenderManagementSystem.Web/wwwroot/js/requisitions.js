(() => {
    'use strict';
    const form = document.querySelector('[data-requisition-form]');
    if (form) {
        const items = form.querySelector('[data-requisition-items]');
        const template = form.querySelector('[data-item-template]');
        function reindex() {
            const rows = [...items.querySelectorAll('[data-requisition-item]')];
            rows.forEach((row, index) => {
                row.querySelector('[data-item-number]').textContent = index + 1;
                row.querySelector('[data-remove-item]').value = index;
                row.querySelector('[data-remove-item]').setAttribute('aria-label', `Remove item ${index + 1}`);
                row.querySelectorAll('[name], [id], [for], [data-valmsg-for], [aria-describedby]').forEach(element => {
                    ['name', 'id', 'for', 'data-valmsg-for', 'aria-describedby'].forEach(attribute => {
                        if (element.hasAttribute(attribute)) element.setAttribute(attribute, element.getAttribute(attribute)
                            .replace(/Items\[\d+\]/g, `Items[${index}]`).replace(/Items_\d+_+/g, `Items_${index}__`));
                    });
                });
            });
            form.querySelector('[data-item-feedback]').textContent = `${rows.length} request items.`;
            if (window.jQuery?.validator) {
                const $form = window.jQuery(form);
                $form.data('validator')?.destroy();
                $form.removeData('unobtrusiveValidation');
                window.jQuery.validator.unobtrusive.parse(form);
            }
        }
        form.querySelector('[data-add-item]').addEventListener('click', event => {
            event.preventDefault();
            const index = items.querySelectorAll('[data-requisition-item]').length;
            const fragment = document.createElement('template');
            fragment.innerHTML = template.innerHTML.replaceAll('__index__', String(index));
            const row = fragment.content.firstElementChild;
            items.append(row); reindex(); row.querySelector('input:not([type=hidden])')?.focus();
            if (!matchMedia('(prefers-reduced-motion: reduce)').matches && row.animate)
                row.animate([{ opacity: .2, transform: 'translateY(12px)' }, { opacity: 1, transform: 'translateY(0)' }], { duration: 350, easing: 'cubic-bezier(.22,1,.36,1)' });
        });
        items.addEventListener('click', event => {
            const button = event.target.closest('[data-remove-item]');
            if (!button) return;
            event.preventDefault();
            const row = button.closest('[data-requisition-item]');
            const next = row.nextElementSibling || row.previousElementSibling;
            row.remove(); reindex();
            (next?.querySelector('input:not([type=hidden])') || form.querySelector('[data-add-item]')).focus();
        });
        const date = form.querySelector('[name=TargetDate]');
        form.addEventListener('click', event => {
            const button = event.target.closest('button[name=command]');
            if (!button) return;
            date.min = button.value === 'Submit' ? form.dataset.today : '';
        });
    }
    function reviewValidation() {
        const review = document.querySelector('[data-requisition-review]');
        const comments = review?.querySelector('[name=Comments]');
        if (comments && window.jQuery?.validator) {
            window.jQuery(review).validate();
            window.jQuery(comments).rules('add', { required: () => ['Rejected', 'Returned'].includes(review.querySelector('[name=Decision]').value),
                messages: { required: 'A reason is required for rejection or return.' } });
        }
    }
    // Unobtrusive validation registers its ready callback before this script.
    if (window.jQuery) window.jQuery(reviewValidation);
    else if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', reviewValidation);
    else reviewValidation();
})();
