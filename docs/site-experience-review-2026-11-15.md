# Site experience verification — 2026-11-15

Scope: review the existing STMS site, repair reproducible interaction/responsive defects, and preserve its architecture, identity, routes, permissions and business rules. No database commands, migrations, production submissions, dependency installation or new application features were needed in this pass.

## Confirmed defects repaired

- **Keyboard access to the sticky homepage footer:** focus could move to a link below the visible viewport while global smooth scrolling delayed its reveal. The footer now resolves its normal-flow focus state and scrolls synchronously. Browser checks cover keyboard entry, pointer access, mobile/short viewports, reduced motion and JavaScript-disabled fallback.
- **Mobile evaluation criteria:** a more-specific two-column CSS rule overrode the mobile rule. Fields occupied only half the available space even though the page passed a basic horizontal-overflow check. The mobile selector now restores a full-width single column. The browser regression also checks actual field width, rather than overflow alone.
- **Mobile passkey page:** the title and profile-settings link competed for the same narrow header row. The header now stacks on small screens; the regression checks that the action sits below the title.
- **Requisition date guidance:** removed the obsolete “server calendar” wording. The established business-date calculation and validation remain unchanged.

## Page and scenario coverage

There are 77 non-partial Razor files. All 76 active page templates were rendered in the browser. `Views/Workspace/Index.cshtml` is an unused legacy template: the actual Workspace Index action explicitly returns Admin or Roles, so it was excluded instead of adding a route to expose it.

Coverage includes:

| Area | Pages/states checked |
| --- | --- |
| Public | Home, About, Contact, Privacy, Login, supplier registration and generic receipt |
| Administration | Role dashboards, employees/departments, supplier administration, messages, notifications, profile and passkeys |
| Requisitions/tenders | Lists, creation/editing forms, details, criteria, invitation administration, inbox and invitation details |
| Offers | My Offers, draft editor, documents, details, submit confirmation, history, personal summary, supplier tender portal and internal offer comparison |
| Evaluation/decisions | Compliance, committee configuration, evaluation/confirmation, unified report/details/submission/manager review, Top Committee list/details/outcome and confirmed vote |
| Supporting pages | Reports/print/exports, AI analysis, work tasks/search/audit, inquiry inbox/conversation, access denied and error/problem pages |

Desktop/tablet/mobile widths: 1440, 768, 390 and 320 px. The dedicated presentation sweep covers 34 pages at all four widths, 16 additional lifecycle states, empty lists and required-field feedback. It detects horizontal overflow, missing visible input labels, duplicate IDs, missing main headings and the two confirmed mobile layout regressions. Other suites exercise real navigation, keyboard menus, modal forms, draft/submission flows, private watermarked downloads, notifications/read state, report exports, committee voting, reduced motion and native Identity login against isolated memory stores.

## Test infrastructure and interpretation

- `tests/Run-BrowserExperience.cjs` runs a fixed allowlist of 22 memory-only browser suites. Each fixture must emit its expected marker. It does not run the historical SQL-writing browser scripts.
- `UiPagePreviewController` and `BrowserViewCoverage` are compiled only into the test assembly. They add no production endpoints or authentication bypasses. Coverage records view/role/render mode/status, never model data or user identifiers.
- Some SQL-coupled pages use synthetic **presentation-only** models. Rendering those views proves Razor/layout/client behavior, not their controller's SQL persistence. Their preview suite blocks POSTs. Other suites use real MVC controllers with memory services/stores; these are recorded separately as `mvc-memory`.
- Existing download tests were updated for the current “Download watermarked copy” control and generated filenames. Synthetic PDFs are now valid, renderable PDFs rather than signature-only dummy bytes. No document-security checks were relaxed.
- The notification test now selects its populated memory-dashboard state explicitly. The native-login test identifies the password button explicitly alongside the existing passkey button. The generic registration receipt is checked on the fixture that supplies its controller dependencies.

## Verification evidence

- Final solution build succeeded with warnings treated as errors: zero warnings/errors.
- All 24 safe acceptance suites passed, including schema/model, Identity/employees/departments, bid/document security, inquiries, contact/messages, evaluation/decision rules, exports and passkey protocols.
- All 22 browser suites passed after targeted corrections/rechecks. No page JavaScript errors or outstanding presentation-sweep findings were reported. The final native-login/role experience suite passed 206 checks; work/notifications/audit/supplier portal passed 387 checks; the Razor presentation sweep passed 152 populated/lifecycle checks plus empty-state and required-field checks.
- Coverage across successful browser runs: 46 distinct templates reached through real MVC actions with memory-backed dependencies, plus 30 templates covered only through the presentation fixture. This distinction is intentional; the total is 76 active templates, not 76 SQL workflow certifications.
- All 20 application JavaScript files passed syntax checks. Final targeted reruns covered the updated mobile layouts, registration receipt, notification page, password login and profile popup.

QA logs, screenshots and coverage JSON remain outside Git under the local `site-experience-qa` directory. The 21st UI review workflow guided the audit; its CLI was unavailable, so no `21st review` execution or score is claimed.

## Limits

This is evidence for the tested scenarios, not a claim that every input, device or concurrency schedule is error-free. Production SQL writes were not authorized for this task and were not executed. Full SQL-backed form submissions remain distinct from memory tests and render previews. Actual face/fingerprint hardware, microphone recognition, deployed HTTPS configuration and browser/device acceptance still require their respective environments. Existing SQL/model verification from the preceding technical review is documented separately in `technical-review-2026-11-15.md`.
