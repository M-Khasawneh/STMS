# Top Committee voting migration — applied 2026-11-15

## Authorization and boundary

The user explicitly approved applying only 20261114220000_TopCommitteeVotingRounds. No business records, test users, seeds, SQL write tests, other migrations, application restart or new module functionality were authorized or performed in this step.

## Guarded execution

- tests/Apply-TopCommitteeMigration.ps1 defaults to read-only preflight. Its explicit -Apply switch applies this one reviewed script only. It rejects an unexpected database target, changed script hash, any migration history other than the exact six predecessors, pre-existing round columns/constraints, duplicate votes/awards or an unexpected award lookup index. Reapplying after success is deliberately rejected.
- Reviewed script SHA-256 (UTF-8, LF, one trailing newline): 3748F774F01AA0A1C08D85FC165936E0684529744271AF301FB3877E599C28EA.
- Integrated-security local development target is guarded. Credentials and real configuration remain outside version control.
- An exclusive transaction-owned application lock and serializable outer SQL transaction protect preflight, migration and verification. Before DDL, the command captures counts and server-side SHA-256 content fingerprints for all 38 pre-existing non-history tables, using deterministic primary-key ordering and the original column projection. The JSON contents never leave SQL Server.
- An initial single-batch execution failed SQL Server compilation when a constraint referenced columns introduced in that same batch. The transaction rolled back; another read-only preflight confirmed the exact original six-migration/column state. No partial change persisted.
- The same hash-verified script was then executed at its 12 reviewed operation-batch boundaries. The initial local-variable/default-constraint block stays intact. All batches remain inside one outer transaction, so a schema or data-verification failure rolls back the entire change.

## Verified before commit

- Exactly the target migration-history row was added; all six predecessor IDs/versions remained unchanged.
- TenderApprovals now has nullable RoundOpenedAt / RoundClosedAt datetimeoffset fields, RoundCloseReason nvarchar(max), RoundMemberSnapshot nvarchar(512), and nullable existing DecidedAt. No existing date was overwritten or manufactured.
- CK_TenderApprovals_TopRound exists, is enabled and trusted.
- UX_TenderApprovals_OpenTopRound is filtered and unique on TenderId / Stage.
- IX_CommitteeVotes_TenderApprovalId_CommitteeMemberId is unique on round / membership.
- IX_TenderAwards_TenderId is unique on TenderId, replacing only its former nonunique lookup index.
- Table inventory, row counts and all pre-existing projected contents matched the baseline across 38 tables. The outer transaction committed only after these checks passed. The approved EF migration-history insertion is the sole additional record.

## Independent post-commit verification

A fresh read-only SQL connection confirmed all seven applied migration IDs, the five field definitions/nullability, the actual three unique index key lists and the enabled/trusted check constraint. Model/snapshot/schema metadata checks passed for seven migrations and 37 required tables. Existing read-only reporting checks passed 87 real SQL read/filter/export checks with a SELECT-only interceptor and no file or database-row writes. No eligible existing TopCommittee account was available, so that role's live SQL report path was not exercised.

The full assignment, voting, report rework, award, notification and MVC workflows remain to be implemented. This schema application is not evidence that those workflows are complete. Temporary SQL workflow/concurrency tests still require separate approval when prepared.
