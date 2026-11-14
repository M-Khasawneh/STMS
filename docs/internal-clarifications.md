# Internal clarification workflows

Implements V5 clarification role pairs using the existing ClarificationRequests, ClarificationResponses, Notifications and AuditLogs tables. Supplier compliance conversations and attachment handling remain unchanged.

## Approved decisions

| Pair | New questions allowed |
| --- | --- |
| Request creator / eligible reviewing officer | Submitted, UnderReview, Returned |
| Tender creator (SourcingOfficer) / effective evaluation member | UnderEvaluation |
| Effective evaluation member / appointed SourcingManager | UnderEvaluation, AwaitingManagerReview |
| Appointed SourcingManager / effective Top Committee member | AwaitingCommitteeDecision |

Communication is bidirectional. A question has one selected eligible recipient, one immutable response, then sender-only resolution. There is no reopen, edit or delete action. Self-messaging is forbidden. Eligibility is derived from current enabled Identity accounts, employee departments, current roles and tender-specific effective appointments. Admin and supplier combinations are excluded from internal conversations.

The request officer is the most recent recorded reviewer. Before the first review, existing cross-department, non-owner review eligibility determines the available officers. The user explicitly approved this fallback; it does not assign an exclusive reviewer or change requisition permissions.

Stage changes prevent new questions outside the approved stage. Existing parties may still read, answer or resolve while they remain currently eligible for that transaction. Membership removal, account suspension or loss of the required role removes access. Internal questions do not block stage transitions. Supplier-compliance questions retain their existing resolved-before-evaluation prerequisite.

## Implementation

- ClarificationsController: Index, Details, Ask, Reply, Resolve; role authorization, antiforgery and no-store responses.
- InternalClarificationManagement: fresh participant queries, recipient-scoped threads, record-scoped lookup, notifications and audit in one transaction.
- InternalClarificationRules: approved stage/pair matrix, current membership and department checks, one-response state transitions and stale-page hashes.
- Razor Views/Clarifications and clarifications.css: inbox, recipient selection, question/response history, responsive layout and reduced-motion support.
- Existing requisition/tender/offer/evaluation/report details show a link only when the service finds authorized participants or conversations. Sidebar and existing notification destinations link to the protected pages.

Internal records link to exactly one requisition or tender and never set BidId. This keeps confidential supplier-compliance storage and its evaluation gate separate. Question/response contents are HTML-encoded; notification previews carry no question text. UTC dates display in Asia/Amman. Resolution dates/actions are retained in transaction audit records.

Requisition writes use a serializable transaction with the existing requisition row-lock pattern; tender writes reuse IBidData.BeginAsync. Authorization, stage, request scope and version are checked inside the transaction. A final participant/stage check runs before commit. Concurrent replies/duplicate submissions and stale pages cannot bypass the shared record lock and state/version validation. Conflicting committee or response data produces an error; it is not repaired automatically.

## Database boundary

No entity, model configuration, migration, seed or database command changed. No SQL Server connections or writes were executed for verification. Real SQL persistence, rollback under injected failure and parallel transaction behavior still require separately approved SQL tests. Top Committee conversations depend on actual effective tender-specific appointments; this module does not add committee appointment, voting or award functionality.

## Verification

- Solution build, including Razor: zero warnings/errors.
- Database-free schema metadata: 37 required tables, six existing migrations, model/snapshot/relationship checks passed.
- --internal-clarification-checks-only: 127 database-free policy/lifecycle/version checks and 17 compiled MVC role/binding/encoding/antiforgery checks.
- tests/internal-clarification-browser.cjs: 44 Edge checks at 1440/768/390/320px; question, response, resolution, immutability, sidebar keyboard/mobile behavior, reduced motion and zero JavaScript errors. Uses a marked presentation-only memory service, not the EF persistence implementation.
- Regressions: compliance 61 domain/private-file + 25 MVC; manager review 40 rules + 29 MVC; evaluation 15 MVC; workspace 45; bid Phase 4 54; default memory-backed HTTPS Identity/employee/department suite passed.

Safe reproduction uses the existing external build artifacts path and test executable. The --internal-clarification-browser-fixture mode is test-assembly-only and prints a memory-only loopback URL. The browser test refuses hosts missing X-STMS-Bid-Fixture: memory-only. All screenshot artifacts stay outside the repository.
