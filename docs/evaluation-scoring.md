# Technical and financial evaluation

The user delegated implementation choices after reviewing six proposed scoring decisions. Each currently appointed member scores every approved technical and financial criterion from 0 to 100. The criterion result is the arithmetic mean of the four current members' confirmed scores. Weighted points equal that mean multiplied by the approved percentage weight divided by 100. Decimal arithmetic is retained throughout calculation; display formatting uses up to six decimal places.

The officer explicitly starts evaluation from a Closed tender's details page. Starting requires approved criteria, four distinct eligible committee positions, at least one compliant submitted offer, completed compliance outcomes for all submitted offers and resolved bid clarification requests. Completing compliance before handoff prevents outstanding clarifications from becoming stranded when the tender leaves Closed. Only Compliant bids can be scored while the tender is UnderEvaluation.

## MVC entry points

- `Evaluations/Start` is a Sourcing Officer POST linked from Closed tender details.
- `Evaluations/Details/{bidId}` shows the authorized offer's evaluation, the current member's draft/history and the completed weighted result. It is linked from internal offer details.
- `Evaluations/Save/{bidId}` saves the caller's draft against the approved criterion IDs.
- `Evaluations/Confirm/{bidId}` provides a saved-score preview and explicit POST confirmation checkbox.
- Committee start notifications route to the authorized Submitted offers list, including for evaluators without general tender-management access.

Partial drafts may omit scores. Notes for an unscored criterion require a score before saving. Confirmation requires all approved criteria. The server validates the exact criterion set, score bounds, precision, notes lengths, current membership, role, department representation, tender stage, compliance outcome and stale form version. Posted member/user IDs, criterion type, weights and timestamps are not inputs.

Manager and committee roles can write only through an effective eligible appointment to that specific tender. Officers have read-only scoring access. Existing bid visibility remains authoritative for managers, committee members and final-stage Top Committee readers. Admin and Supplier accounts have no scoring endpoint or confidential evaluation access. RequestingDepartment-only accounts remain excluded. Per-member draft/confirmation audit actions are recorded without adding them to supplier-facing bid-history labels.

All writes retain the existing serializable tender lock. Draft versions include tender/bid state, membership ID and the caller's persisted scores. Confirmed rows are immutable. Changing committee membership after evaluation starts requires the already implemented reasoned replacement procedure. Previous rows and scores remain intact. A replacement's new membership must submit its own scores, and combined results wait for all four current eligible memberships to confirm. No automatic ranking, threshold, award or report handoff is inferred from the score total.

## Approved migration

`20261114190000_EvaluationConfirmation` adds nullable CommitteeMemberId and SubmittedAt fields to TechnicalEvaluations and FinancialEvaluations. Composite member/tender foreign keys retain tender scope. Filtered unique bid/criterion/member indexes prevent duplicate scoring rows while preserving unlinked legacy rows. Legacy scores remain available in SQL and are excluded from current-member aggregates until represented by the new confirmed membership records.

Existing migration files are unchanged. The model snapshot and new target designer match the EF model. The explicit SchemaChecks `--apply-evaluation-migration` command inspected existing score scales and row counts and applied only this approved migration. Preflight found zero previous technical/financial score rows, 15 users, five bids and three tenders. Counts were preserved. The running application performs no automatic migration or seeding.

## Verification

- Solution build passed with zero warnings/errors.
- `--evaluation-sql-checks-only` passed 29 checks using real scoped SQL services. Coverage includes explicit start prerequisites, outstanding clarification gating, role/member isolation, partial drafts, invalid score range/precision, manipulated criteria, stale forms, injected audit failure rollback, concurrent confirmation, exact weighting, immutable confirmation and replacement/history retention.
- The sample calculation returned exactly 81 points, then 80.5 after a documented member replacement and new confirmation.
- SQL fixture cleanup removed all temporary users, role assignments, tender/source/bid data, scores, criteria, memberships, audits and notifications. Existing users and role assignments matched their baseline. A first cleanup failure caused by tender-scoped notifications was corrected; the owned failed fixture was recovered and the full test was rerun successfully.
- `--evaluation-checks-only` passed 15 database-free real Razor rendering, role-filter and antiforgery checks. It uses a presentation-only service fixture, separately from the real SQL service tests.
- `tests/evaluation-browser.cjs` passed 33 Edge checks at 1440, 768, 390 and 320 px, including confirmation, score validity, keyboard focus, read-only officer display, reduced motion and no JavaScript errors.
- Existing bid Phase 4, compliance, SQL committee and schema regressions passed. Schema verification covers six migrations and 37 tables.

Unified report authoring and manager/Top Committee report decisions remain subsequent modules. Current results provide the confirmed calculations and observations needed for that work.
