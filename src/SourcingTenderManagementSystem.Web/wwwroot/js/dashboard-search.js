/* Native voice input for the existing, explicitly submitted MVC search. No application audio storage. */
(() => {
    'use strict';
    const root = document.querySelector('[data-dashboard-search]');
    if (!root) return;
    const menu = root.closest('[data-work-search-menu]');
    const toggle = menu.querySelector('[data-work-search-toggle]');
    const query = root.querySelector('[data-work-query]');
    const voice = root.querySelector('[data-work-voice]');
    const label = root.querySelector('[data-work-voice-label]');
    const language = root.querySelector('[data-work-language]');
    const status = root.querySelector('[data-work-status]');
    const form = root.querySelector('[data-work-search-form]');
    let recognition = null, session = 0, timer;
    const Constructor = window.SpeechRecognition || window.webkitSpeechRecognition;
    const supported = typeof Constructor === 'function' && window.isSecureContext;
    function updateQuery() {
        const ready = query.value.trim().length >= 2;
        toggle.classList.toggle('has-query', ready);
        toggle.setAttribute('aria-label', ready ? 'Voice and text search, query ready' : 'Voice and text search');
    }
    function finish(message, abort = false) {
        const current = recognition;
        recognition = null;
        session++;
        clearTimeout(timer);
        voice.setAttribute('aria-pressed', 'false');
        voice.setAttribute('aria-label', 'Start voice search');
        label.textContent = 'Use voice';
        if (abort && current) { try { current.abort(); } catch { /* Already ended. */ } }
        if (message) status.textContent = message;
    }
    function cancel(message = 'Voice input cancelled. Your search is unchanged.') { if (recognition) finish(message, true); }
    function closeSearch(restoreFocus = false) {
        cancel(); root.hidden = true; toggle.setAttribute('aria-expanded', 'false');
        if (restoreFocus) toggle.focus();
    }
    toggle.addEventListener('click', () => {
        if (!root.hidden) { closeSearch(true); return; }
        const notifications = document.querySelector('.notification-dropdown');
        if (notifications) notifications.open = false;
        root.hidden = false; toggle.setAttribute('aria-expanded', 'true'); query.focus();
    });
    root.querySelector('[data-work-search-close]').addEventListener('click', () => closeSearch(true));
    document.addEventListener('pointerdown', event => { if (!root.hidden && !menu.contains(event.target)) closeSearch(); });
    menu.addEventListener('focusout', event => {
        if (event.relatedTarget && !menu.contains(event.relatedTarget)) closeSearch();
    });
    menu.addEventListener('keydown', event => {
        if (event.key === 'Escape' && !root.hidden) {
            event.preventDefault();
            if (recognition) { cancel(); query.focus(); }
            else closeSearch(true);
        }
    });
    query.addEventListener('input', () => { cancel(); updateQuery(); });
    form.addEventListener('submit', () => cancel());
    root.querySelector('[data-work-clear]').addEventListener('click', () => {
        cancel(); query.value = ''; updateQuery(); status.textContent = 'Search cleared. Enter a new search.'; query.focus();
    });
    language.addEventListener('change', () => cancel('Voice language changed. Start voice input when ready.'));
    window.addEventListener('pagehide', () => cancel());
    document.addEventListener('visibilitychange', () => { if (document.hidden) cancel('Voice input stopped when the page was hidden.'); });
    voice.addEventListener('click', () => {
        if (recognition) { cancel(); return; }
        if (!supported) return;
        const ticket = ++session;
        try {
            const current = new Constructor();
            recognition = current;
            current.lang = language.value;
            current.continuous = false;
            current.interimResults = false;
            current.maxAlternatives = 1;
            const valid = () => ticket === session && recognition === current;
            current.onstart = () => { if (valid()) status.textContent = 'Listening. Say a title, company or account name. Choose Cancel voice or press Escape to stop.'; };
            current.onresult = event => {
                if (!valid()) return;
                const result = Array.from(event.results).find(item => item.isFinal);
                if (!result) return;
                const text = result[0]?.transcript?.trim().replace(/^[\s"'“”«»]+|[\s"'“”«».!?؟،؛]+$/gu, '').slice(0, 160);
                if (!text) { finish('No speech was recognized. Try again or type your search.', true); return; }
                query.value = text;
                updateQuery();
                finish('Voice input is ready. Review the words, then choose Search.', true);
            };
            current.onerror = event => {
                if (!valid()) return;
                const messages = {
                    'not-allowed': 'Microphone permission was denied. Allow access in your browser, or type your search.',
                    'service-not-allowed': 'Your browser does not allow speech recognition. Type your search instead.',
                    'audio-capture': 'No usable microphone was found. Check your microphone or type your search.',
                    'no-speech': 'No speech was detected. Try again or type your search.',
                    'network': 'The browser speech service could not connect. Type your search or try again later.',
                    'language-not-supported': 'This voice language is unavailable. Choose another language or type your search.',
                    'aborted': 'Voice input stopped. Your search is unchanged.'
                };
                finish(messages[event.error] || 'Voice input is unavailable. Your typed search still works.', true);
            };
            current.onend = () => { if (valid()) finish('Voice input ended without a result. Try again or type your search.'); };
            voice.setAttribute('aria-pressed', 'true');
            voice.setAttribute('aria-label', 'Cancel voice input');
            label.textContent = 'Cancel voice';
            status.textContent = 'Starting voice input. Your browser may request microphone permission.';
            timer = setTimeout(() => { if (valid()) finish('Voice input timed out. Try again or type your search.', true); }, 20000);
            current.start();
        } catch { finish('Voice input could not start. Type your search or try again.', true); }
    });
    if (!supported) {
        voice.disabled = true;
        language.disabled = true;
        status.textContent = 'Voice input is unavailable in this browser or connection. Type your search and choose Search.';
    }
    updateQuery();
    menu.hidden = false;
})();
