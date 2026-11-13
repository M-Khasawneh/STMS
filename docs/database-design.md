# Database Requirements and Conceptual Design

## Design status

Source section 9 gives a preliminary table outline, not a physical schema. It permits adjustments but supplies no field definitions, key types, lengths, indexes, SQL engine or ORM. The user has now confirmed SQL Server with Entity Framework Core 10 for the ASP.NET Core MVC application targeting .NET 10 (`net10.0`) in Visual Studio 2026. The 19 original tables below are preserved by name and purpose. All field names, keys, cardinalities and constraints below remain **suggested design details derived from the workflow**, subject to confirmation. They are not original database specifications or an implemented database.

ASP.NET Core Identity is required and will use EF Core 10 persistence in SQL Server. Identity key type, application-user extensions and schema customization remain design details to finalize before the first migration. The full application uses Controllers, Models and Razor Views with HTML, CSS and JavaScript; no Web API is included. No provider packages or migrations have been installed or generated.

## Logical notation

`ID` means a stable identifier with physical key type undecided; `UserID` uses the chosen Identity user-key type. `Text`, `LongText`, `Decimal`, `Integer`, `Date`, `Timestamp`, `Boolean`, `Binary` and `Status` are logical types. Candidate SQL Server mappings are `nvarchar(n)`, `nvarchar(max)`, `decimal(p,s)`, `int`/`bigint`, `date`, `datetime2` or `datetimeoffset`, `bit`, and `varbinary(n/max)` respectively; statuses need an agreed constrained representation. Here `Timestamp` means an event time, not SQL Server's `timestamp`/`rowversion` concurrency type. Precision, scale, lengths, timezone convention and nullability remain to be finalized. Money and quantity should use exact decimals rather than floating point.

Each business table is suggested to have one `...ID` primary key unless specified otherwise. Status fields use only applicable source vocabulary until rules are agreed. Suggested timestamps/actor references support the explicit history/audit/report obligations. Required relationships should be enforced by foreign keys; delete behavior must preserve submitted procurement records and be agreed before physical design.

## Original table inventory with tentative properties

| Original table and source purpose | Suggested properties and keys | Suggested relationships and constraints |
|---|---|---|
| **AspNetUsers** — Identity login accounts | Identity-managed user key PK, username, email and standard Identity authentication fields; candidate DepartmentID FK for employee affiliation; enabled flag if selected Identity model needs one | One user to roles via Identity membership; one department to many employees is tentative; do not replace Identity password handling |
| **AspNetRoles** — roles | Identity-managed role key PK, name and normalized name | Seven business roles; role name uniqueness managed through Identity |
| **Departments** — company departments | DepartmentID PK; Name Text; optional Code Text and active flag are suggestions | Many requests belong to one department; department uniqueness rules are open |
| **SupplierProfiles** — company/tax/legal verification and linked UserID | SupplierProfileID PK; UserID FK; CompanyName Text; TaxNumber Text; RegistrationNumber Text; ContactName/Email/Phone Text; legal verification Status; review reason LongText; reviewer UserID FK; reviewed Timestamp | User link is explicit; 1:1 is a candidate from singular UserID, not a confirmed company-user cardinality; legal-number uniqueness/jurisdiction unprovided |
| **PurchaseRequisitions** — department purchase requests | PurchaseRequisitionID PK; DepartmentID FK; CreatedByUserID FK; Title Text; Reason LongText; Priority Status/value; TargetDate Date; Status; CreatedAt/SubmittedAt timestamps | One department/user to many requests; one request to many items; priority values and approval authority unresolved |
| **PurchaseRequisitionItems** — quantities/specifications | PurchaseRequisitionItemID PK; PurchaseRequisitionID FK; Description Text; Quantity Decimal; Specifications LongText; candidate LineNumber Integer | Each item belongs to one request; positive quantity is proposed; unit of measure and mandatory item attributes unspecified |
| **Tenders** — tenders from approved requests | TenderID PK; candidate PurchaseRequisitionID FK; CreatedByUserID FK; Title Text; Description/Terms LongText; SubmissionStart/SubmissionDeadline Timestamp; Status | Source implies origin in approved request; one FK assumes one request per tender and is **not confirmed**; start before deadline is proposed temporal validation |
| **TenderSuppliers** — invited/participating supplier link | TenderSupplierID PK; TenderID FK; SupplierProfileID FK | Candidate unique (TenderID, SupplierProfileID); joins tenders to suppliers; relationship to Invitations must avoid duplicate independent participation state |
| **Invitations** — invitation details/responses | InvitationID PK; TenderSupplierID FK; SentByUserID FK; SentAt Timestamp; Status; RespondedAt nullable Timestamp; proposed ExpiresAt Timestamp | Candidate one invitation per TenderSupplier, or one-to-many if resending/history is required; do not also copy supplier/tender FKs without a consistency constraint |
| **Bids** — supplier offers | BidID PK; TenderSupplierID FK; Status; DeliveryTimeframe Text; TechnicalDetails LongText; SubmittedAt Timestamp; Currency Text; PaymentTerms Text; OfferValidUntil Date | One tender-supplier to bids; one vs multiple/revised bids unresolved; monetary totals derived from items/costs or immutable submission snapshot, never unchecked client totals |
| **BidItems** — itemized pricing/specifications | BidItemID PK; BidID FK; PurchaseRequisitionItemID FK candidate; OfferedQuantity Decimal; UnitPrice Decimal; Specifications LongText | Tie each offered line to requested item if all tenders follow request lines; partial/alternative offers unresolved; cross-tender/request linkage must be prevented |
| **BidDocuments** — bid attachments and legal documents | BidDocumentID PK; BidID FK; DocumentType Text; OriginalName Text; StorageReference Text; ContentType Text; Size Integer; UploadedAt Timestamp; proposed content digest Binary/Text | Each attachment belongs to owning bid; source also mentions legal documents, but registration-time legal uploads exist before a bid and need a separate model or explicit scope change |
| **EvaluationCriteria** — tender criteria and percentage weights | EvaluationCriterionID PK; TenderID FK; Name Text; Description LongText; Kind technical/financial; Weight Decimal; proposed MaxScore Decimal; approval actor/date and version references if approved | One tender to many criteria; pre-approval before opening and documented post-receipt exception required; exact weights/score scales and approval authority open |
| **TechnicalEvaluations** — technical scores/notes | TechnicalEvaluationID PK; BidID FK; EvaluationCriterionID FK; EvaluatorUserID FK; Score Decimal; Notes LongText; NonCompliance LongText; EvaluatedAt Timestamp; candidate revision | Proposed grain one evaluator × bid × criterion × revision; source does not settle individual versus collective scoring; criterion and bid must share tender |
| **FinancialEvaluations** — financial scores/notes | FinancialEvaluationID PK; BidID FK; EvaluationCriterionID FK candidate; EvaluatorUserID FK; Score Decimal; Notes LongText; evaluated timestamp; financial cost references as needed | Grain and score formula unresolved; need evidence for taxes/fees and applicable transport/installation/maintenance; avoid duplicating submitted prices as editable evaluation facts |
| **EvaluationReports** — unified report/recommendation | EvaluationReportID PK; TenderID FK; PreparedByUserID FK; TechnicalSummary/FinancialSummary/Observations/NonCompliance/Comparison/FinalResult/Recommendation LongText; proposed RecommendedBidID FK; SubmittedAt Timestamp; decision Status/reference; revision | Required report content includes actual technical/financial scores via evaluation relations or preserved versioned snapshot; candidate many versions, one current submitted report; recommended bid must belong to tender |
| **TenderApprovals** — Manager and Top Committee decisions | TenderApprovalID PK; TenderID FK; EvaluationReportID FK; Stage manager/top committee; Decision Status; ActorUserID FK nullable for collective decision; Comments/Justification LongText; DecidedAt Timestamp; proposed committee-decision reference | Append decisions rather than overwrite; group decision needs group identity and voting evidence; do not attribute a collective decision solely to one approver |
| **Notifications** — notifications/alerts | NotificationID PK; RecipientUserID FK; Subject Text; Message LongText; CreatedAt Timestamp; related record reference; optional ReadAt Timestamp | Many notifications per user; related-record access must be rechecked; channel/read tracking not specified |
| **AuditLogs** — security/audit history | AuditLogID PK; ActorUserID FK nullable for system/authentication events; Action Text; Timestamp; record type/ID; previous/new status or selected change details; reason | Append-only behavior is proposed to meet clear complete audit; retention/readers/redaction policy open; never store secrets or biometric templates here |

Field lists provide a conceptual design, not a complete migration-ready schema. No assigned PK implementation, extra field, unique business rule, or table extension is approved by this analysis.

## Relationship summary

| Relationship | Source support | Decision still needed |
|---|---|---|
| User ↔ Role | RBAC and Identity explicit; EF Core 10 SQL Server persistence confirmed | Identity key/customization details and multiple-role restrictions |
| Department → Request → Request items | Requesting department and itemized requests explicit | Employee affiliation cardinality, who sees/edits department data |
| Approved request → Tender | Explicit workflow | Split/consolidation and one-to-many vs many-to-many |
| Tender ↔ Supplier through TenderSuppliers | Explicit linking table | Invitation history, eligibility and participation lifecycle |
| TenderSupplier → Invitation/Bid | Derived normalization of original tables | Number of invitations and bids per participation |
| Bid → BidItems/BidDocuments | Explicit source tables | Requested-item mapping, supplementary documents and revisions |
| Tender → Criteria; Bid → Evaluations | Explicit purposes | Criterion version, evaluator grain, aggregate formula |
| Tender → EvaluationReports → TenderApprovals | Explicit report and decision sequence | Versioning and decision linkage to exact report |
| User → Notifications; actor → AuditLogs | Explicit purposes | System actors, retention and related-record model |

## Suggested supporting structures for existing requirements

These are **not new features** and **not original table names**. They identify missing data storage needed by explicit requirements. Confirm names and whether each is a separate SQL Server table, an extension, or an Identity facility within the confirmed EF Core 10 design. Do not create all suggestions automatically.

| Suggested structure | Requirement served | Candidate keys/properties/relationships |
|---|---|---|
| Identity user-role and support records | Identity/RBAC P096/P099 | EF Core Identity membership and support tables, including AspNetUserRoles, AspNetUserClaims, AspNetRoleClaims, AspNetUserLogins and AspNetUserTokens as applicable to the selected Identity configuration; finalize keys before migrations |
| SupplierDocuments | Registration legal uploads, licences, expiry reports P041/P098/P104 | SupplierDocumentID PK; SupplierProfileID FK; type/storage/expiry/verification; no fake BidID for registration documents |
| PurchaseRequisitionReviews | Request review notes/history and approval-duration reports | ReviewID PK; RequestID FK; ReviewerUserID FK; outcome/comments/date; preserve each review cycle |
| CommitteeAssignments and CommitteeMembers | Two four-person committees and involvement restrictions | CommitteeID PK; TenderID FK; Type; MemberID PK or (CommitteeID, UserID) candidate; UserID FK; position; effective dates if membership history approved |
| CommitteeVotes | Voting-record report P106 | VoteID PK; CommitteeID/ApprovalID FK; MemberUserID FK; option/justification/date; uniqueness per member/decision cycle only after voting rules settled |
| ComplianceReviews and ComplianceReviewItems | Three outcomes, missing requirements and reasons P021–P024 | ReviewID PK; BidID FK; ReviewerUserID FK; outcome/reasons/time; child requirement/document reference if item-level checks selected |
| ClarificationRequests and ClarificationResponses | Mandatory questions/answers/status/history | RequestID PK; sender/recipient UserID FKs; subject/detail/status/date; ResponseID PK, RequestID FK, responder/time/text; request/tender/bid FK scope as agreed |
| CriterionVersions or documented-change evidence | Official exception to locked criteria P026 | VersionID PK; tender/criterion FK; approver, reason/date and evidence; exact procedure unknown |
| FinancialCostComponents | Taxes, fees, transport/installation/maintenance P064–P065 | CostID PK; BidID or BidItemID FK; cost type/amount/currency; document evidence; structure depends on cost/quantity rules |
| AiBidAnalyses | Reproducible Ollama advisory output P173 | AnalysisID PK; TenderID FK; input reference/version; model/prompt version; output/status/time; human review reference if retained |
| Biometric credential records | Account-linked mandatory authentication | Identity user FK; credential public-key/challenge verification data if passkeys selected; no default raw face/fingerprint table |
| Award record or award fields on Tender | Winner, final decision and awarded-contract reports P019/P106 | TenderID, selected BidID FK, decision FK, award date and contract reference if required; one/many awards and contract data scope unresolved |
| Transition history | Status tracking and metric evidence | Record FK/reference, old/new state, actor/time/reason; may be satisfied by existing audit/review records without another table |

“Related record type plus ID” cannot have ordinary referential integrity to multiple tables. For clarifications and notifications, candidate alternatives are explicit nullable request/tender/bid foreign keys with a scope constraint, or separate link records. Neither alternative is selected. If a bid link supplies tender/request context, derive that context rather than store contradictory references.

## Business constraints requiring confirmation

- Only approved requests enter tender creation; supplier activation requires Admin verification; competing supplier isolation is mandatory.
- Exactly four evaluation positions and four Top Committee members are stated. Distinct-user requirements, temporary vacancies, per-decision quorum and membership changes need rules; a row-count check alone cannot define them.
- Prices/quantities, date ordering, decimal rounding, currencies and taxes need validation, but exact limits, permitted zero/negative adjustments and aggregation formulas are not supplied.
- Percentage criteria weights suggest a defined total; whether technical and financial groups each total 100 or share one total is unknown. Do not enforce an assumed total across the wrong scope.
- Bid supplier/tender, criterion tender, report tender, recommended bid and decision tender must agree. Use relational design or transactional validation to prevent cross-tender associations.
- No required globally unique tax number, tender number format, one bid per supplier, one request per tender, or one award per tender is established by the source.
- Preserve prior submissions, reviews and decisions for history/audit. Hard deletion, archive and correction rules are open.

## Physical design work deferred

The engine/provider is settled: SQL Server through EF Core 10. Finalize ID strategy, field lengths/nullability, decimal precision/scale, timezone convention, transaction/concurrency strategy, foreign-key delete rules and indexes after approval. SQL Server edition/version, development instance and deployment connection settings remain operational choices. Likely access paths include department/status requests, tender/status/date, supplier invitations/bids, criterion/evaluator lookups, recipient/date notifications, and entity/date audit history. These justify later index analysis; no indexes or migrations have been created.

Report definitions also require data decisions: approval interval start/end and returns; participation denominator; evaluation delay threshold; legal-expiry dates; performance metrics; supplier transaction definition; and awarded-contract evidence. The schema cannot manufacture missing business measures.
