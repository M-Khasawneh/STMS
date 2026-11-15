# Supplier official-document expiry — 2026-11-15

V5 P104 requires supplier-document expiration tracking. The user approved Admin entry of genuine document dates, read-only supplier visibility and an inclusive 30-day upcoming window using Asia/Amman. No dates are invented or populated automatically.

## Delivered behavior

- On Supplier Review, each existing official document has its own date editor, integrated with the existing workspace popup behavior. The popup includes the document type. Without JavaScript the original MVC form remains usable.
- Admin records the actual expiry date printed on the original document. Blank means the date is not recorded; it does not mean indefinite validity. Historic expired dates may be recorded. No artificial future-date requirement is applied.
- Labels: before today's Amman date = Expired; today = Expires today; day 1 through day 30 inclusive = Expires within 30 days; later dates = Expiry date recorded; null = Expiry not recorded.
- Supplier Status shows the dates and labels read-only. Supplier downloads, original private files and original approval/suspension decisions remain unchanged. Expiry metadata does not automatically suspend a supplier or block an existing workflow.
- The existing supplier report now includes a separate upcoming-30-days document count, alongside expired and unknown counts. Existing authorized reports and exports use real SupplierDocuments records.

## Security, persistence and concurrency

`SupplierDocumentExpiry` checks a fresh Identity-backed account before opening the transaction, again inside it and before commit. Only active Admin employees without a supplier identity may write dates. The action has Admin authorization and explicit antiforgery validation. No posted user ID, supplier status or storage path controls the operation.

`SupplierExpiryStore` uses a serializable transaction and the same supplier-profile update lock as SupplierReview. Document queries require both the document ID and owning supplier ID. A version token derived from the document's IDs and current expiry date rejects stale forms; it is a concurrency token, not an access credential. A stale date remains stale in the validation response until the user reloads.

Only ExpiresOn is modified. AuditLog records actor, UTC time, document ID and old/new dates in the same transaction. Originals, filenames, storage references, account status and roles are not modified. No-op saves add no audit event. Failure or authorization revocation rolls the date and audit back together.

## Database and tests

- Reuses the existing nullable SQL `date` SupplierDocuments.ExpiresOn field and existing AuditLogs. No entities, migrations, dependencies, seed data or expiry-related database backfill were added.
- Build: zero warnings/errors; compiled Razor views passed.
- 34 database-free service checks: role/eligibility denial, foreign-document isolation, inclusive dates and Amman midnight, audit contents, no-op/clear date, stale/concurrent edits, failure/revocation rollback and preserving approved suppliers when an expired date is entered.
- 12 real MVC/Razor checks: Admin date forms, supplier read-only visibility, encoding, all role gates, antiforgery, binding, memory persistence and redirect to the existing Details route. A connection-blocking SQL interceptor protects the fixture. No test-only controllers/authentication are registered in the production assembly.
- Existing checks passed: 50 reporting rules plus 46 reporting MVC checks; 59 workspace checks; HTTPS Identity/Employee/Department regressions.
- 106 actual SQL read/report/export checks passed with the SELECT-only interceptor. TopCommittee had no eligible existing account and was skipped by that existing runner. No SQL rows or files were written by this check.
- Edge browser: 1440/768/390 layouts, existing date popup and keyboard dismissal, document context inside popup, supplier read-only display, no page/popup overflow and no page errors. Screenshots remain outside the repository.
- SQL **write**/rollback/concurrency execution remains unrun without separate approval. Transaction behavior is covered by memory tests; no claim of live SQL persistence verification is made.
