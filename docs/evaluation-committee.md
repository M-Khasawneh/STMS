# Evaluation committee membership

Sourcing Managers manage each tender's evaluation committee from the tender details page. The service rechecks the current Identity roles, lockout, employee department and supplier profile. Admin accounts, including combined Admin/Manager accounts, cannot manage confidential committee membership through this endpoint.

Four distinct employees occupy the V5 positions. The manager seat requires SourcingManager. Department, financial and technical representatives are selected from employees with the existing TechnicalFinancialEvaluationCommittee role. The department representative must belong to the source requisition department. Financial and technical seats identify the appointment's responsibility. No new occupational roles or qualifications are created.

The existing CommitteeAssignment, CommitteeMember, EffectiveFrom and EffectiveTo fields are reused. An appointment changes membership only for its tender. Replacing a member ends their membership at the current UTC instant and creates a new history row. Earlier rows and evaluation scores are preserved. The existing protected bid queries already enforce effective tender-specific membership and process stage.

After the first TechnicalEvaluation or FinancialEvaluation row is recorded, membership changes require a reason. Each assigned or replaced seat produces an existing AuditLog entry with actor, tender, previous/new user IDs, position, reason and timestamp. The screen displays employee names, membership periods and change reasons. Times use Asia/Amman display conventions.

Saves use the existing serializable tender lock and a version hash of all membership history and evaluation-start state. Duplicate/stale forms and simultaneous updates cannot produce competing current seat assignments through this service. Existing conflicting or scheduled membership records are rejected for explicit review. The module does not change tender status or implement scoring/report decisions.

## Files

- `Controllers/CommitteesController.cs`
- `Data/CommitteeManagement.cs` and `Data/CommitteeRules.cs`
- `Models/Bids/CommitteeModels.cs`
- `Views/Committees/Edit.cshtml` and `Problem.cshtml`
- Existing Program registration and manager-only tender details link
- `tests/SourcingTenderManagementSystem.AuthenticationChecks/CommitteeSqlChecks.cs`
- Shared SQL fixture's explicit `--committee-sql-checks-only` mode

## Database and verification

No entity, model configuration, migration or schema changes are required. Read-only inspection found zero existing evaluation assignments/members and no conflicts before test writes.

The approved temporary SQL runner verifies distinct seats, supplier denial, duplicate/stale forms, pre/post-evaluation replacements, required reasons, immediate revocation/grant of protected bid access, audit-failure rollback, concurrent updates, retained earlier scores and assignment uniqueness. It uses the shared isolated fixture with fresh scoped EF contexts and preserves existing Identity users and role assignments. It removes all temporary committee, evaluation, criterion and related transaction records afterward.

The MVC/Razor solution build and existing database-free bid, compliance and schema regression runners also apply. A live authenticated browser walkthrough of this new screen remains a visual review step. Scoring, report authoring and later-stage workflows require their own implementation approval.
