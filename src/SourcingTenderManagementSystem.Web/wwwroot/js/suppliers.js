(() => {
    'use strict';
    document.querySelectorAll('[data-supplier-upload]').forEach(input => {
        input.addEventListener('change', () => {
            const file = input.files[0];
            const error = file && (!/\.(pdf|jpg|png)$/i.test(file.name) || file.size === 0 || file.size > 5 * 1024 * 1024)
                ? 'Select a PDF, JPG or PNG file containing data, no larger than 5 MB.' : '';
            input.setCustomValidity(error);
            const feedback = document.querySelector(`[data-valmsg-for="${input.name}"]`);
            if (feedback) { feedback.textContent = error; feedback.classList.toggle('field-validation-error', !!error); }
        });
    });
    function configureReview() {
    const form = document.querySelector('[data-supplier-review]');
    if (form && window.jQuery?.validator) {
        window.jQuery(form).validate();
        window.jQuery(form.querySelector('[name=Reason]')).rules('add', {
            required: () => ['Rejected', 'Suspended'].includes(form.querySelector('[name=Decision]').value),
            messages: { required: 'A reason is required for rejection or suspension.' }
        });
    }
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', configureReview);
    else configureReview();
})();
