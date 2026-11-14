(() => {
    const choice = document.getElementById('DecisionForm_Decision');
    const comments = document.getElementById('DecisionForm_Comments');
    if (!choice || !comments) return;
    const update = () => { comments.required = ['ManagerRejected', 'ReturnedForReview'].includes(choice.value); };
    choice.addEventListener('change', update);
    update();
})();
