# Top Committee — prepared foundation, database approval pending

## Delivery boundary

The user approved the seven proposed Top Committee/award rules. This increment prepares the pure rules and the essential migration. It does NOT complete or enable the assignment, voting, rework, award, notification or MVC workflows. Database application requires a separate explicit approval. No migration, seed, SQL write test or application restart was executed.

The new build requires this migration before it is used against the existing database. Keep the current deployment running its existing binaries until the approved migration/deployment is performed. Do not run the new build against the unmigrated database.

## Approved behavior represented by the pure rules

- Four distinct eligible TopCommittee employees; the tender preparers, evaluation participants and report approvers are rejected by the rules when the trusted exclusion set contains them. Building the fresh, record-specific candidate/exclusion queries remains part of the transactional service implementation.
- Three vote choices: CommitteeApproved, CommitteeRejected and ReturnedForReview; each needs a nonblank justification of at most 8000 characters. Votes are immutable.
- All four must vote before a collective result or other members' votes can be revealed. Three matching votes form a majority. The tests enumerate all 81 possible complete vote combinations. No majority means no decision; AwaitingCommitteeDecision remains unchanged.
- Member replacement after voting closes the old round without a final outcome and starts a reasoned new round; confirmed old votes remain retained. Round snapshots store the original four membership IDs, independently of later appointment dates. Round opening/replacement persistence is still to be implemented after approval.
- Rejection or request for details returns to UnderEvaluation for a new report version and manager review; confirmed evaluations are retained. Extending current-report selection for these versions remains part of the next implementation increment.
- Approval alone is not an award. Only the responsible SourcingOfficer can confirm an unawarded tender, using the current approved report's compliant recommended bid and a verified four-vote majority. Award persistence will be atomic and leave the tender Awarded. No new bid-status transitions are inferred here.
- Notification delivery and supplier-only outcome disclosure will be wired with the existing infrastructure; not enabled in this preparation increment.

## Exact migration

20261114220000_TopCommitteeVotingRounds reuses TenderApprovals, CommitteeVotes and TenderAwards. No new tables, business statuses, roles or dependencies.

1. Add nullable RoundOpenedAt and RoundClosedAt datetimeoffset columns to TenderApprovals.
2. Add nullable RoundCloseReason nvarchar(max), with the application/model 8000-character limit.
3. Add nullable RoundMemberSnapshot nvarchar(512): server-written JSON array of four CommitteeMember IDs, frozen for the round. It is not a posted user field. Domain validation checks exact cardinality, distinct nonempty GUIDs and correspondence to the trusted roster.
4. Make DecidedAt nullable so pending/abandoned rounds never require fabricated decision timestamps. Existing manager decisions still require real decision dates through the check constraint; existing timestamps are unchanged.
5. Add a filtered unique index for one open TopCommittee round per tender, a unique round/member vote index and a unique tender award index (replacing the previous nonunique award lookup index).
6. Add a check constraint for coherent legacy/open/closed/final round metadata, valid JSON and timestamp ordering. A rollback guard refuses to drop any stored voting-round history or manufacture decision dates.

The generated review script is top-committee-voting-migration.sql. It has not been executed. Earlier migration files are unchanged; the normal EF snapshot was updated for the prepared target. The existing manager-report view handles a nullable date defensively without changing its workflow.

## Read-only preflight

Focused existing-database SELECTs found zero TenderApprovals, CommitteeVotes, TenderAwards and TopCommittee assignments, no duplicate vote/award groups, and a currently non-nullable DecidedAt column. These results reflect inspection time; repeat preflight immediately before any separately approved application. No existing records were altered.

## Safe verification

- Solution build: zero warnings/errors, including Razor compilation.
- --top-committee-rules-only: 128 database-free voting, secrecy, fixed-roster, stale/duplicate vote, status and award-authority checks passed.
- SchemaChecks: model/snapshot equivalence, reviewed migration operations, filtered/unique indexes and 37 required tables passed; seven migrations exist in source, while the seventh is NOT applied to SQL Server. --write-top-committee-script only writes the review file locally.
- Existing manager-review checks: 40 domain and 29 MVC checks passed using memory-only fixtures.

No full SQL workflow, concurrency, notification, browser or award-persistence verification is claimed. These depend on the remaining module implementation and separately approved SQL tests. The prepared rules are not a substitute for the transactional service/controller authorization that must be wired next.

## Next implementation after database approval

- Reinspect the migration history, nullable columns, duplicate votes/awards and existing round data; stop on a conflict. Apply only the reviewed migration after explicit approval, without seeding or advancing unrelated migrations.
- Add appointed-manager-only candidate/assignment and reasoned replacement services. Derive exclusions from actual tender preparation, evaluation involvement and report approvals; do not trust posted account or member identifiers.
- Build transactional round opening, replacement/abandonment, all-four voting, no-consensus reopening and responsible-officer award confirmation with the existing serializable tender lock, current eligibility checks, stale versions, audits and atomic notifications.
- Preserve vote secrecy in every reader: committee pages, existing decision reports/exports, notifications and audit previews. Never expose another member's choice/reason while fewer than four confirmed votes exist, including abandoned rounds. Own-vote access still requires current record eligibility.
- Extend report-current/version selection and revision handling for Top Committee rejection/detail requests; existing ManagerApproved history must remain immutable, not deleted to allow a revision.
- Implement the required Razor screens and role-aware navigation only after the service paths are ready. Keep suppliers restricted to their own award outcome and preserve existing request/tender/bid access.
- Prepare and request separate approval for temporary SQL write/concurrency tests. The current approval request concerns the migration only, not test-data creation.
