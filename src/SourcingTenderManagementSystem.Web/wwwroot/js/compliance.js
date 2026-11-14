(() => {
    const outcome = document.getElementById('outcome');
    const reason = document.getElementById('reasons');
    if (outcome && reason) {
        const update = () => { reason.required = outcome.value === 'NonCompliant'; };
        outcome.addEventListener('change', update);
        update();
    }
    document.querySelectorAll('.compliance-workspace input[type="file"]').forEach(input => {
        input.addEventListener('change', () => {
            const files = Array.from(input.files || []);
            const invalid = files.length > 10 || files.some(file => file.size === 0 || file.size > 5 * 1024 * 1024 || !/\.(pdf|jpg|png)$/i.test(file.name));
            input.setCustomValidity(invalid ? 'Choose up to 10 PDF, JPG or PNG files, each between 1 byte and 5 MB.' : '');
            input.reportValidity();
        });
    });
})();
