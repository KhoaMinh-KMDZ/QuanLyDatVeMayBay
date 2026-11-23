document.querySelectorAll('[data-password-toggle]').forEach(button => {
    const input = document.getElementById(button.getAttribute('aria-controls'));
    if (!input) return;

    button.addEventListener('click', () => {
        const show = input.type === 'password';
        const start = input.selectionStart;
        const end = input.selectionEnd;

        input.type = show ? 'text' : 'password';
        button.textContent = show ? 'Ẩn' : 'Hiện';
        button.setAttribute('aria-pressed', String(show));

        input.focus({ preventScroll: true });
        if (start !== null && end !== null)
            input.setSelectionRange(start, end);
    });
});
