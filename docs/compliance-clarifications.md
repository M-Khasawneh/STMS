# Bid compliance and supplier clarifications

## Delivered scope

Sourcing Officers can record Compliant, Non-Compliant or Pending Clarification outcomes on submitted offers while the tender is Closed. Non-Compliant requires a reason. The service updates only the bid status and appends the existing ComplianceReview and AuditLog records. Supplier-entered commercial contents, quantities, prices, submission timestamp and original bid documents stay fixed.

The officer can address a clarification to the supplier determined from the existing bid participation. An approved supplier with an Accepted invitation can answer their own Pending request. The requesting officer can resolve an Answered request. A new question uses a new clarification request. Responses and attached evidence are immutable after sending. This increment covers the Sourcing Officer / Supplier compliance interaction. Other V5 role-pair conversations belong to their workflow stages.

## Files and routes

- `Controllers/ComplianceController.cs` provides GET Details and Document, and antiforgery-protected POST Decide, Ask, Reply and Resolve.
- `Data/ComplianceManagement.cs` handles fresh account authorization, scoped bid access, transactions, review history, notifications, audit and attachment recovery.
- `Data/ComplianceRules.cs` validates reviewer authority, Closed stage, outcomes/reasons, response ownership/state, attachment limits and stale forms.
- `Models/Bids/ComplianceModels.cs` provides MVC inputs and display models.
- `Models/Entities/ClarificationAttachment.cs` and its model configuration link evidence to ClarificationResponse.
- `Views/Compliance`, `wwwroot/css/compliance.css` and `wwwroot/js/compliance.js` provide the responsive interface using the existing visual identity.
- Existing supplier/internal offer detail pages link to compliance. Existing notifications link clarification events to this page. Bid activity labels include the four new actions.

## Private evidence

Each reply accepts up to ten optional PDF/JPG/PNG files, each at most 5 MiB. Existing signature and bounded-stream validation is reused. Files use generated references and SHA-256 verification, under `App_Data/ClarificationDocuments`, outside `wwwroot`. A separate private storage instance reuses the existing durable journal implementation. It cannot modify the original bid-document directory.

Downloads first authorize the bid and then join attachment → response → request → bid. They return verified bytes as attachments with no-store, nosniff and sandbox headers. Existing submitted-bid visibility applies to authorized internal reviewers, including effective tender-specific committee membership and stage checks. Admin and RequestingDepartment gain no confidential access. Supplier reads require an approved enabled account, owning bid and Accepted invitation.

Writes use the existing serializable tender lock. A version hash over bid status, review IDs and clarification states rejects duplicate/stale forms. Replies, attachments, state, audit and notification metadata commit together. Durable file intents precede file creation. Cleanup uses committed references after transaction disposal, including an uncertain commit result. A temporary database/storage outage retains the journal for recovery on the next authorized reply or attachment download and logs the pending cleanup. No external scanner or notification service is introduced.

## Database

`20261114120000_ClarificationAttachments` adds only the attachment table, its response foreign key with NoAction deletion, size check and response index. Earlier migration files are unchanged. Model snapshot equivalence is checked by the existing SchemaChecks runner.

The approved migration was applied after read-only inspection. Existing account, role, supplier, employee, department and procurement row counts were preserved. The new table was empty afterward and no migration remained pending.

`--inspect-clarification-database` is a read-only preflight for the specific pre-migration state. `--apply-clarification-migration` performs that preflight and applies this one approved migration. Both deliberately reject a different migration state, including a previously applied target, so they cannot advance an unrelated migration.

## Verification

- Build passed with zero warnings and errors.
- `--compliance-checks-only` passed 61 domain/private-file checks and 25 MVC checks covering state/version validation, storage/signatures, real Razor rendering, model validation, role filters and antiforgery. MVC rendering uses a presentation-only fixture service.
- `tests/compliance-browser.cjs` passed 31 checks across 1440, 768, 390 and 320 px layouts, form feedback, keyboard focus, reduced motion and JavaScript execution against the marked database-free fixture.
- Existing bid Phase 1–4, tender/requisition/upload rule, Identity, employee and department checks were run through their safe runners.
- SchemaChecks verifies 37 required tables, five migrations, attachment constraints, snapshot equivalence and non-destructive relationships.

SQL Server workflow write/rollback/concurrency tests for the new service were not run. Approval covered the migration. The application database contained no bids or tenders at inspection, so no existing transaction was available for an end-to-end review. Service-level database failure recovery and concurrent SQL execution remain to be exercised with separately approved temporary SQL test data. Browser fixture results establish UI behavior and do not establish SQL workflow persistence.
