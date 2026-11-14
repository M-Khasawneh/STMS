# Supplier bids Phase 4

## Delivered

Internal read-only offer listing, details, comparison, protected document download and activity history are available through `ReviewBidsController`. The submitted-offers navigation item appears for Sourcing Officers, Sourcing Managers and committee roles. The service rechecks the current account and reuses the existing bid visibility queries. Supplier-only pages remain served by `BidsController`.

The comparison uses source requisition items and requested quantities, saved bid prices, cost components, technical/commercial terms, exact decimal totals and scoped company names. It provides no scoring or ranking actions.

| Route | Access |
| --- | --- |
| `/ReviewBids` | Authorized submitted offers, optionally filtered by tender ID |
| `/ReviewBids/Details/{id}` | Read-only submitted offer |
| `/ReviewBids/Compare?tenderId=...` | Available submitted offers for one tender |
| `/ReviewBids/Document/{id}?documentId=...` | Existing private-file verification and attachment headers |
| `/ReviewBids/History/{id}` | Activity for an offer the account may currently read |
| `/Bids/History/{id}` | Owning approved supplier only |

Sourcing Officers and Sourcing Managers read submitted offers after explicit tender closure. Evaluation Committee members require an effective assignment to that specific tender and an allowed closed stage. Top Committee access retains the existing final-decision/award-history stage and assignment checks. Drafts remain private. Passing a deadline while a tender is still Open does not release offer contents. Admin and RequestingDepartment have no new confidential-offer endpoint.

## Audit and notifications

Draft creation, draft save, document upload/replacement/removal and final submission record `AuditLog` rows with actor, bid identity, status and UTC timestamp. Records are written inside the existing bid/document transaction. Supplier-facing activity history exposes event descriptions and timestamps, without storage references or internal account identifiers. Existing offers without activity rows display an empty history. Historical events are not fabricated or backfilled.

Successful final submission creates a database-backed confirmation notification addressed only to the submitting supplier, linked to that bid and tender. It contains no prices, proposal content or document names. The existing notification inbox links that receipt to the supplier's offer. Submission rollback also rolls back the event and receipt. Repeated or concurrent submission cannot create a second receipt.

Notification behavior in this phase is the supplier's submission receipt. Additional officer alerts, unread-bell feeds and mark-read operations are separate enhancements. Compliance decisions, evaluation/scoring, award decisions and further procurement modules remain outside this phase.

## Files

- Added `Controllers/ReviewBidsController.cs`, `Data/BidReviewPages.cs`, `Models/Bids/BidReviewModels.cs`, `Views/ReviewBids/` and `Views/Bids/History.cshtml`.
- Updated existing bid data/domain/document services for transactional activity and receipts, and the shared offer details model for an optional scoped company name.
- Updated supplier history action/link, notification receipt link, navigation, service registration and comparison header styling.
- Added `BidPhase4Checks.cs` and `tests/bid-phase4-browser.cjs`. Extended the existing memory fixture with transactionally rolled-back event/notification lists and a Phase 4 browser mode.

## Verification

- Solution build including Razor compilation succeeded with zero warnings/errors.
- `--bid-phase4-checks-only` passed 54 checks. Coverage includes real MVC authorization and views, source quantities and two-offer comparison, stage/assignment isolation, protected downloads, own activity history, rollback, duplicate submission and concurrent receipt uniqueness. These use memory stores and real temporary private files.
- Existing bid Phase 3/2/1 regression mode passed 61 MVC, 97 private-document and 92 domain checks, alongside tender/requisition rules and supplier-file checks.
- Default HTTPS Identity/employee/department regression runner passed.
- SchemaChecks passed for the existing model, relationships, decimal storage, Identity stores and four migrations, without opening a SQL connection.
- Edge/Playwright passed 56 new browser checks at 1440/768/390/320px, including internal list/details/comparison/history, private download, supplier history, reduced motion and zero JavaScript errors. Screenshots/results are external QA artifacts using synthetic fixture data.

No entities, migrations, role definitions, application settings or database records were changed during verification. Production SQL persistence and cross-process SQL concurrency were not run and require separate approval. The production activity/receipt code uses the existing SQL transaction and tender lock.

Run the safe checks with `--bid-phase4-checks-only`. For visual QA, `--bid-phase4-browser-fixture` prints the marked memory-only host, bid/tender IDs and temporary storage directory. Run the browser script with those values and an external output folder, then stop the host so its temporary private storage is removed.
