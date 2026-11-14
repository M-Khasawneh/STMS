# Sourcing Manager report review

Implements V5 sections 2 (step 10), 4.6, 7 and 8 using the user's approved follow-up decisions. No new roles, tender statuses, tables, packages or migrations.

## Behavior

- Only the effective Sourcing Manager appointed to this tender's evaluation committee can decide its current submitted report while the tender is AwaitingManagerReview. Other authorized internal readers retain their existing read-only access. Supplier, requester and Admin confidentiality restrictions are unchanged.
- Approve creates ManagerApproved in TenderApprovals and moves the tender to AwaitingCommitteeDecision. All approved recommendations are forwarded in this increment, as authorized by the user; this action never creates an award. Top Committee voting, award and policy thresholds remain outside this module.
- Reject creates ManagerRejected for the submitted version, moves the tender to UnderEvaluation and creates a separate draft for an alternative recommendation. It does not cancel the tender.
- Return creates ReturnedForReview and a separate correction draft in UnderEvaluation.
- Rejection and return require a nonblank reason (maximum 8000 characters). Every decision requires explicit confirmation. Server validation remains authoritative; native client validation also requests a reason for rejection/return.
- A submitted version is never overwritten or reopened. Existing EvaluationReports rows store the successive versions; TenderApprovals links each manager decision to its precise version. The service permits only one unresolved version and rejects inconsistent duplicate/decision records.
- Revised drafts retain the prior comparison snapshot and confirmed technical/financial results. Only the currently appointed Technical Representative may edit/save/submit the new findings using the existing report workflow. Submission notifies the currently eligible appointed manager rather than relying on a historical snapshot's member list.
- Details exposes protected previous-version links, decisions/reasons and report activity. Archives are read-only. Existing unread notifications link to the report. Approved decisions notify effective eligible Top Committee members when assigned; rejected/returned decisions notify effective evaluation representatives. No external notifications or artificial committee assignments are created.

## Consistency and security

Decision, new revision, tender status, notifications and audits share the existing serializable tender-lock transaction. Account/role/appointment eligibility is refreshed inside the transaction and before commit. A version hash includes the actual report, tender status and manager appointment; stale/duplicate/concurrent decisions fail. Antiforgery, role filters, protected bid-query access and no-store response headers are retained. Archive lookups are constrained to the already authorized tender.

## Verification

- Build: zero warnings/errors; compiled Razor pages included.
- 40 database-free review-rule tests and 29 report MVC tests passed, including role guards, explicit confirmation, binding, reason feedback and antiforgery.
- Approved temporary SQL tests passed: 29 evaluation checks and 53 report/review checks. Verified immutable revision history, rejection/return/resubmission, unrelated manager denial, stale/duplicate actions, concurrent approvals, audit-failure rollback, notifications and absence of an award.
- Temporary SQL records were cleaned. Existing Identity records, role assignments, report rows and approval rows matched their pre-test baselines.
- 67 Edge browser checks passed at 1440/768/390/320 pixels, including review forms, conditional reason validation, explicit confirmation, responsive overflow and reduced motion; no JavaScript errors. Screenshots remain outside the repository.
- Regressions passed: bid Phase 4 (54), compliance rules/private files (61), compliance MVC (25), evaluation MVC (15), workspace routing/roles (45), and schema metadata (37 required tables, existing six migrations).

The tests use a disposable marked fixture for SQL writes, following separate user approval. No migrations were applied and existing application data was not changed. The new final-decision actions are deliberately not implemented here.
