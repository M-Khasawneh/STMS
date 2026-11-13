# Development Roadmap

## Current boundary

Phase 1 requirements analysis is complete. The confirmed technical baseline is ASP.NET Core MVC targeting .NET 10 (`net10.0`), Entity Framework Core 10, SQL Server, ASP.NET Core Identity, and Razor Views with HTML, CSS and JavaScript, developed in Visual Studio 2026. All required pages and dashboards are in scope. ASP.NET Core Web API is excluded. No project, application code, packages or migrations have been created; implementation still requires approval.

The nine future implementation work packages below are the delivery sequence for one developer using the confirmed stack. Their numbering is separate from completed Phase 1 analysis: implementation package 1 is the next implementation phase (Phase 2 overall). Each package can be implemented and verified in small increments after approval. Every module includes its MVC controllers, Models/ViewModels, Razor Views, authorization, client/server validation and tests. Role dashboards develop alongside their modules. The initial procurement path uses real requests, invitations, bids, human evaluations and decisions; mandatory advanced features and at least two optional choices remain required for final source compliance.

## Implementation prerequisites

Architecture, framework, ORM, database engine, presentation technology and IDE decisions are settled. Resolve [open-questions.md](open-questions.md) before each affected package rather than treating all remaining questions as project-creation blockers. Foundation decisions include account/company relationships, initial Admin provisioning, role combinations, supplier activation behavior and upload policy. Request reviewer, record scope, committee assignment/voting, scoring, forwarding/award authority, deadlines and rework rules must be settled before their modules. Investigate biometric acceptance/device feasibility early and select at least two optional features before their implementation. No missing business rule becomes a developer-chosen default.

## Future work packages

### 1 Identity and administrative foundation

**Features:** MVC project foundation and shared responsive Razor layout/navigation; Identity login/logout/access-denied screens; seven roles; Admin-created employee accounts; role assignment and enable/disable; departments; supplier registration pages and Admin verification pages with states/reasons; initial Admin dashboard using available real counts. Establish client/server validation, protected uploads/downloads, antiforgery, safe error handling and audit capability. Complete dashboard request/tender counts when their data modules exist; do not fabricate metrics.

**Dependencies:** user approval to implement the confirmed .NET 10/EF Core 10 MVC stack; SQL Server development instance/configuration; account lifecycle, initial Admin provisioning, supplier required fields/documents and ownership decisions. Keep one MVC application with straightforward controllers, Models/ViewModels and narrowly scoped services where needed; no extra architectural layers are required.

**Data:** original AspNetUsers, AspNetRoles, Departments, SupplierProfiles, AuditLogs; provider-required Identity membership data; agreed registration-document storage.

**Permissions:** Admin account/department/verification operations; supplier own application; inactive supplier has no participation access. Operational scope must match the approved role analysis.

**Acceptance:** employee can authenticate; supplier cannot activate before approval; Admin records verification/rejection/suspension reasons; role changes and disabled status have the agreed effect; confidential registration documents remain protected.

**Tests:** valid/invalid credentials, disabled account, attempted self-approval/role escalation, pending supplier access, required-field checks, unsafe uploads and verification audit history.

**Concrete task order:** confirm outstanding foundation rules; create the MVC project targeting `net10.0`; configure EF Core 10 SQL Server persistence and Identity; establish user/role/department/supplier models and validated ViewModels; securely provision initial Admin and roles; implement MVC authentication views/actions; implement employee/department management; implement supplier registration/document protection and verification; add shared layout and initial dashboard; verify permissions, validation, antiforgery, audit and safe errors. Package installation and any first migration are future implementation work only after approval; neither is authorized by this planning update. Biometric login remains mandatory in package 7 and credential login is an intermediate milestone.

### 2 Purchase request lifecycle

**Features:** department requests/items; title/reason/priority/target date/quantity/specifications; submission, review, approval/return/rejection; department history and review notes. Support source statuses without inventing review authority.

**Dependencies:** package 1; agreed reviewer, department visibility/edit rules, return/resubmit path and item validation. Start gathering real timing facts needed for approval reports.

**Data:** PurchaseRequisitions, PurchaseRequisitionItems, agreed review/history records, AuditLogs.

**Permissions:** department authors/authorized colleagues as agreed; designated reviewer only; officer sees approved requests according to agreed scope.

**Acceptance:** valid request can proceed to approval; returned request retains notes; unauthorized department cannot read or change it; unapproved request cannot feed a tender.

**Tests:** missing items/fields, invalid quantities/date, wrong department, unauthorized review, concurrent review, resubmission and complete audit of decision sequence.

### 3 Tenders supplier invitations and bids

**Features:** tender creation from approved request; title/description/terms/dates; supplier selection/invitation; acceptance/decline; supplier pricing/specifications/delivery and protected documents; own bid history; capture pre-approved criteria before opening.

**Dependencies:** packages 1–2; request-to-tender cardinality; criterion approval authority; invitation prerequisite; submission boundary, bid count/revision/withdrawal rules; supplier eligibility at each stage.

**Data:** Tenders, TenderSuppliers, Invitations, Bids, BidItems, BidDocuments, EvaluationCriteria and audit/history evidence.

**Permissions:** officer manages tenders/invitations; supplier only its addressed invitations and own bids/documents; criteria changes restricted according to approved rule; bid read timing agreed before officer/committee review.

**Acceptance:** approved request reaches a tender and addressed supplier; supplier submits required bid data before deadline; no competitor data is exposed; pre-opening criteria are preserved.

**Tests:** unapproved conversion, bad dates, duplicate invitation/submission according to agreed rules, expired/declined invitation, exact deadline, unauthorized item/document access, interrupted upload, supplier suspension, zero bids.

### 4 Compliance evaluation and unified report

**Features:** compliance three outcomes and reasons; four-position evaluation assignment; technical media/document/sample observations; technical and financial scores and notes; required financial factors; supplier comparison; unified report with all mandatory content and substantiated recommendation.

**Dependencies:** package 3; agreed mandatory-document list, compliance gate/cure rule, scoring/weights/currency/rounding, member duties, technical-result approval, report submission process and exceptional criterion-change procedure.

**Data:** EvaluationCriteria, TechnicalEvaluations, FinancialEvaluations, EvaluationReports; approved compliance/membership/cost/evidence structures; AuditLogs.

**Permissions:** officer compliance; actual evaluation members only; Admin excluded from scoring; assigned manager acts as evaluator only within that assignment.

**Acceptance:** human committee can evaluate submitted offers and send one complete report to manager; reasons/evidence are traceable; AI is not required to make this human path function, but remains mandatory later.

**Tests:** noncompliant reasons, Pending Clarification handling, unrelated evaluator, criterion/bid tender mismatch, weight and score validation against approved rules, missing member/results, report completeness and locked-criteria changes.

### 5 Manager review final decision award and closure

**Features:** manager approve/reject/return with comments; policy-required forwarding; four-member Top Committee review, voting evidence and collective decision; requests for details; winner recording, closure and required notifications.

**Dependencies:** package 4; forwarding threshold/criticality, voting/quorum and conflict rules, ordinary-tender final authority, rejection/return routes, award/closure state mapping and authorized recorder.

**Data:** TenderApprovals, EvaluationReports, Tenders, Notifications, audit/history, agreed committee-vote and award representation.

**Permissions:** manager review; assigned Top Committee collective decision; only authorized award executor; notifications disclose only permitted data.

**Acceptance:** approved policy branches and rework paths function; a real request can reach an authorized final award and closure with attributable decisions. Report versions remain linked to the decision actually made.

**Tests:** duplicate/conflicting decisions, absent member/quorum/tie rules, return and re-review, rejected recommendation, altered report, wrong tender's recommended bid, notification failure and supplier-safe award visibility.

This package completes the initial human procurement path with its MVC pages and decisions. Clarification-dependent branches remain incomplete until package 6. It does not complete all original requirements.

### 6 Role based clarification and inquiry

**Features:** all five allowed role pairs; automatic participant identification by involvement/stage; subject/detail/questions/responses/dates; Pending/Answered/Resolved; links to request/tender/offer; new-request and response notifications; transaction history.

**Dependencies:** existing workflow records and package 1 authorization; agreed stage matrix, recipient and resolver rules, post-deadline clarification behavior. Keep earlier packages' clarification-dependent scenarios pending until this package is complete; do not replace them with unrestricted messaging.

**Data:** agreed clarification question/response representation; Notifications; links to source records; audit history.

**Permissions:** permitted pair plus current involvement/stage; owning supplier only; no generic direct messaging or evaluator-to-supplier grant.

**Acceptance:** eligible recipients are derived correctly; response changes state as agreed; authorized record history includes all dates/questions/responses; correct recipients receive notifications.

**Tests:** five valid pairs and invalid pairs, unrelated same-role users, supplier isolation, stale membership, wrong related record, resolution authority, repeated responses and notification visibility.

### 7 Remaining mandatory advanced integrations

**Features:** both face and fingerprint sign-in tied to Identity with required Razor login/JavaScript interaction; static face upload rejection; Ollama extraction/comparison/strengths/weaknesses/preliminary recommendation presented to authorized humans; full SpeechRecognition voice-to-text search on the employee dashboard task list, backed by authorized MVC task searching.

**Dependencies:** biometric feasibility decision from prerequisites; approved devices/client coordination; defined task/search scope; Ollama model/hosting/data formats and resource limits; completed data isolation and human evaluation path. No unapproved biometric or transcription product is introduced.

**Data:** account-linked credential representation chosen with Identity; authorized task queries over existing records; agreed analysis/input/evidence retention. No automatic Task table or raw biometric store is assumed.

**Permissions:** verified account only; authorized internal bid-analysis access; AI cannot approve; employee search sees only permitted tasks; suppliers cannot compare competitors.

**Acceptance:** MVC sign-in verifies approved biometric proofs and secure failures; both face and fingerprint are demonstrated with the required device/browser interaction; authorized views show advisory AI comparisons with human control; the employee dashboard uses SpeechRecognition to populate and execute authorized task search. Server search alone does not complete this package.

**Tests:** biometric enrollment/replay/failure/revocation plus actual face/fingerprint demonstrations; AI known-answer extraction, injection, timeout and hallucination; task scope and malformed text; required MVC/browser voice, microphone-permission and browser-support integration.

### 8 Reports public data and two selected optional features

**Features:** MVC report pages for all source categories, dynamic actual-data statistics, search/filter and PDF/Excel exports; own supplier history; Top Committee comprehensive statistics and transaction frequency; at least two selected optional features. Complete public home, logo/name/navigation, hero/CTAs, six-step explanation, About and Contact content/form/details as required, real public statistics, and all required role dashboards.

**Dependencies:** agreed metrics/report scope/refresh expectations; award/contract/performance data definition; two optional selections; QR destination, watermark supported formats or signature rules as applicable.

**Data:** original business tables and history/expiry fields; optional metadata only where justified; no separate analytics platform is assumed.

**Permissions:** role-specific reports, supplier-only own data, restricted internal decision/audit content, approved public aggregate scope.

**Acceptance:** every P102–P108 report requirement is implemented in MVC and validated against actual records; exports retain isolation; at least two selected optional requirements are complete across their full source scope. Public pages, report presentation and required dashboards are implemented, responsive and protected according to role; statistics use actual data.

**Tests:** counts/timing/denominators against known records, zero-data results, exported content and file authorization, supplier/committee boundaries; QR authorization after scanning, watermark identity/original preservation and view/download behavior, or signing/verification if selected.

### 9 Complete verification documentation and assessment preparation

**Features:** verify all original functional/security/quality requirements across the full MVC application; close requirement gaps; document design and operation; prepare the required website demonstration and technical assessment.

**Dependencies:** packages 1–8; working required MVC/browser interactions and biometric hardware; no unresolved rule that changes implementation acceptance.

**Data and permissions:** verify all persisted relationships, statuses, ownership, membership and decision evidence; no new tables/features implied.

**Acceptance:** trace every original requirement to full-application evidence and resolve remaining implementation gaps; complete all four mandatory features and at least two optional features; verify required pages/dashboards and responsive desktop/tablet/mobile behavior; prepare five-minute slides, ten-minute live MVC website demo and approximately ten-minute individual technical discussion per source. Missing browser/device functionality cannot be treated as completed by server logic alone.

**Tests:** full end-to-end happy path plus returns/rejections, concurrent actions, authorization/ownership negatives, upload/download security, validation, audit completeness, errors and integration failures; verify report correctness and advanced features. No test has been run against an application during Phase 1 because no application exists.

## Solo developer execution guidance

Within each approved package, complete one small coherent operation with its persistence, validation, permissions and verification before adding another. Keep audit and isolation working throughout rather than postponing them to the final review. Revisit design only when a requirement or resolved question demands it. Do not add extra architectural layers, messaging platforms, integrations or product modules without a requirement or approval.

The next implementation phase is **Identity and administrative foundation** (implementation package 1, Phase 2 overall), using the confirmed MVC/.NET 10/EF Core 10/SQL Server/Identity/Razor stack in Visual Studio 2026. Resolve its foundation questions and obtain explicit approval before creating application code, installing packages or generating migrations.
