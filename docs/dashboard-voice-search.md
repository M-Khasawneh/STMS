# Employee dashboard voice search — V5 section 10.3

## Delivered

V5 requires speech-to-text search with SpeechRecognition on the Employee Dashboard task list. The current role dashboard already derives permitted recent work records from Identity, ownership, department review exclusions and effective tender committee assignments. This increment adds typed and voice filtering to those existing rendered rows, rather than creating a new task entity, workflow or access policy.

The employee-only Razor view renders the control when dashboard data is available. Admin and Supplier dashboards do not receive the control or its assets. Search covers visible recent record titles, statuses, dates and section names. It deliberately does not claim a global search of older records: the UI directs users to the existing workspaces for older records. KPI counts and source records remain unchanged.

- Native SpeechRecognition or webkitSpeechRecognition, one explicit single-utterance session at a time; English or Arabic (Jordan).
- Text input works when recognition is unsupported, denied or unavailable. No automatic microphone activation, fake transcript, simulated audio waveform, external transcription integration or application audio storage.
- Final recognition results populate the same bounded 160-character query; all words must match. Unicode/Arabic marks are normalized. Transcript is assigned as input value, never HTML or a command/URL.
- Explicit cancel, Escape, typing, language changes, hidden page, page exit and a 20-second timeout abort the session. Late callbacks cannot overwrite a newer or cancelled search.
- Permission, microphone, network, no-speech, language and start errors recover without altering the stored records.
- Accessible labels and status announcements, keyboard focus, responsive controls and reduced-motion listening feedback. Without JavaScript, all original dashboard records remain visible and the unavailable enhancement stays hidden.

## Files

Views/Workspace/Roles.cshtml; wwwroot/css/dashboard-search.css; wwwroot/js/dashboard-search.js. No controllers, domain services, entities, authentication rules, migrations, SQL writes or dependencies changed.

## Verification

- Solution/Razor build: zero warnings and errors.
- --voice-search-checks-only: 13 compiled MVC audience/data/no-store checks using memory stores only.
- --workspace-ui-checks-only: 59 existing role/navigation checks passed.
- tests/voice-search-browser.cjs: 38 Edge browser checks on a marked memory-only fixture. Covers final transcript filtering, Arabic normalization, cancellation/late callbacks, permission and network errors, timeout, typed fallback, safe transcript handling, keyboard operation, no-JS fallback, role audience, 1440/768/390/320 px overflow and reduced motion.
- Browser recognition events are injected ONLY by the test harness. This does not prove actual microphone capture or speech-service accuracy. Actual English/Arabic microphone recognition requires manual verification in a supported browser with user permission and service connectivity.

## Browser constraints and references

The browser speech service may process audio remotely and needs browser/device/service support. The interface states this before activation. Unsupported configurations retain typed search; STMS does not introduce a separate speech provider or store recordings.

Primary API references: https://developer.mozilla.org/en-US/docs/Web/API/SpeechRecognition and https://developer.mozilla.org/en-US/docs/Web/API/SpeechRecognition/abort .
