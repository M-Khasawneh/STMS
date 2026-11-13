# Open Questions and Decisions Required

## Decision status

The Word document is authoritative for business requirements. The user has confirmed ASP.NET Core MVC targeting .NET 10 (`net10.0`), Entity Framework Core 10, SQL Server, ASP.NET Core Identity, and Razor Views with HTML, CSS and JavaScript, developed in Visual Studio 2026. The full application includes all required pages and dashboards; ASP.NET Core Web API is excluded. These decisions resolve Q01–Q03 and Q15; their identifiers remain below for traceability. Other unanswered points require clarification before their affected implementation, not necessarily before creating the project. No missing business rule is assumed.

## Priority decisions

| ID | Question | Source or gap | Why it matters |
|---|---|---|---|
| Q01 — Resolved | ASP.NET Core MVC with Controllers, Models and Razor Views using HTML, CSS and JavaScript; no ASP.NET Core Web API | User-confirmed technical decision | Application style and presentation scope are settled |
| Q02 — Resolved | Target .NET 10 (`net10.0`) and use Visual Studio 2026 | User-confirmed technical decision | Official .NET 10 IDE baseline is settled |
| Q03 — Resolved | SQL Server with Entity Framework Core 10 and ASP.NET Core Identity persistence | User-confirmed technical decision | Engine/ORM are settled; physical schema and development connection settings still need finalization |
| Q04 ? Resolved | SourcingOfficer reviews requisitions across departments; cannot review own or own-department requests. Admin oversight is read-only. | User-approved requisition decisions | Implemented; see purchase-requisitions.md |
| Q05 ? Requisitions resolved | RequestingDepartment users access only their own requisitions; officers and Admin can view all. Officer assignment for later procurement records remains open. | User-approved requisition decisions | Requisition access implemented; later record assignment unresolved |
| Q06 | How are four evaluation members and four Top Committee members appointed, replaced and scoped? Are distinct people and fixed positions required? | P055/P082 | Defines record access, completion and membership persistence |
| Q07 | How does the manager's evaluation membership interact with subsequent manager review? Are conflict/self-review safeguards expected? | P055/P077–P079 | Source includes both responsibilities but no safeguard rule |
| Q08 - Criteria approval resolved | Officers configure, another SourcingManager approves, exactly 100 percent total; approved criteria are read-only. Post-bid exceptions are not implemented. | User-approved tender decisions | Implemented; see tenders-invitations.md |
| Q09 | What are technical/financial score scales, weight totals, pass thresholds, aggregation, normalization and tie rules? | P015/P026/P059/P069 | No valid scoring formula can be invented |
| Q10 | Which tenders must reach Top Committee: value threshold, currency/value basis, criticality and who classifies them? | P018/P079/P081 | Conditional forwarding must remain conditional |
| Q11 | What constitutes a collective decision: quorum, majority/unanimity, abstention, conflict, tie and missing vote handling? | P082/P106 | Four members does not define a voting rule |
| Q12 | Who makes/records the final award for tenders not requiring Top Committee? | P019 and conditional forwarding | Source does not settle ordinary-tender award authority |
| Q13 | How are Awarded and Closed represented at final closure? What are rejection/return/cancellation destination states? | P019/P113/P119 | Avoid contradictory or invented state transitions |
| Q14 | What demonstration of face and fingerprint satisfies the requirement, and is device-backed WebAuthn accepted? | P164–P170 | Mandatory biometrics cannot be completed by backend alone or silently replaced with PIN-capable passkeys |
| Q15 — Resolved | Implement the full MVC application, including all required pages, dashboards, forms, browser interactions and website demonstration | User-confirmed full scope; P088–P094/P168/P175/P207/P212 | Client presentation is required implementation work, not an excluded dependency; biometric hardware remains an external capability to verify |

## Detailed business questions

| ID | Question | Affected work |
|---|---|---|
| Q16 - Tender linkage resolved | Exactly one requisition per tender and at most one tender per requisition, converted atomically on first Draft save. Award cardinality remains future scope. | User-approved tender decisions |
| Q17 ? Requisition validation resolved | Low/Medium/High/Urgent; title/reason/date required; positive fractional quantities and mandatory specifications; at least one item and non-past date at submission. No unit field or separate service workflow added. | User-approved requisition decisions |
| Q18 ? Resolved for this module | Draft and Returned are owner-editable; explicit Under Review before decisions; corrected Returned requests resubmit; Approved and Rejected are read-only. Draft deletion is not implemented. | User-approved requisition transitions; see purchase-requisitions.md |
| Q19 - Resolved | Published is visible to invited suppliers; Open is an explicit officer action within the configured period, gated by approved criteria. Anonymous details are forbidden. | User-approved tender decisions |
| Q20 - Tender timing resolved | UTC storage, Asia/Amman display, exclusive deadline, explicit opening/closing; no automatic tender status changes or reopening. Bid revisions/withdrawal remain future scope. | User-approved tender timing |
| Q21 - Invitation lifecycle resolved | Pending accepts/declines once, no response changes/resends, unanswered invitations expire at period end. Bidding prerequisites remain future scope. | User-approved invitation decisions |
| Q22 | How many bids/alternatives/revisions may one supplier submit? Must all request items be quoted? | Bid structure and uniqueness |
| Q23 | When may officers/evaluators/managers view bids? Is technical evaluation separate from financial access? | Confidentiality and evaluation sequence |
| Q24 | Which required legal/bid documents and essential compliance terms apply? What are expiry/renewal rules? | Upload validation and compliance |
| Q25 | Does Non-Compliant automatically exclude? Who can reverse a result? May missing documents be cured after deadline, and can evaluation proceed while Pending Clarification? | Compliance transitions and fairness |
| Q26 | What do Bid Accepted and Rejected mean relative to compliance, evaluation and award? From which states is Withdrawn allowed? | Bid status semantics |
| Q27 | Do all four members score every criterion, or are there specialist duties? Who approves technical results and submits the unified report? | Evaluation grain and sign-off |
| Q28 | How are taxes, fees, discounts, transport, installation and maintenance calculated? How are currencies, validity and payment terms compared? | Financial data, exact decimals and formulas |
| Q29 | What happens after manager rejection, manager return, Top Committee rejection, or request for details? Which report changes need re-evaluation/reapproval? | Rework and decision history |
| Q30 | Can Top Committee choose a supplier different from the recommendation? Who records award, closes tender and notifies which parties? What award information can suppliers/public see? | Final authority and disclosure |
| Q31 | Who may cancel, at which stages, with what reason, and what happens to bids/invitations/evaluations/notifications? | Cancelled status |
| Q32 | Are awarded contracts merely reporting references, or must additional contract data be captured? What defines a supplier transaction and performance? | Reports; no unrequested contract/fulfillment module assumed |

## Identity security and communication questions

| ID | Question | Affected work |
|---|---|---|
| Q33 | May one account hold multiple roles, and are Supplier/Admin/committee combinations restricted? | Privilege conflict and competitor isolation |
| Q34 | Is SupplierProfiles linked one-to-one with an account, or can a company have multiple users? How are duplicate tax/registration numbers defined by jurisdiction? | Identity ownership and company uniqueness |
| Q35 | May pending/rejected/suspended suppliers sign in to view/correct applications? What are reinstatement and existing-bid rules? | Account eligibility vs supplier status |
| Q36 | How is initial Admin provisioned? What are credential delivery/reset/recovery, lockout, email verification, session duration and revocation policies? | Identity security decisions |
| Q37 | What file types, sizes/counts and media formats are permitted? How are malware scanning, storage protection and viewing/downloads handled? | Secure uploads and evaluation media |
| Q38 | Who may read detailed audit/security records, and what retention, redaction/correction, backup and confidentiality policies apply? | Audit and historical reporting |
| Q39 | For each clarification pair, which process stages permit opening/responding/resolving? Are recipients individuals or groups? | Automatic eligibility matrix |
| Q40 | Who resolves/reopens a clarification? Are multiple responses/attachments allowed, and does process closure stop communication? | Clarification state/history scope |
| Q41 | Must evaluator requests to suppliers be mediated through the Sourcing Officer as the allowed pair list suggests? | Reconcile technical clarification responsibility with role-pair restrictions |
| Q42 - Invitation notifications resolved | Database-backed supplier invitation notice and response notice to the responsible tender creator; no external services. Award/closure notification recipients remain future scope. | User-approved notification decisions |

## Advanced features optional choices and reporting questions

| ID | Question | Affected work |
|---|---|---|
| Q43 | What biometric hardware, OS/browser, enrollment proof, liveness level and fallback/recovery are required? Is raw biometric processing expected? | Face/fingerprint feasibility and data handling |
| Q44 | Which Ollama model/hosting/hardware, attachment formats, extraction/OCR requirements, latency and data retention are acceptable? How should failed/uncertain analysis appear? | Advisory bid analysis |
| Q45 | What constitutes an employee task and which fields/languages/filters should voice text search cover? What browsers must support SpeechRecognition? | No unrequested task-management module or server speech service |
| Q46 | Which two or more optional features are selected? | Minimum two remains mandatory; recommendation is QR plus watermark, pending selection |
| Q47 | If QR is selected, what deployed base address and protected MVC request/tender detail destination should be encoded? | QR generation cannot grant access by itself |
| Q48 | For digital signatures, what signature type/assurance, signer rules, provider/certificate and verification requirements apply? | Scope/cost of a possible selected option |
| Q49 | For watermarking, which user information and file formats are required, including viewing as well as download? | User-specific derivatives and preservation of originals |
| Q50 | Define average approval duration, participation rate, evaluation delay, document-expiration tracking, supplier performance and transaction frequency. What filters, export columns and refresh interval apply? | Accurate real-data reports; no additional expiry-alert feature is assumed |
| Q51 | Which counts are safe for public statistics; what are the six public workflow steps; what about/contact text is supplied, and is contact submission processing expected? | Preserve source homepage without inventing a contact-ticket feature |
| Q52 | Which report and historical record scopes apply to each internal role and Top Committee? | Role-specific reporting and audit disclosure |
| Q53 | Which example industry is intended, if any? Are any organization-specific terms supplied? | Examples do not create industry-specific business rules |
| Q54 | What measurable performance, concurrency, recovery, deployment, accessibility/localization and support expectations apply? | Non-functional requirements not quantified in source |

## Confirmed technical baseline

Use ASP.NET Core MVC targeting `net10.0`, Entity Framework Core 10, SQL Server and ASP.NET Core Identity, with Razor Views, HTML, CSS and JavaScript. Develop with Visual Studio 2026 (18.0 or newer) for official .NET 10 support. Keep the selected SDK and EF Core 10 packages on compatible servicing patches when implementation is authorized. See [Microsoft Visual Studio compatibility](https://learn.microsoft.com/en-us/dotnet/core/install/windows#net-versions-and-visual-studio) and [EF Core release guidance](https://learn.microsoft.com/en-us/ef/core/what-is-new/).

The earlier tooling conflict is resolved by the user's Visual Studio 2026 decision. No project, package installation, migration or IDE/runtime upgrade has been performed by this documentation update. SQL Server edition/version, development instance, Identity key strategy and physical schema details remain to be finalized without reopening the confirmed engine/ORM choice.

## Decision timing

- **Before implementation begins:** obtain user approval for the next phase. No architecture/framework/engine decision remains open. Confirm practical SDK/IDE availability and SQL Server development connection settings before project/persistence setup; do not change the machine during this planning task.
- **Before the foundation phase is completed:** settle Q33–Q37 as relevant to role combinations, supplier ownership/activation, initial Admin and credential/session policy, and supplier-document uploads; settle employee department cardinality and initial audit scope. Finalize Identity/business key types and field constraints before their first migration.
- **Before procurement modules:** settle the affected request reviewer, ownership, request/tender cardinality, deadline, invitation, bid-visibility and revision questions.
- **Before evaluation and decisions:** settle committee assignment, criteria approval/scoring, compliance cure/exclusion, forwarding, voting, award authority and rework/closure questions.
- **Before later feature modules:** settle clarification stage rules, AI model/deployment, task-search semantics, report metrics and optional-feature selections. Investigate biometric device/acceptance feasibility early; detailed implementation is in the mandatory-integration phase.

## Risk register

| Risk | Consequence | Analysis response |
|---|---|---|
| Technical decisions drift from confirmed scope | Implements a different application style or stack | Use confirmed MVC/net10.0/EF Core 10/SQL Server/Identity/Razor baseline; no Web API |
| Unknown approval/scoring/voting rules | Invalid authorization or disputed procurement outcome | Resolve before affected implementation; no default majorities, thresholds or price formulas |
| Supplier information exposed indirectly | Competing offers leak through files/reports/history/AI/notifications | Apply record scope throughout and test with competing suppliers |
| Manager evaluates and reviews | Conflict or self-review concern | Preserve source roles; request explicit safeguards rather than alter workflow |
| Required presentation or browser work omitted | Mandatory features and assessment fail | Implement required Razor pages/dashboards, device-backed login interaction, SpeechRecognition and live website demonstration |
| Raw image treated as face authentication | Direct violation of mandatory requirement and insecure access | Static image uploads cannot authenticate; validate approved fresh proof |
| Passkey proof mistaken for modality proof | Cannot demonstrate both face and fingerprint | Agree acceptance and test actual hardware modalities |
| AI hallucination/prompt injection | Incorrect advice or leaked procurement information | Limit context, validate against source evidence, preserve human authority |
| Optional minimum forgotten | Source compliance incomplete | Select and complete at least two options; none waived |
| Schema suggestions treated as original requirements | Unapproved complexity/features | Label every derived field/table/cardinality and defer physical design |
| Report measures lack definitions/data | Misleading analytics | Agree formulas and capture real supporting facts; no fabricated metrics |
| Tooling/package versions diverge | Build incompatibility or unsupported combination | Use Visual Studio 2026 and compatible .NET 10/EF Core 10 servicing versions |
| Scope expands into contracts, payments, chat or tasks | Solo-developer effort diverted to unrequested modules | Restrict design to stated functions; clarify references before adding products |

## Approval boundary

The next step is user approval of **Identity and administrative foundation** (implementation package 1, Phase 2 overall), with remaining foundation questions settled before their affected work. Technical stack and full MVC scope are confirmed; approval to implement has not yet been given. No implementation starts merely because these documents have been updated.
