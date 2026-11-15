# Top Committee final decisions and explicit award

## Implemented scope

The approved four-member collective decision workflow is now wired through the existing MVC, Identity, EF Core and SQL Server architecture. No new roles, tables, dependencies, bid statuses, contract workflow or procurement modules were introduced. The separately approved migration 20261114220000_TopCommitteeVotingRounds was already applied on 2026-11-15; this implementation did not apply or alter migrations or write database records.

- The appointed evaluation Sourcing Manager selects four distinct enabled internal TopCommittee employees. Trusted queries exclude tender creators/preparers, evaluation participants (including historical appointments and recorded evaluators), report authors and manager approvers.
- Replacement requires a reason, ends the superseded membership and closes an active round without deleting its votes. A fresh round freezes the new four membership IDs and approved report version.
- Each appointed member confirms one immutable reasoned vote: approve, reject, or request details. Other members' choices stay sealed until all four vote, including incomplete abandoned rounds. Reporting/Excel/PDF queries also exclude incomplete-round votes. Audits and notifications contain no partial choices/reasons.
- All four votes are required. Three matching choices produce the collective outcome. No majority leaves the tender AwaitingCommitteeDecision and requires a documented explicit new round.
- Rejection or details request returns to UnderEvaluation, preserves the submitted report and confirmed scores, and creates a new report draft. Current-report selection recognizes the returned manager-approved version and requires renewed manager review.
- Approval does not award automatically. Only the responsible tender-creator SourcingOfficer explicitly records the compliant recommended bid as winner. Approved supplier eligibility is checked again before commit. TenderAward, Awarded status, audits and existing in-app notifications share one transaction. Bid statuses remain unchanged.
- An authorized outcome projection returns metadata and, for invited suppliers, only their own selected/not-selected result. It exposes no competitor name, bid ID, documents, prices or vote details. Admin receives metadata only.

## Application files and routes

- Data/TopCommitteeData.cs: scoped EF queries, provenance, eligibility, history, protected outcome projection and immutable-record persistence.
- Data/TopCommitteeManagement.cs: assignment, round, vote, rework and award orchestration; serializable tender lock, fresh account checks and stale-form versions.
- Models/TopCommittee/TopCommitteeModels.cs and Controllers/TopCommitteeController.cs.
- Views/TopCommittee/{Index,Details,Outcome,Problem}.cshtml and wwwroot/{css,js}/top-committee.*.
- Existing shared navigation, notification destinations, tender/report/bid details and role dashboard link to authorized entry points.
- Existing ReportReviewRules and UnifiedReportManagement support Top Committee report revisions; Reporting protects sealed ballots.

GET /TopCommittee and /TopCommittee/Details/{tenderId} are restricted to the responsible officer, appointed manager and currently eligible assigned Top Committee members at their permitted stages. POST Assign/Open require the appointed manager; Vote requires the assigned member; Award requires the responsible officer. Every modifying action requires antiforgery. GET /TopCommittee/Outcome/{tenderId} applies its own record-level metadata projection and no-store headers.

## Transaction and integrity details

The existing IBidData serializable UPDLOCK/HOLDLOCK tender transaction serializes changes. SHA-256 versions cover trusted workflow/eligibility state and ballot metadata, never partial ballot choices. Existing records are updated through a narrow allowlist: tender status, membership end, and Top Committee round closure/decision fields. Reports, manager decisions, awards, existing votes and frozen rosters are immutable. Closing an old round is flushed before inserting its replacement to respect the filtered unique open-round index; both flushes, new votes, audits and notifications remain in the same transaction. Decision and closure timestamps use the same captured UTC instant to satisfy the SQL check constraint.

## Exact migration

20261114220000_TopCommitteeVotingRounds reuses TenderApprovals, CommitteeVotes and TenderAwards. No new tables, business statuses, roles or dependencies.

1. Add nullable RoundOpenedAt and RoundClosedAt datetimeoffset columns to TenderApprovals.
2. Add nullable RoundCloseReason nvarchar(max), with the application/model 8000-character limit.
3. Add nullable RoundMemberSnapshot nvarchar(512): server-written JSON array of four CommitteeMember IDs, frozen for the round. It is not a posted user field. Domain validation checks exact cardinality, distinct nonempty GUIDs and correspondence to the trusted roster.
4. Make DecidedAt nullable so pending/abandoned rounds never require fabricated decision timestamps. Existing manager decisions still require real decision dates through the check constraint; existing timestamps are unchanged.
5. Add a filtered unique index for one open TopCommittee round per tender, a unique round/member vote index and a unique tender award index (replacing the previous nonunique award lookup index).
6. Add a check constraint for coherent legacy/open/closed/final round metadata, valid JSON and timestamp ordering. A rollback guard refuses to drop any stored voting-round history or manufacture decision dates.

The generated review script is top-committee-voting-migration.sql. Its exact reviewed commands were applied in operation batches within an outer verification transaction. Earlier migration files are unchanged; the normal EF snapshot was updated for the prepared target. The existing manager-report view handles a nullable date defensively without changing its workflow.

## Verification in this implementation

- Solution build and compiled Razor: zero warnings/errors.
- --top-committee-checks-only: 128 pure voting/majority/secrecy/award checks, 49 production-service checks using serializable memory transactions, and 23 real compiled MVC checks. Covers role/appointment isolation, stale forms, concurrent votes/award, atomic failure rollback, replacement, ties, both return outcomes, renewed report selection, supplier result isolation, antiforgery and HTML encoding.
- Existing checks passed: manager report review 40 + MVC 29; reporting/export 50 + MVC 46; workspace 59; internal clarification 127 + MVC 17; bid audit/internal visibility 54; evaluation MVC 15; compliance 61 + MVC 25; default HTTPS Identity/employee/department regression runner.
- SchemaChecks: seven source migrations, snapshot/model agreement, constraints and 37 required tables passed. No migration applied by these checks.
- SELECT-only actual SQL checks: 97 read/report/export checks passed, including new final-decision list/page and protected outcome projections for eligible existing accounts. No eligible existing TopCommittee account was available; that role was covered by memory service/MVC tests instead. The interceptor rejects non-SELECT commands.
- Edge browser using a memory-only host: HTTP 200 at 1440, 768, 390 and 320 px; no document overflow or JavaScript errors; keyboard focus and reduced-motion mode checked. Screenshots and fixture assets stayed outside the repository. No live users were created.

## Remaining verification boundary

Actual SQL writes, vote/award persistence, transaction failure and concurrent SQL operations have not been tested in this implementation. Memory concurrency proves service behavior, not SQL lock/index behavior. A separately approved temporary SQL integration test is still required before claiming complete database persistence/concurrency verification. No new migration is required. Do not rerun the already applied migration or seed committee accounts automatically.
