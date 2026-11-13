# Supplier Bids and Documents — Phase 1

## Scope

Implemented bid input models, pure validation/access rules, application service operations and the SQL Server persistence adapter. No bid routes, views, file uploads/downloads, audit-history UI or notification delivery were added. Those remain in Phases 2–4.

The existing `Bid`, `BidItem`, `BidCostComponent`, `BidDocument`, tender, invitation and committee entities are reused. No schema or migration changes were made.

## Rules

- Participation requires an approved, unlocked, Supplier-only account, its accepted invitation, approved criteria, and an Open tender during `[SubmissionStart, SubmissionDeadline)`.
- One bid is created per tender/supplier participation; duplicates and conflicting legacy bids are rejected. All bid writes take the existing tender `UPDLOCK,HOLDLOCK` in a Serializable transaction, matching tender workflow locking. This relies on every future bid/document mutation using that same boundary. A database unique index can be considered separately as defense in depth; no index is required to serialize the current service writers.
- Save and submit require the current content version. The version includes all items, costs and document metadata. Submission transitions only Draft to Submitted, once, and persists the UTC submission time. No withdrawal or resubmission operations exist.
- Every source requisition item must appear exactly once. The server derives descriptions, specifications and quantities from the tender source. Supplier/status/currency/quantity/total are not form inputs.
- JOD only. Unit prices are positive; optional additional costs are nonnegative. Stored decimal inputs obey existing decimal(18,6) precision/range. Totals use checked decimal arithmetic without silently rounding input values or intermediate products.
- Delivery, technical details, payment terms and offer-validity date are required for a saved bid. This phase does not invent a minimum offer-validity period or default costs.
- Submission requires 1–10 document records including a Technical Proposal. Metadata prerequisites are checked, but are not treated as proof of safe files.

## Fail-closed document boundary

`IBidSubmissionDocuments` is the Phase 2 verification boundary. Production currently resolves it to `BidSubmissionDocumentsPending`, which rejects submission. Phase 2 must verify the actual privately stored files before replacing this registration. Unit checks use an explicitly test-only verifier; no physical bid documents are uploaded or claimed verified in Phase 1.

## Confidential reads

Queries filter before loading items, charges or documents. Suppliers see only their own bids. Officers and Managers see submitted bids after closure. Evaluation Committee members require an effective assignment to that tender. Top Committee members require an effective Top assignment and the final-decision stage; access to awarded tender history is retained as required by V5. Admin does not gain confidential access, including with combined roles; requesting-only accounts have no direct access. No internal actor can read supplier drafts. Cancelled status alone is not treated as proof that bidding previously closed.

SQL queries recheck effective committee membership. Write operations refresh Identity user/account state rather than relying on previously tracked Identity entities. No Admin metadata page is supplied yet.

## Verification

The explicit `--bid-phase1-checks-only` mode returns before starting hosts or selecting any SQL integration runner. It exercises services against a transactional memory test double, validates SQL query translation without connecting, and runs existing tender/requisition pure rule checks. The schema runner without `--inspect-database` checks only the EF model and migration definitions.

These checks do not verify actual SQL locking, persisted records, HTTP antiforgery or document bytes. SQL persistence/concurrency tests need separate approval and safe test infrastructure. Phase 3 will supply MVC validation/antiforgery and Asia/Amman display using the existing TenderTime helper. Phase 4 supplies audit history and notifications.
