# Protected PDF and browser regression repairs — 2026-11-16

## Scope and diagnosis

Only private derivative processing and isolated test infrastructure changed. No application design, authorization, controllers, business workflows, database models, migrations, configuration or dependencies changed. The pre-existing logo modification is excluded.

The old PDF loop reopened the renderer for each page, encoded an intermediate PNG, decoded that PNG for stamping, then encoded another PNG for PDF embedding. The new path uses lazy native multi-page enumeration for contiguous pages of equal render dimensions and stamps the rendered bitmap directly. Mixed page sizes and order are preserved and page counts must match. PNG and PDF Flate compression use faster **lossless** settings. Existing 150-DPI/bounding calculations, stamp content, flattened output, source integrity and final authorization checks remain. Worker concurrency (two), 45-second deadline, memory guard and 40-MiB output bound were not increased. No original-file fallback or derivative cache was added.

## Measured verification

All tests use synthetic documents and isolated memory accounts. Prior audit observed a 41-page timeout during concurrent testing; this repair does not hide that failure by changing a deadline.

| Run | One 41-page copy | Two concurrent 41-page copies together |
| --- | --- | --- |
| During browser regressions | 16.34 s | 19.36 s |
| Safe service regression run | 15.85 s | 17.33 s |
| Final targeted verification | 13.33 s | 11.99 s |

Scheduling/warm-up/load differ between runs; these measurements are not a performance guarantee for arbitrary PDFs or hardware. Each protected copy stayed within the unchanged worker deadline. Tests verify all 41 flattened pages, different viewer-specific copies, unchanged original bytes, lossless decoded PNG pixels, mixed page dimensions, malformed/encrypted input rejection, cancellation and bounded inputs. The final protected-copy suite passed 39 checks.

## Browser repairs

- Footer assertions verify the current dark navy gradient/solid surface rather than an obsolete exact backgroundColor. Pointer access, sticky behavior, keyboard focus, responsive fallback, reduced motion and no-JavaScript behavior remain checked.
- Admin popup buttons must match the current parent primary control palette (background, text, border), rather than an obsolete navy value. Application CSS is unchanged.
- Notes preview hosts now have explicitly eligible employee accounts in their memory account store. Production eligibility and authorization are unchanged. Notification ownership, supplier isolation and mark-as-read assertions remain.
- The four affected suites use observable badge/menu state, loaded fonts, completed finite animations/layout frames and focused-element visibility instead of fixed sleeps. A first footer attempt exposed that animation completion alone did not mean scrolling had finished; the targeted rerun passed after waiting for focus visibility. No assertion was removed to conceal that attempt.
- Added the existing appearance, all-details, QR and face-settings browser suites to the fixed safe allowlist, bringing it to 28 groups. Fixtures still require their memory-only markers and write evidence outside the repository.

## Results

- Final solution build: zero errors and warnings. An earlier build overlapped running fixture hosts and encountered Windows DLL copy locks; after those hosts stopped, the build succeeded. No user application process was terminated.
- 27 safe service/MVC regression groups passed, including authentication, employees/departments, audit repair, supplier files, bids, role isolation, compliance, evaluation/report/decision rules, inquiries, notifications, exports, voice, passkeys, face settings and extraction.
- All 28 safe browser groups passed after the targeted footer rerun. The original attempt history remains in external evidence; this was not one entirely green first attempt.
- Additional browser evidence: 74 appearance checks, 990 checks across 13 Details views, 165 QR checks, and 16 simulated-camera/local-model checks passed.
- EF model/snapshot verification passed offline. No SQL writes, SQL migration application, seeding or provisioning occurred.

## Device/service readiness and limits

- Camera: existing local library/models load and reject a blank frame. Simulated permission denial/cancellation, aspect ratio, descriptor-only payload, enrollment and verification passed. Physical face recognition and liveness are not certified. The existing production camera flag remains disabled by default; no database activation was performed.
- Microphone: browser voice state/error/cancel/fallback tests passed. Actual microphone recognition, browser speech-service availability and device permissions were not demonstrated.
- Passkeys: native virtual-authenticator/HTTPS-origin/security tests passed. Real Windows Hello face/fingerprint/PIN hardware was not exercised. Deployment still needs the existing configured HTTPS origin; Development has its existing localhost fallback.
- Ollama: service/failure/authorization tests passed using the isolated model boundary. The actual local endpoint was unavailable; no model/service was started, installed or claimed to pass.
- Linux/macOS runtime tests and SQL write end-to-end tests were not performed. Complex documents remain subject to existing resource/output bounds.

## Files

- src/SourcingTenderManagementSystem.Web/Data/DocumentWatermarkWorker.cs
- tests/SourcingTenderManagementSystem.AuthenticationChecks/AccessCopyChecks.cs
- tests/SourcingTenderManagementSystem.AuthenticationChecks/BidPhase3Checks.cs
- tests/Run-BrowserExperience.cjs
- tests/browser-waits.cjs
- tests/sticky-footer-browser.cjs
- tests/admin-shell-browser.cjs
- tests/notification-browser.cjs
- tests/notes-browser.cjs
- docs/pdf-browser-regression-fixes.md
