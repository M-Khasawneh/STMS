# Shared UI notes review — 2026-11-16

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
