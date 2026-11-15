# Employee dashboard voice search — V5 section 10.3

## Delivered

V5 requires speech-to-text search with SpeechRecognition on the Employee Dashboard task list. The current role dashboard already derives permitted recent work records from Identity, ownership, department review exclusions and effective tender committee assignments. This increment adds typed and voice filtering to those existing rendered rows, rather than creating a new task entity, workflow or access policy.

The employee-only Razor view supplies a microphone icon beside the header notification bell when dashboard data is available. It opens a search popover; no large search section remains in the dashboard content. Admin and Supplier dashboards do not receive the control or its assets. Search covers visible recent record titles, statuses, dates and section names. It deliberately does not claim a global search of older records: the UI directs users to the existing workspaces for older records. KPI counts and source records remain unchanged.

- Native SpeechRecognition or webkitSpeechRecognition, one explicit single-utterance session at a time; English or Arabic (Jordan).
- Text input works when recognition is unsupported, denied or unavailable. No automatic microphone activation, fake transcript, simulated audio waveform, external transcription integration or application audio storage.
- Final recognition results populate the same bounded 160-character query; all words must match. Unicode/Arabic marks are normalized. Transcript is assigned as input value, never HTML or a command/URL.
- Punctuation is treated as word separators in both record text and queries, so speech-service punctuation such as `Network renewal.` or `مراجعة الأجهزة؟` does not hide the matching record.
- Explicit cancel, Escape, typing, language changes, hidden page, page exit and a 20-second timeout abort the session. Late callbacks cannot overwrite a newer or cancelled search.
- Permission, microphone, network, no-speech, language and start errors recover without altering the stored records.
- Accessible labels and status announcements, keyboard focus, responsive controls and reduced-motion listening feedback. Without JavaScript, all original dashboard records remain visible and the unavailable enhancement stays hidden.

## Files

Views/Workspace/Roles.cshtml; Views/Shared/_Layout.cshtml (optional HeaderTools section); wwwroot/css/dashboard-search.css; wwwroot/js/dashboard-search.js. No controllers, domain services, entities, authentication rules, migrations, SQL writes or dependencies changed.

## Verification

- Solution/Razor build: zero warnings and errors.
- --voice-search-checks-only: 13 compiled MVC audience/data/no-store checks using memory stores only.
- --workspace-ui-checks-only: 59 existing role/navigation checks passed.
- tests/voice-search-browser.cjs: 38 Edge browser checks on a marked memory-only fixture. Covers final transcript filtering, Arabic normalization, cancellation/late callbacks, permission and network errors, timeout, typed fallback, safe transcript handling, keyboard operation, no-JS fallback, role audience, 1440/768/390/320 px overflow and reduced motion.
- Browser recognition events are injected ONLY by the test harness. This does not prove actual microphone capture or speech-service accuracy. Actual English/Arabic microphone recognition requires manual verification in a supported browser with user permission and service connectivity.

## Browser constraints and references

The browser speech service may process audio remotely and needs browser/device/service support. The interface states this before activation. Unsupported configurations retain typed search; STMS does not introduce a separate speech provider or store recordings.

Primary API references: https://developer.mozilla.org/en-US/docs/Web/API/SpeechRecognition and https://developer.mozilla.org/en-US/docs/Web/API/SpeechRecognition/abort .

## Focused verification follow-up — 2026-11-15

Reproduced and fixed a search defect: a final transcript of `Network renewal.` showed zero results for the existing `Network renewal` row. Normalizing punctuation consistently in rows and queries restores the match without modifying the transcript, source data, permissions or search scope.

- Expanded `tests/voice-search-browser.cjs`: 59 passing checks, zero page JavaScript errors. Added English/Arabic punctuation, interim/empty/long transcripts, stale sessions, all documented error paths plus unknown errors, clear/hidden-page cancellation, keyboard activation, prefixed API, insecure connection, constructor/abort failures and typed fallback. These recognition events are deliberately simulated and are not evidence of transcription accuracy.
- `tests/voice-search-native-browser.cjs` observes the installed Edge and Chrome native recognizers without injecting results. It always uses browser fake-capture flags and an explicitly supplied external synthetic WAV; it never opens the physical microphone. The English phrase was generated locally through the existing Windows speech synthesizer, with no new software installed. Both `en-US` and `ar-JO` configurations were exercised with that same English fixture; this is not an Arabic-speech accuracy test.
- Native results on installed Edge/Chrome 157: audio/speech lifecycle events occurred, but **0 of 4 capture attempts returned a nonempty transcript**. Edge returned an empty result; Chrome ended without a result. Both recovered with truthful feedback and preserved the previous typed query. These runs do not establish why the browser service returned no transcription and do not prove end-to-end voice recognition works.
- Two additional native permission-denial probes, one per browser, produced `not-allowed`; the UI displayed microphone-permission guidance and retained working typed search.
- The final solution/Razor build passed with warnings treated as errors (zero warnings/errors); all three affected JavaScript files passed syntax checks. All 13 database-free MVC audience/no-store checks passed again.
- No SQL operations, migrations, physical recordings, external transcription integration, dependencies or role changes were introduced. QA WAV files, logs and screenshots stay outside Git under the local `voice-qa` directory.

Remaining acceptance: successful human English/Arabic transcription on a supported browser with a real microphone and working speech service. Browser-native SpeechRecognition has limited availability and may rely on a remote service; application filtering and failure recovery do not guarantee provider availability.

## Header placement follow-up — 2026-11-15

Moved the existing employee dashboard search into a closed-by-default header popover, opened by a microphone icon immediately beside Notifications. Opening the panel focuses typed search without starting the microphone. Recording still requires the explicit Use voice button. Search and notification popovers dismiss one another. Close, outside pointer interaction and keyboard departure cancel capture; Escape first cancels recording, then closes an idle panel. Closing preserves the current filter and marks the header icon when a filter is active. No-JavaScript behavior preserves the original records and hides the unavailable enhancement.

Verification: solution/Razor build passed with zero warnings/errors; 13 voice MVC audience/no-store checks and 59 existing workspace role/navigation checks passed. The expanded browser suite passed 72 checks including placement, removal from main content, focus restoration, late callbacks after closing, notification interaction, keyboard departure, reduced motion and viewport containment at 1440/768/390/320 px. Responsive review caught and fixed left-edge clipping on tablet. The native probe was updated to open the header panel; provider transcription results from the preceding focused review were not re-certified by these interface tests. No database changes or dependencies.
