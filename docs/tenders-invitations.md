# Tenders and supplier invitations

## Approved module contract

Implements the Word sourcing workflow plus the user's tender, deadline, invitation and criteria-approval decisions. Each successful Draft creation atomically links exactly one Approved purchase requisition and marks that requisition ConvertedToTender. A source cannot be converted twice. Officers manage across departments; Admin is read-only even in combined-role accounts. Request creators see only tenders linked to their own requisitions. Sourcing Managers can inspect criteria and approve them, without gaining officer management rights. Approved, eligible invited suppliers see published tender content, items, approved criteria, their own invitation and their own notifications. Anonymous access is denied. Supplier company documents retain their existing independent owner/Admin protection; no new tender document upload or bid functionality is introduced.

Core content and dates are editable only in Draft. Workflow actions are Draft -> Published -> Open -> Closed. Publication requires an unexpired deadline; opening is an explicit officer action within the configured start-inclusive/deadline-exclusive period and requires approved criteria. Closing is explicit once the configured submission period has ended. Status never automatically changes because of time. No reopening, cancellation, later evaluation, bidding or award transitions are implemented.

All submission timestamps are converted from explicit Asia/Amman form inputs to UTC and displayed back in Asia/Amman. The current submission availability helper checks status Open and start <= now < deadline, including the exact-deadline exclusion. This does not implement bid submission.

## Criteria configuration and approval

Officers configure names, descriptions, existing Technical/Financial kinds and percentage weights in Draft or Published before approval. No criteria or weights are supplied by default. Weight supports the existing decimal(18,6) storage, is a percentage between 0 and 100 and must total exactly 100 before approval. Sourcing Managers approve the complete set in one transaction using existing ApprovedByUserId and ApprovedAt fields. Approved rows become read-only.

Audit records identify every actual configurator for the current criterion IDs. A manager who configured any current criterion cannot approve that set, including combined Officer/Manager accounts. Deleted criteria no longer belong to the current set. Approval also requires recorded authorship for every current criterion, so unattributed legacy data cannot bypass separation of duties. MaxScore is unchanged and not exposed; scoring is outside this module.

## Invitations, expiry and notifications

Officers select approved, enabled, non-locked-out supplier-only accounts and send invitations only while Published and before the deadline. Supplier identity/profile eligibility is checked again in the write transaction. One tender/supplier association and one invitation per association are enforced. There are no resends.

Suppliers accept or decline a Pending invitation once. Responses are immutable. At the exact submission deadline, late responses are refused. Pending invitations become Expired when the configured period ends; expiry is materialized under a tender row lock during invitation inbox/tracking access, response attempts or explicit closing. Expiry is audited as a system event and never changes tender status. Answered invitations keep Accepted/Declined. The clock is checked again after eligibility queries before storing a response.

The invited supplier receives one database-backed notification. On a response, the tender creator (the existing CreatedByUserId, representing the responsible officer) receives the notification, even if a different officer sent the invitation. Notifications are rendered only for their authenticated recipient. No email, SMS, resend or unrelated notification workflow is added.

## Consistency and security

All writes use existing global antiforgery validation, dedicated form models and server-side authorization. Route IDs and authenticated database roles determine scope; posted creator, source status or approval fields are not trusted. Source conversion uses an update/hold lock on the requisition, and tender mutations serialize on the tender. Unique indexes provide a second line of defense. Forms carry a SHA-256 version over persisted tender/criteria and tender audit event IDs; stale submissions are rejected with HTTP 409. Invitation versions cover invitation state and current tender availability. Duplicate/deadlock database conflicts render a reload response instead of overwriting data.

Audits preserve conversion, core changes, criteria configuration/approval, publication/opening/closing, invitation creation/responses and expiry. Notifications commit with the corresponding invitation action. A failed write rolls back its whole transaction. Approval evidence uses existing fields/audit records; no new approval entities or architecture are introduced.

## Migration applied after explicit approval

Read-only preflight found zero tenders, criteria, links, invitations and tender/supplier records. Existing history has three migrations. Existing link tables currently do not enforce the approved uniqueness rules.

New migration 20261113180000_TenderInvitationIntegrity adds four unique indexes: TenderPurchaseRequisitions.TenderId, TenderPurchaseRequisitions.PurchaseRequisitionId, TenderSuppliers(TenderId,SupplierProfileId), Invitations.TenderSupplierId. Two existing nonunique indexes are replaced by their unique equivalents. No tables, columns, roles or existing business records are recreated. The existing join table is retained: unique indexes enforce upper bounds; the application's atomic conversion transaction ensures each created tender has its required source link. All prior migrations remain untouched.

Reviewed SQL: docs/tender-invitation-migration.sql. Do not apply without the user's explicit approval. Creation and invitation writes fail closed until migration history contains this migration. The user explicitly approved the migration, and it was applied successfully. The database now records all four migrations.

## Verification and execution

Solution build passed. A running application instance subsequently locked its normal executable; it was left running and the final clean build used an isolated artifacts directory under the Codex workspace. The isolated solution build passed with zero warnings/errors using already installed dependencies and NuGet auditing disabled for that command. No packages were added or updated.

The existing no-SQL HTTPS Identity, employee, department, supplier-upload and requisition-rule regressions passed. New tender rules cover exact deadline/start boundaries, Asia/Amman UTC round-trips, exact 100 percent totals, full approval and restrictive Admin role precedence. Schema checks verify all four new unique indexes and no pending EF model differences. The schema/model contains four migrations; the database now has all four migrations after the approved deployment.

The new SQL/HTTPS/browser suite passed after the approved migration was applied. It requires the real Identity/SQL stores and uses only unique-marker temporary accounts, approved-source fixtures, tenders, criteria, invitations, notifications and audits. It checks conversion/duplicate races, stale forms, self-approval and locked criteria, role visibility, supplier eligibility/isolation, notification ownership/recipient selection, one-time responses, exact deadline persistence, explicit closing and audit-failure rollback. Browser checks cover five widths, actual criteria entry and manager approval, supplier acceptance, immutable response UI, Admin read-only behavior and reduced motion. Finally it removes only test-owned records and verifies every table count plus pre-existing Identity data/memberships against the post-migration baseline.

Normal verification command after building: dotnet run --project tests/SourcingTenderManagementSystem.AuthenticationChecks --no-build --no-restore -- --tender-sql-checks-only. For the current isolated build, execute tender-artifacts/bin/SourcingTenderManagementSystem.AuthenticationChecks/debug/SourcingTenderManagementSystem.AuthenticationChecks.dll with dotnet from the project root and the same flag. Browser tests use installed Edge/Playwright. The running normal application needs a rebuild/restart to load the completed module.

## Files

Created: Data/TenderManagement.cs; Controllers/TendersController.cs and NotificationsController.cs; Models/Tenders/TenderViewModels.cs and TenderTime.cs; tender list/details/create/edit/criteria/invitation management/inbox/response/problem views and shared fields; Notifications/Index; migration/designer and SQL script; TenderRuleChecks.cs, TenderSqlChecks.cs and tests/tender-browser.cjs.

Modified: Program service registration, existing index mappings/model snapshot, role-aware shared navigation, requisition-to-tender creation link and audit label, shared navigation scrolling CSS, authentication/schema test runners. Original Word requirements, roles, existing management services and prior migrations are unchanged.

## Final tender verification

The real SQL/HTTPS suite passed conversion, duplicate and concurrent conversion prevention, source linkage/status atomicity, stale Draft/criteria forms, exact percentage totals, officer/manager/Admin separation, self-approval prohibition, immutable approved criteria, Published/Open/Closed transitions, supplier eligibility and two-way privacy, duplicate invitation races, one-time accept/decline, responsible-officer and supplier notification records, and recipient-scoped notification views. At the exact deadline, late responses were rejected and Pending invitations persisted as Expired while Accepted/Declined remained final and tender status stayed Open until explicit closing. An injected audit failure rolled back the tender, link and requisition conversion.

All 102 browser checks passed across 1440, 1024, 768, 390 and 320px, including actual criteria configuration/approval, supplier response, read-only Admin views, hidden supplier tracking/source links, reduced motion and no runtime errors. Desktop/mobile screenshots were inspected. Long invitation titles now wrap on narrow screens. All test-owned accounts and linked records were removed; every table count matched the post-migration baseline and original Identity users/memberships were unchanged. No bid submission, scoring, AI or awards were implemented.

Final regressions also passed: existing Identity/employee/department/upload checks, the complete requisition SQL/HTTPS suite with 71 browser checks, the supplier SQL/HTTPS suite and schema/model validation. Final SQL inspection confirms all four intended unique indexes, four migration-history entries and no retained tender/link/invitation/criteria/notification/audit test records. Foreign keys remain enabled and trusted. Existing real employee and supplier accounts were preserved. There are no remaining code, schema or verification blockers; the already running development instance still requires a rebuild/restart to load the verified isolated build.
