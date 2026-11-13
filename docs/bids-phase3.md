# Supplier bids — Phase 3 MVC

## Delivered

Supplier-facing MVC pages now wrap the Phase 1 domain service and Phase 2 private-document service. No internal review pages, audit/notification features, schema changes, migrations, new packages, or procurement workflows were added. The original V5 requirement for downloadable personal summary reports does not specify PDF; the export is a UTF-8 `.txt` attachment.

| Route/action | Behavior |
| --- | --- |
| `GET /Bids` | My Offers: only the approved supplier's bids, tender title, status, local submission timestamp, permitted edit link, details and summary links. Empty state links to existing invitations. |
| `GET/POST /Bids/Create?tenderId=…` | Eligible accepted invitation required. Authoritative requested items/quantities, JOD prices, commercial and technical terms, and optional nonnegative charges. Saves Draft only. |
| `GET/POST /Bids/Edit/{id}` | Owning supplier's eligible Draft only. Commercial edits use the current bid version. Add/remove charge commands prepare unsaved form rows, with a server fallback when JavaScript is unavailable. |
| `POST /Bids/Upload/{id}`, `Replace/{id}?documentId=…`, `RemoveDocument/{id}` | Existing private-document operations; no filesystem or eligibility rules duplicated in the controller. One file per upload/replacement; max 5 MiB/file and ten files/bid. |
| `GET /Bids/Details/{id}` | Own saved offer, exact server totals, terms, documents, deadline-related edit eligibility, and read-only submitted content. |
| `GET/POST /Bids/Submit/{id}` | GET performs a read-only, locked confirmation check using real private files. POST requires explicit confirmation and repeats all domain/file/version/deadline checks in the submission transaction. Duplicate submission fails. |
| `GET /Bids/Document/{id}?documentId=…` | Own verified bytes, attachment disposition, octet-stream MIME, no-store, nosniff and sandbox headers; MVC disposes the stream. |
| `GET /Bids/Summary/{id}` | Own saved offer only, with item prices/totals, charges, terms, document names and timestamps. No storage references, physical paths, embedded document contents or competing supplier data. Draft reports are clearly marked. |

All timestamps display through the existing Asia/Amman `TenderTime` helper. Export amounts use invariant decimal formatting; calculated totals preserve available precision rather than silently rounding. The browser uses fixed-point BigInt arithmetic for live previews; server decimal arithmetic remains authoritative. No posted supplier, status, quantity, currency or total is trusted.

## Security and integration

`BidsController` requires the Supplier role and rechecks the existing database-backed eligible supplier predicate before every action. `SupplierBidPages` also guards its public operations so supplier exports cannot inherit broader internal bid-read rights. Existing bid/document services retain ownership and transactional enforcement. Access failures produce 403/404; stale or ineligible mutations produce an explanatory error page instead of silently refreshing and retrying an outdated form.

Every modifying action is POST-only and covered by explicit controller-level automatic antiforgery validation, in addition to the unchanged application-wide filter. Razor generates antiforgery inputs. Form binding accepts only the existing draft input model using the `Form` prefix; display-only tender/items/document metadata are composed on the server. Invalid values and validation messages redisplay without saving. A file-selection error requires the user to choose the file again.

The existing shared layout adds only a supplier My Offers link. Accepted invitation details link to offer preparation and My Offers. Identity, completed controller workflows, public pages, models/entities, database configuration, and migrations remain unchanged.

## Frontend

The new isolated `bids.css` supplies Navy/Blue/Cyan panels, pricing tables, progress labels, document controls and responsive layouts. It preserves the existing shared shell. Existing page/table motion and reduced-motion handling are reused. Long-form focus scrolling is immediate to avoid moving controls during validation. Visually hidden table labels remain contained within their horizontal scroll regions after viewport changes.

`bids.js` adds exact live totals, optional cost rows, inline correction of previously reported errors, file size/extension feedback, and a one-send final confirmation guard. It preserves named submit controls and native server POSTs. Unsaved commercial edits prompt before leaving and prevent document mutations/review navigation until saved; without JavaScript the page still clearly states that document operations and submission use saved content. JavaScript does not supply security enforcement.

## Files

Created:

- `src/SourcingTenderManagementSystem.Web/Controllers/BidsController.cs`
- `src/SourcingTenderManagementSystem.Web/Data/SupplierBidPages.cs`
- `src/SourcingTenderManagementSystem.Web/Models/Bids/SupplierBidPages.cs`
- `src/SourcingTenderManagementSystem.Web/Views/Bids/`: `_ViewImports.cshtml`, `Index.cshtml`, `Editor.cshtml`, `_Documents.cshtml`, `_OfferContent.cshtml`, `Details.cshtml`, `Submit.cshtml`, `Problem.cshtml`
- `src/SourcingTenderManagementSystem.Web/wwwroot/css/bids.css`
- `src/SourcingTenderManagementSystem.Web/wwwroot/js/bids.js`
- `tests/SourcingTenderManagementSystem.AuthenticationChecks/BidPhase3Checks.cs`
- `tests/bid-browser.cjs`
- `docs/bids-phase3.md`

Modified:

- `src/SourcingTenderManagementSystem.Web/Data/BidManagement.cs`: extracted the unchanged submission validations into a shared helper and added a read-only confirmation operation; exposed the existing participation check internally for page composition. Final submission still repeats validation in its own transaction.
- `src/SourcingTenderManagementSystem.Web/Program.cs`: registered the supplier page-composition service.
- `src/SourcingTenderManagementSystem.Web/Views/Shared/_Layout.cshtml`: My Offers navigation link only.
- `src/SourcingTenderManagementSystem.Web/Views/Tenders/Invitation.cshtml`: links for an Accepted invitation only.
- `tests/SourcingTenderManagementSystem.AuthenticationChecks/Program.cs`: explicit database-free Phase 3 modes returning before SQL suites.

## Verification

- Solution/Razor build: **passed, zero warnings/errors**, isolated artifacts outside the application build folders.
- `--bid-phase3-checks-only`: **61 MVC checks passed**, including compiled Razor rendering, routes, prefixed binding, manipulated item IDs, ignored ownership/status/quantity inputs, numeric validation, optional charges, stale versions, antiforgery, multipart files, private download headers/bytes, supplier isolation, explicit confirmation, real-file preview, immutability and duplicate submission.
- The same safe mode also passed **97 Phase 2 checks**, **92 Phase 1 checks**, and existing tender/requisition rule and supplier-upload checks.
- Schema runner without flags: **passed**, existing four migration definitions and 36 required tables verified without SQL access.
- Existing installed Edge/Playwright: **36 browser checks passed** at 1440/768/390/320px, including precise live totals, dynamic costs, file feedback, dirty-form guard, upload/download, explicit submission, summary attachment, mobile menu/Escape, no page overflow, reduced motion, and **zero JavaScript errors**. Screenshots/results are external QA artifacts; no test data or screenshots are added to production content.
- The test host registers memory bid data and ephemeral data-protection only; it registers no SQL DbContext. Its header/cookie simulation is confined to the test assembly and is never registered in the application. Browser checks require the fixture marker before any mutation. Temporary private files are removed when fixtures finish.

## Limits / next phase

No SQL Server commands, write tests, migrations, production account changes or database updates were performed. Live SQL persistence/locking and the new pages under production Identity cookies are not end-to-end verified here; the HTTP/browser tests use isolated authentication and memory persistence. The production Identity configuration is unchanged. Real SQL and production-environment integration require separate approval and safe infrastructure.

Internal review screens, audit-history features, notification work, and later-phase integration verification are not implemented. Phase 3 is complete; stop for approval before Phase 4.
