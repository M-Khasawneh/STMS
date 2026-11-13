# Purchase requisitions

Implements Word sections 2, 4.2 and 8 plus the user's approved requisition decisions. Existing PurchaseRequisition, PurchaseRequisitionItem, PurchaseRequisitionReview and AuditLog entities/tables are reused. No new roles, packages, schema changes or migrations are needed. Tender conversion is outside this module.

## Pages and actions

/Requisitions lists authorized records; /Requisitions/Create and /Requisitions/Edit/{id} compose the header and items, save a draft or save-and-submit. /Requisitions/Details/{id} shows originating department, creator, dates, reason, items, status, review decisions and readable audit history. POST Submit and POST Review perform the approved transitions. Add/remove item controls progressively enhance server form round-trips; they work without JavaScript. No requisition deletion or reopening endpoints exist.

## Authorization

RequestingDepartment users create, edit and submit only their own requests and see no colleagues' records, even in the same department. New requests derive creator and originating department from the signed-in user's existing EmployeeDepartment assignment. These values are never taken from the posted form. Existing request origin is preserved after employee department changes.

SourcingOfficer users see requests across departments and may review, but cannot edit request content. Both the creator and current officer department are compared server-side with the request's original creator/department; either conflict denies review. Admin is read-only across all requisitions. Supplier and unrelated roles have no module access. For multiple-role accounts the explicit restrictions take precedence: Admin remains read-only; Officer does not gain content-editing rights by also holding RequestingDepartment. Read ownership failures return 404. All POSTs use the existing global antiforgery filter. Posted item IDs must belong to the route-selected request.

## Workflow and validation

Draft -> Submitted; Submitted -> Under Review; Under Review -> Approved / Rejected / Returned; Returned -> Submitted after correction. Only owners of Draft/Returned requests can edit. Approval and rejection are read-only final states in this module. Review must explicitly begin before a decision. Rejection and return require a nonempty reason; approval comments are optional. Returned content is compared with a canonical snapshot recorded at return so unchanged resubmission is refused; corrections can be saved before resubmission or saved and submitted together.

Headers require title, reason, one of Low/Medium/High/Urgent and a target date. Drafts may have zero items and a past target date; submission requires at least one valid item and a target date today or later. Today is derived from the server's local calendar via TimeProvider and is displayed on the form. Every saved item requires description, positive quantity and specifications. Quantity respects the existing decimal(18,6) storage: up to six fractional digits and twelve integer digits, with no silent rounding. Client feedback and server validation are both provided.

## Consistency and audit

Each content save, submission or review decision commits with its audit records in one SQL transaction. Review starts and outcomes append PurchaseRequisitionReview rows, preserving reviewer ID, UTC time and comments. AuditLog records preserve actor ID, action, prior/new statuses, UTC time and content snapshots for additions/edits. Return audit evidence preserves comments and the content snapshot used to check correction. The UI presents readable field/item changes instead of technical JSON.

Serializable transactions and update/hold locks on the request serialize mutations. A form version is SHA-256 over persisted request/items and audit event identities, so stale saves/submissions/decisions are rejected with HTTP 409. No concurrency column or migration is introduced. Existing item IDs survive edits; removed items are deleted only from their owning editable request. Foreign or duplicated item IDs are refused. Audit-write failure rolls back the content transaction. Invalid decisions and reasons do not modify status or history.

## Verification

Build and no-SQL rule/Identity/employee/department/supplier upload regressions passed. The full transition matrix, ownership/role precedence, date, quantity and priority rules are covered. The existing schema check confirms the model still matches the three existing migrations.

The SQL/HTTPS test command is dotnet run --project tests/SourcingTenderManagementSystem.AuthenticationChecks --no-build --no-restore -- --requisition-sql-checks-only. It uses temporary accounts and one temporary review department, the real Identity/EF stores, real MVC antiforgery and cookies, and an ephemeral TLS listener. It tests drafts, item add/edit/remove and ownership, empty/past-date submission rejection, all approved transitions, reasons, self/department review exclusions, colleague isolation, Admin/officer restrictions, overposting, stale forms, simultaneous decisions and audit-failure rollback. Browser tests run inside that fixture and cover five widths, dynamic and JavaScript-free item controls, client validation, review reasons, reduced motion and Admin read-only pages. Cleanup removes only test-user-owned requests/items/reviews/audits, test accounts and the dedicated test department, then compares all table counts and pre-existing Identity data/memberships with baseline.

Final result: all requisition SQL/HTTPS workflow, permission, validation, stale-form, concurrent-review and audit-failure tests passed, plus 71 browser checks at 1440, 1024, 768, 390 and 320px. Client feedback space prevents validation from moving a submit button during a pointer click; dynamic item validators are destroyed/reparsed safely. The rule suite and existing Identity/employee/department regressions passed. The existing supplier SQL/HTTPS suite also passed and cleaned up its temporary accounts/files. All three existing migrations still match the EF model. Final SQL inspection found zero retained test requisitions, items, reviews, audit logs or supplier profiles; only the original admin -> Administration -> Admin remained, with no invalid foreign keys. All pre-existing user data and memberships were unchanged. Build succeeded with zero errors and two existing NU1900 vulnerability-feed warnings. No remaining blocker.

## Files

Created: Controllers/RequisitionsController.cs; Data/RequisitionManagement.cs; Security/RequisitionActor.cs; Models/Requisitions/RequisitionViewModels.cs and RequisitionDisplay.cs; Views/Requisitions/Index, Create, Edit, Details and _Fields; wwwroot/js/requisitions.js; RequisitionRuleChecks.cs, RequisitionSqlChecks.cs and tests/requisition-browser.cjs.

Modified: Program service registration, role-aware shared navigation/breadcrumbs, shared CSS and the existing authentication test runner. No existing management controller, supplier backend, Identity behavior, entity mapping, migration or Word requirements was changed.
