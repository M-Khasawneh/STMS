# Shared UI notes review — 2026-11-16

## Site-wide reading rhythm, roof motion and navigation work

- Added a shared readability layer to the existing layout for list, create/edit, review, confirmation and account pages, plus public contact/registration/login forms. It increases body/table/label legibility, standardizes spacing and separates form actions. Existing Details composition remains scoped and unchanged. This uses the existing MVC routes and tokens, with no new framework or business functions.
- The hero now renders the original architectural SVG inside a shared SVG coordinate system. A native Web Animations highlight follows the exact original roof path; it has an explicit pause/resume control, stops offscreen/on hidden documents, and becomes static for reduced motion. The background and moving stroke cannot drift apart on responsive reflow. No GSAP or dependency was added.
- Removed whole-page exit dimming/movement during native navigation, while retaining short entrances. Embedded form frames no longer start their own notification menu/live connections: the visible parent bell remains the owner. No prefetching of authenticated routes, PJAX, history interception or duplicate form submissions was introduced.
- Verification: solution build, zero warnings/errors; 38 native motion/request/navigation checks; 34-page / 152-case presentation sweep; 395 shared role/theme checks; 171 form/layout regression checks; 335 public composition/navigation checks; and 101 synchronized journey checks. No findings/JavaScript errors in the completed runs. The request test observed five parent notification requests across its navigations and zero from the embedded form. A simulated pending-navigation click kept the old page fully opaque, and the subsequent real MVC navigation completed.
- Timings are from the synthetic memory-only fixture, not a production SQL benchmark. No percentage speedup or universal live-latency guarantee is claimed. The shared styles cover routes using the existing layout; not every possible production record, validation state or POST action was exercised. No database writes, migrations, backend changes or dependencies were introduced.

## All 13 MVC Details actions

The shared layout now provides a scoped Details workspace for every authenticated Details action, with existing requisition composition preserved. A dedicated stylesheet standardizes readable headings/body copy, metadata grids, section dividers, document rows, action spacing, vertical histories, role-appropriate section navigation and dark-theme surfaces. Long offer/report pages read as documents rather than stacks of nested cards. Tender details now separate their main content from a vertical status rail and use the compact protected QR action. Supplier details separate company/documents from the existing decision form; popup behavior, status conditions, version fields and antiforgery remain unchanged.

Covered views: Requisitions, Tenders, Suppliers, SupplierPortal, Bids, ReviewBids, Compliance, Evaluations, UnifiedReports, TopCommittee, AiAnalysis, Clarifications and Messages. The new test compares this list to the actual `Views/*/Details.cshtml` files, preventing accidental omissions. Ten additional synthetic models were added only to the existing test preview controller; no new production endpoints or demo data were introduced.

Final verification: build succeeded without warnings/errors; 990 checks across all 13 Details views, two themes and four viewport widths, including solid-surface text contrast, responsive overflow, heading hierarchy, field labels, antiforgery and metadata layout; 395 shared UI checks; 199 existing record-guidance/tender-state checks; 165 QR enlargement checks; and the 34-page / 152-case presentation sweep. Final review fixed inherited low-contrast banner links, light navigation panels, evaluation fieldset surfaces and tender-stage captions. No remaining findings or JavaScript errors in the tested presentations. Real SQL acceptance, physical scanning and every possible live workflow state were not exercised. No production controller, service, permission, entity, migration or database was modified.

## Requisition detail composition rewrite

The screenshot feedback called for a composition change rather than another layer of card polish. Requisition details now use a main content column and a compact status rail on desktop, stacking on smaller screens. Summary metadata uses a consistent two-column grid (one on very narrow phones), with larger readable content. Items remain the original responsive table. Actual change history is vertical within one activity section; review decisions and comments remain available in an accessible disclosure. All seven states remain in the existing progress component, and the duplicate status-reference component was removed. QR is a compact header action opening the existing protected enlargement dialog. All modifying forms, versions, validations, ownership conditions and review actions remain intact.

An optional final PageStyles section lets this scoped composition load after shared shell styles. Regression expectations were updated for the intentionally changed composition, while retaining data, role, antiforgery and current-state assertions. Build passed with zero warnings/errors. Browser checks: 171 notes/layout/form checks, 499 state/dashboard/greeting checks, 395 shared consistency checks, 233 prior-note regression checks, 165 QR/dialog checks, and the 34-page/152-case presentation sweep. Realistic desktop and mobile screenshots were inspected. Tests used synthetic in-memory records; no SQL writes or migrations.


## Detail pages and QR enlargement

- A shared detail-page stylesheet applies to authenticated Details, Status, Invitation and Outcome presentations. It refines panel hierarchy, metadata grids, headings, document rows and action spacing without rebuilding or changing any forms, permissions, routes or data. Mobile metadata stacks instead of becoming cramped. Empty history containers are hidden.
- Existing requisition/tender QR thumbnails now open a native modal with a large black-on-white image, original protected download link, close control, Escape, native focus containment and focus restoration. Failures display a readable message; no third-party QR service or dependency is used. Without JavaScript/dialog support, the original endpoint link remains usable.
- The generator already encodes the module and record ID in the canonical URL. New tests confirm different records and different modules produce different PNGs; the same record's QR remains stable. Production QR generation and authorization were preserved.
- Verification: solution/Razor build, zero warnings/errors; 165 modal/detail browser checks at four widths and two themes (load, keyboard focus, Escape, reopening, download identity and error handling); 34-page / 152-case presentation regression; 395 role/theme consistency checks; 34 database-free QR/private-copy tests. No findings or JavaScript errors, no SQL writes or migrations. Synthetic presentation records use a fixture-only endpoint with the real generator and their exact module/ID; this does not bypass production authorization. A physical phone-camera scan against a deployed, reachable HTTPS origin was not performed.

## Latest document: all statuses, Admin metrics and sign-in welcome

- Show all seven existing requisition statuses beside Change history, highlighting the persisted current status. This is explicitly a status reference; no synthetic audit events or dates are inserted. Existing real history is preserved.
- Admin totals now use three columns on desktop/tablet, with two on narrow mobile screens. All six bound totals and their existing routes remain unchanged.
- A one-time welcome is set only after successful password/passkey authentication and rendered using the authenticated account name. It works after local return redirects as well as the default workspace redirect, and does not recur on every navigation. Authentication rules and eligibility checks are unchanged.
- Verification: zero-error/warning solution build; 499 focused browser checks across states, themes, screen widths and seven role greetings; 44 Admin dashboard regression checks; 147 existing notes checks; and 18 memory-only Identity/passkey checks including successful password welcome and one-time consumption. No SQL writes or migrations.

Source: the user's `New Microsoft Word Document (2).docx`, including its five screenshots.

## Corrections

- Shared device sign-in surface: rounded perimeter, internal spacing, wrapping controls and readable help text in both themes.
- Requisition history: rounded outer container with transparent timeline entries. Prevent the dark-theme panel rule from turning each entry into a square block; preserve the actual current-state glow and horizontal scrolling.
- Requisition and tender details: show the existing protected QR near the heading, before item tables, instead of hiding it inside a disclosure at the end. Existing QR authorization and download actions are unchanged.
- Report tables: numeric headings and their values share end alignment, including participation counts and percentages. Avoid a second nested table border inside the results panel.
- Commercial report screen and native print view: display the actual currency beside its amount, omitting the redundant standalone currency column. The source report data, XLSX/PDF export services and monetary calculations are unchanged.
- Sidebar: add matching SVG icons to the previously unadorned company, invitation, offer and final-decision links. Preserve all existing visibility conditions and destinations.

## Verification

- Solution/Razor build: zero warnings and errors using a separate local output directory. The normal output was locked by the running Visual Studio/application session; that session was left running.
- UI consistency: 395 checks, seven role workspaces, both themes, responsive widths, search interactions, text contrast and dark surfaces; no findings or JavaScript errors.
- Page sweep: 34 Razor pages, 152 populated/empty/workflow-state render checks, responsive layouts, labels, table scrolling and keyboard access; no findings or JavaScript errors.
- Existing notes regression: 147 checks, including forms, multi-role binding, timeline lighting, popup presentation and validation.
- Reports browser regression: 40 checks, including native print and download interactions.
- Focused shared-notes regression: 233 checks, including all visible sidebar links, numeric heading alignment, combined currency, QR placement, device sign-in and dark timeline entries.
- Reporting rules/MVC/export regression: 160 database-free checks. QR/private-copy regression: 31 database-free checks.

All browser runs targeted a marked memory-only MVC host. Modifying requests were blocked in the review sweeps. Synthetic presentation records do not exist in the protected record store, so the focused visual test serves QR images from a fixture-only endpoint using the existing real QR generator; production QR authorization remains independently tested. No production database writes, migrations or dependencies were introduced.

This is a broad presentation review, not a claim that every possible production record, device or business workflow was exercised. SQL-backed acceptance and a live end-to-end run against real accounts were not performed. The optional `21st` review CLI was not installed/available; the project browser and build checks supplied the verification evidence.

## Follow-up document revision

The next revision supplied four screenshots: tight profile sections in the popup, square search focus, square identity focus, and a tender rail colored after the saved stage.

- Profile sections now use a grid gap (28 px desktop / 24 px mobile), avoiding the popup rule that forces panel margins to zero. Section padding and rounded borders remain consistent in the standalone and iframe presentations.
- Header search uses a rounded focus-within ring around the whole search control; the input no longer draws a second square outline. The profile identity link has its own rounded, visible keyboard outline. No general keyboard focus indicator was removed.
- Tender connectors now use individual rails filled only before the persisted current stage. The outgoing rail stays neutral and the final node has no trailing rail. The same direction applies to the requisition main track and recorded change history. Alternative outcomes are kept separate; cancellation does not invent a completed path. This is a position indicator, not evidence of historical completion.
- The tender track fits the existing desktop content width while retaining horizontal keyboard scrolling on smaller screens.

Verification: solution build succeeded with zero warnings/errors in a separate local output; 765 focused standalone checks across all seven roles, two themes and four widths; 169 additional checks of the real iframe embedding across seven roles, two themes and three widths; 395 shared consistency checks; the 34-page / 152-case sweep; and 199 existing tender-state/navigation/file-selection checks. No findings or JavaScript errors. All checks used the marked memory-only fixture; no SQL writes, backend changes, migrations or new dependencies. The optional 21st CLI remains unavailable. Presentation fixtures do not persist their synthetic records, so their real protected QR URLs cannot resolve those synthetic record IDs; this revision did not alter QR behavior.
