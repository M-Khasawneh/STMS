# Workspace voice search and role dashboards

## Current behavior — 2026-11-15

The user expanded the original employee-dashboard voice input to **all authenticated internal pages and all existing roles**. `_WorkspaceSearch.cshtml` is rendered once by the shared authenticated layout, immediately beside Notifications. Public Home/About/Contact/Login/registration pages and standalone printable reports do not acquire a private search control. Existing modal form frames retain their intentional embedded layout; their parent workspace header remains the shared entry point outside the modal.

This is now a global search entry point, not a filter over dashboard rows. Its ordinary GET form submits `q` to the existing `Work/Search` MVC action. Result visibility, ownership, supplier isolation, department rules and committee/stage restrictions remain in the existing WorkspaceDiscovery service and destination services. No new endpoint, API, role or permission was introduced. The user reviews recognized words and explicitly chooses Search; recognition never automatically navigates or executes commands. The existing two-character minimum and 160-character limit apply. Typed search remains available without speech support, and the ordinary workspace-search link remains available without JavaScript.

The closed header popover focuses text input on opening without activating the microphone. Use voice explicitly starts one native SpeechRecognition/webkitSpeechRecognition session, English or Arabic (Jordan). Browser-generated terminal punctuation/quotes are trimmed from recognized input; internal punctuation such as record-ID hyphens is preserved. Typed input retains the existing search semantics. Transcripts are assigned to input values, never inserted as HTML or URLs.

Cancel, Escape, close, outside interaction, keyboard departure, typing, language changes, hidden page, page exit and a 20-second timeout stop capture. Session guards reject late callbacks. Permission, device, network, language, empty/no-speech, constructor/start/abort failures recover while preserving typed input. Search and notification popovers dismiss one another. Keyboard labels/focus, mobile containment and reduced motion remain supported. No application audio upload/storage, external transcription integration, new dependencies or automatic microphone activation.

## Non-Admin dashboard composition

The existing `RoleDashboardModel.Sections` remain the data source. Employees, suppliers, officers, managers and committee members share a calmer layout: a modest heading, one My tasks link, grouped existing metrics, readable recent-record lists and a separate personal-notification summary. Desktop uses a primary column plus a narrow notification column; tablet/mobile stack the same content. Single metrics and mobile metrics use compact horizontal rows instead of oversized individual cards. Multi-role sections remain visible; no metrics are combined across roles and no charts, totals or work items are fabricated.

Every existing metric count, destination, title, status and timestamp remains sourced from the original model. A record with no authorized destination remains plain text. Unavailable data stays an explicit warning. The existing Admin dashboard is unchanged apart from the shared search entry point. No production controllers, services, models, role policies, database schema, migrations or records were changed.

## Files

- Shared/_Layout.cshtml and Shared/_WorkspaceSearch.cshtml: one global authenticated search control and its assets.
- wwwroot/js/dashboard-search.js and wwwroot/css/dashboard-search.css: progressive search, recognition lifecycle, popover and responsive controls.
- Workspace/Roles.cshtml and wwwroot/css/role-dashboard.css: non-Admin dashboard presentation.
- Test-only VoiceSearchChecks/BidPhase3Checks registration and browser checks: marked memory fixtures for the existing Work/Search action and representative role dashboards; fixture counts/records are synthetic and never seeded into SQL.

## Verification and limits

The global browser test covers seven roles on Dashboard, Reports, My tasks and Workspace search; explicit voice-to-query-to-MVC navigation; native form validation; error/cancellation/stale-result recovery; safe transcript handling; unsupported/insecure/prefixed API fallback; keyboard/reduced motion; public/no-JavaScript behavior; and desktop/mobile dashboard composition. The compiled MVC checks cover 36 audience/markup/no-store/other-page assertions.

Recognition events in these UI tests are injected deliberately and do not prove physical microphone capture or transcription accuracy. In the previous native probe, installed Edge and Chrome 157 processed a synthetic English WAV under en-US/ar-JO configurations, but **0 of 4 attempts returned a nonempty transcript**. Native microphone-denial handling passed in both browsers. The updated native probe opens the shared header and checks the global form; these UI changes do not re-certify the browser speech service. Successful human English/Arabic transcription on a supported browser with permission and a working speech service remains device acceptance.

Browser SpeechRecognition has limited availability and may use a remote browser-vendor service. STMS states this before activation; microphone permission alone does not guarantee provider availability. Primary references: https://developer.mozilla.org/en-US/docs/Web/API/SpeechRecognition and https://developer.mozilla.org/en-US/docs/Web/API/SpeechRecognition/abort .

QA screenshots, logs and coverage stay outside Git under the local `global-voice-qa` directory. No SQL write tests, migrations, production submissions, external speech providers or new libraries were used.

Final verification for this revision: solution/Razor build passed with warnings treated as errors (zero warnings/errors), all 24 safe-acceptance groups passed and all 23 browser suites passed. The rewritten global voice/role-dashboard suite passed 188 assertions with zero page JavaScript errors, including notification-side-panel rendering for all six non-Admin roles. The 36 compiled voice MVC checks passed. This certifies the tested UI/navigation scenarios, not real microphone transcription or production SQL persistence.
