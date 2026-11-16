# Audit repairs — 2026-11-16

## Implemented

- Supplier registration now journals private writes and holds a cross-process filesystem lease through its SQL transaction and reference reconciliation. Uncertain commits are resolved from committed references after transaction disposal. SQL failures preserve bytes and pending intent; a subsequent registration retries recovery. Existing uploads without recovery markers are never swept.
- Structural validation decodes images and parses/renders every PDF page in the existing isolated worker, bounded by size, concurrency, timeout and memory. Signature/extension/filename checks remain. Test success fixtures now contain actual readable documents; signature-only and malformed fixtures are rejection cases.
- Removed the arbitrary 40-page download cutoff. The already installed PDFium/Skia packages provide native Windows/Linux/macOS assets, and rendering now dispatches explicitly to supported desktop platforms. Protected flattened copies and authorization rechecks remain; no original-file fallback was added.
- Supplier registration POST has a three-attempt/minute per-IP policy and Retry-After responses. Private worker concurrency stays bounded.
- Production employee lists use four SQL statements per page (count, selected users, batched roles, batched departments), independent of page population.
- My Offers and internal submitted-offer lists use authorized metadata projections, without loading bid items/documents/full commercial contents for each list row. Details/edits/submissions still use authoritative services and validation.
- Server pagination (25 rows) for employees, supplier applications, requisitions, tenders, My Offers and internal submitted offers; deterministic ordering, authorized total counts, query/status/tender filter preservation in pager links. Existing routes and view model contracts remain usable.
- Eligibility uses a fresh SQL predicate rather than repeated role/profile/department queries. Notification streams reuse that predicate without loading committees. Zero-interval security-stamp validation remains enabled; no cross-request authorization cache was introduced.
- Known SQL conflicts/deadlocks and temporary processing failures produce safe conflict/unavailable responses. Unknown exceptions remain visible to normal error handling. Modifying operations are never automatically retried.
- Shared style ownership moved into foundation/shell partials; styles, conditional loading and cascade order verified exactly against the baseline. No CSS declarations, colors or spacing changed. No broad CSS merge or unrelated service rewrite was justified by a proven defect; common paging, recovery and failure handling were extracted instead.

## Migration — prepared, NOT applied

`20261116152333_BidParticipationUniqueness` adds only the unique index on `Bids.TenderSupplierId`; it checks for duplicate groups and stops instead of altering conflicting records. Previous migrations are unchanged. The model snapshot and obsolete schema assertion were updated. Actual SQL application/concurrency enforcement requires separate approval. The earlier camera-face migration remains unapplied and was not activated as part of this work.

## Verification

- Final solution build: succeeded, zero warnings/errors.
- 25 explicit safe suites passed: audit-repair, work-hub, experience, provisioning, workspace-ui, admin-dashboard, profile-picture, supplier-expiry, bid-phase3, bid-phase4, compliance, evaluation, report, top-committee, internal-clarification, contact, inbox, reporting, voice-search, passkey, ai, document-extraction, access-copy, static-asset, face-settings. Authentication/employee/department default checks also passed.
- Focused final checks: supplier structural upload/storage/lease checks passed; 12 database-free recovery/concurrency/rate-limit checks passed; 28 SELECT-only paging/query-budget/eligibility checks passed; EF model/snapshot and ten migration definitions passed.
- Personal-copy checks: 34 passed, including 41-page PDF, source integrity and protected derivatives. Static asset checks: 16 passed; camera settings protocol/encryption/MVC checks: 43 passed.
- No SQL mutation tests, migrations, provisioning or seeding were executed. SELECT-only tests use an interceptor that rejects non-SELECT commands.

## Limits

Actual SQL commit-disconnection and deadlock scenarios were not injected against the live database; their recovery/error policies have isolated tests. The new index is not yet enforced by the current database. Windows rendering was exercised; Linux/macOS runtime acceptance remains unverified. Large/complex documents remain subject to the existing technical time/memory/output limits (45 seconds, 700 MiB worker guard, 40 MiB derivative). A full manual visual/device acceptance pass was not performed. The existing logo change was excluded from this patch.

## Changed files

- `src/SourcingTenderManagementSystem.Web/Controllers/BidsController.cs`
- `src/SourcingTenderManagementSystem.Web/Controllers/EmployeesController.cs`
- `src/SourcingTenderManagementSystem.Web/Controllers/NotificationsController.cs`
- `src/SourcingTenderManagementSystem.Web/Controllers/RequisitionsController.cs`
- `src/SourcingTenderManagementSystem.Web/Controllers/ReviewBidsController.cs`
- `src/SourcingTenderManagementSystem.Web/Controllers/SuppliersController.cs`
- `src/SourcingTenderManagementSystem.Web/Controllers/TendersController.cs`
- `src/SourcingTenderManagementSystem.Web/Data/BidData.cs`
- `src/SourcingTenderManagementSystem.Web/Data/BidReviewPages.cs`
- `src/SourcingTenderManagementSystem.Web/Data/DatabaseFailureFilter.cs`
- `src/SourcingTenderManagementSystem.Web/Data/DocumentWatermarkWorker.cs`
- `src/SourcingTenderManagementSystem.Web/Data/EmployeeData.cs`
- `src/SourcingTenderManagementSystem.Web/Data/Migrations/20261116152333_BidParticipationUniqueness.Designer.cs`
- `src/SourcingTenderManagementSystem.Web/Data/Migrations/20261116152333_BidParticipationUniqueness.cs`
- `src/SourcingTenderManagementSystem.Web/Data/Migrations/ApplicationDbContextModelSnapshot.cs`
- `src/SourcingTenderManagementSystem.Web/Data/NotificationInbox.cs`
- `src/SourcingTenderManagementSystem.Web/Data/PrivateSupplierFiles.cs`
- `src/SourcingTenderManagementSystem.Web/Data/PrivateWatermarkedCopies.cs`
- `src/SourcingTenderManagementSystem.Web/Data/ProcurementModelConfiguration.cs`
- `src/SourcingTenderManagementSystem.Web/Data/RequisitionManagement.cs`
- `src/SourcingTenderManagementSystem.Web/Data/SupplierBidPages.cs`
- `src/SourcingTenderManagementSystem.Web/Data/SupplierDocumentRecovery.cs`
- `src/SourcingTenderManagementSystem.Web/Data/SupplierRegistration.cs`
- `src/SourcingTenderManagementSystem.Web/Data/TenderManagement.cs`
- `src/SourcingTenderManagementSystem.Web/Models/PageWindow.cs`
- `src/SourcingTenderManagementSystem.Web/Program.cs`
- `src/SourcingTenderManagementSystem.Web/Security/EmployeeEligibility.cs`
- `src/SourcingTenderManagementSystem.Web/Security/EmployeeManagement.cs`
- `src/SourcingTenderManagementSystem.Web/Security/PublicContactConfiguration.cs`
- `src/SourcingTenderManagementSystem.Web/Views/Bids/Index.cshtml`
- `src/SourcingTenderManagementSystem.Web/Views/Employees/Index.cshtml`
- `src/SourcingTenderManagementSystem.Web/Views/Requisitions/Index.cshtml`
- `src/SourcingTenderManagementSystem.Web/Views/Shared/_FoundationStyles.cshtml`
- `src/SourcingTenderManagementSystem.Web/Views/Shared/_Layout.cshtml`
- `src/SourcingTenderManagementSystem.Web/Views/Shared/_Pagination.cshtml`
- `src/SourcingTenderManagementSystem.Web/Views/Shared/_ShellStyles.cshtml`
- `src/SourcingTenderManagementSystem.Web/Views/Suppliers/Index.cshtml`
- `src/SourcingTenderManagementSystem.Web/Views/Tenders/Index.cshtml`
- `tests/SourcingTenderManagementSystem.AuthenticationChecks/AccessCopyChecks.cs`
- `tests/SourcingTenderManagementSystem.AuthenticationChecks/AuditReadOnlyChecks.cs`
- `tests/SourcingTenderManagementSystem.AuthenticationChecks/AuditRepairChecks.cs`
- `tests/SourcingTenderManagementSystem.AuthenticationChecks/BidPhase2Checks.cs`
- `tests/SourcingTenderManagementSystem.AuthenticationChecks/ComplianceChecks.cs`
- `tests/SourcingTenderManagementSystem.AuthenticationChecks/ComplianceSqlChecks.cs`
- `tests/SourcingTenderManagementSystem.AuthenticationChecks/ExperienceChecks.cs`
- `tests/SourcingTenderManagementSystem.AuthenticationChecks/ProfilePictureChecks.cs`
- `tests/SourcingTenderManagementSystem.AuthenticationChecks/Program.cs`
- `tests/SourcingTenderManagementSystem.AuthenticationChecks/ReportingReadOnlyChecks.cs`
- `tests/SourcingTenderManagementSystem.AuthenticationChecks/SupplierSqlChecks.cs`
- `tests/SourcingTenderManagementSystem.AuthenticationChecks/SupplierUploadChecks.cs`
- `tests/SourcingTenderManagementSystem.SchemaChecks/Program.cs`
- `docs/audit-repairs-2026-11-16.md`
