# Sourcing and Tender Management Requirements Analysis

## Purpose and authority

This is Phase 1 planning for a solo developer using Visual Studio 2026 and Codex. The confirmed implementation is a full ASP.NET Core MVC application targeting .NET 10 (`net10.0`), with Entity Framework Core 10, SQL Server, ASP.NET Core Identity, and Razor Views using HTML, CSS, and JavaScript. It includes all pages, dashboards, forms and functionality required by the source. ASP.NET Core Web API is excluded. No application code, packages or migrations have been created; implementation awaits approval.

The authoritative source is `Sourcing_Tender_Management_System_English_Brief_V5 (1).docx`. Its complete main-document text was read in document order, including every table cell. Paragraph references P001–P223 below are extraction locators, not page numbers. Footer text and empty note parts were also inspected. There are no embedded media parts, comments, or separate substantive footnotes/endnotes in the package. The Word document is unchanged.

Source SHA-256: `C7C4DFAAD498C48F26B50C67B9BD4F639DC6812AFB3C09D6E920BEDB959215A4`.

The source says its database outline is preliminary and may be adjusted. It explicitly requires ASP.NET Core Identity, RBAC, Data Annotations, Ollama, and SpeechRecognition. MVC, .NET 10, EF Core 10, SQL Server, Razor Views and Visual Studio 2026 are subsequent user-confirmed technical decisions, not statements attributed to the Word document. No REST API, Web API project, JWT scheme, repository pattern or unrelated business functionality is introduced.

## Reading conventions

**Required** means stated in the source outside its optional section; **explicitly mandatory** means one of the four features in section 10, including the misnumbered clarification heading. **Optional candidate** means one of three choices, with at least two required overall. **Suggested** identifies the preliminary schema or example industries. **Proposed detail** identifies an analysis suggestion, never an approved business rule. **Open** identifies absent or contradictory rules.

Backend, frontend, integration, and security are overlapping responsibility classifications within the same full MVC application. They do not exclude client work from scope. Controllers and server logic enforce business/security rules; Models and ViewModels represent data and validated form input; Razor Views, HTML, CSS and JavaScript implement the required presentation and browser interaction. Device capabilities remain dependencies for biometric verification.

## Functional requirement register

| ID | Source | Requirement and backend responsibility | Classification and completion boundary |
|---|---|---|---|
| FR01 | P003–P006, P028 | Central record of requests, tenders, suppliers, bids, documents, evaluations, decisions, notifications, and reports | Required backend; central platform presentation requires integration |
| FR02 | P009–P010, P045–P046 | Requests include title, reason, priority, target date, item quantities/specifications; preserve history and review notes; approve, return, or reject | Required backend/integration; reviewer authority is open |
| FR03 | P011–P013, P048–P053 | Sourcing handles approved requests, tender titles/descriptions/terms/dates, supplier selection and invitations, incoming bids, comparison and report tracking | Required backend/integration; screens are frontend |
| FR04 | P021–P024 | Mandatory-document and essential-requirement compliance; Compliant, Non-Compliant with reasons, Pending Clarification | Required backend; final bid-status mapping is open |
| FR05 | P015–P016, P026, P055–P069 | Four evaluation members; pre-approved criteria and weights; technical and financial evaluations; one substantiated unified report | Required backend/integration; samples require external physical assessment |
| FR06 | P017, P077–P079 | Manager approves, rejects or returns report with comments; forwards when policy requires | Required backend; forwarding policy is missing |
| FR07 | P018, P081–P087 | Four-member Top Committee reviews supporting records, results, available AI analysis, history; collective final decision selects supplier or seeks details | Required backend/integration; voting rules absent |
| FR08 | P019 | Record winner, close tender, notify all parties | Required backend; visibility of award announcements and definition of parties need clarification |
| FR09 | P038–P041 | Admin configures system, accounts, roles, departments/master data and supplier verification; does not evaluate | Required backend/security; configuration and master-data scope unspecified |
| FR10 | P071–P075, P098 | Suppliers self-register with company/contact/legal information and documents; Admin verifies before activation; invitation accept/decline; own submissions/history/summary downloads | Required backend/security/integration; registration and supplier screens are frontend |
| FR11 | P039, P044, P049, P073, P083 | Role-specific dashboard data and work overviews | Required MVC dashboards with authorized real data and Razor presentation |
| FR12 | P089–P094 | Public logo/navigation, hero/CTAs, six-step explanation, about/contact details/form, real public statistics | Required public MVC pages and real statistics; contact processing details remain open; no implied contact-ticket module |
| FR13 | P096–P099 | ASP.NET Core Identity, Admin-created employee accounts, approved supplier activation, strict server-side role protection | Required backend/security/integration |
| FR14 | P101–P102 | Dynamic real-data request reports: all requests, status/department breakdowns, average approval duration | Required backend/integration; metric definitions open |
| FR15 | P103 | Tender states, participation, zero bids and evaluation delay reports | Required backend/integration; denominators and delay targets open |
| FR16 | P104 | Supplier status, legal verification, document expiry and performance reports | Required backend/integration; performance measures missing |
| FR17 | P105 | Side-by-side comparison, technical/financial summaries, unified report and excluded-offer summary | Required backend/integration; comparison screen is frontend |
| FR18 | P106–P108 | Recommendations, manager decisions, Top Committee votes, audit, awarded contracts, search/filter, PDF/Excel exports; supplier own history; Top Committee comprehensive statistics and supplier transaction frequency | Required backend/security/integration; contract-management module is not implied |
| FR19 | P111–P119 | Preserve all source request, tender, invitation, bid and decision statuses | Required backend; transitions beyond prose require confirmation |
| FR20 | P164–P170 | Account-linked face and fingerprint login, identity verification before access, both methods on login page, no static face image accepted, secure failures | Explicitly mandatory security/integration; cannot be completed by backend alone |
| FR21 | P172–P173 | Ollama extracts bid prices/specifications/delivery/missing files, compares suppliers, identifies strengths/weaknesses and preliminary recommendations; humans decide | Explicitly mandatory backend/integration; no automatic award |
| FR22 | P174–P175 | SpeechRecognition speech-to-text search on employee dashboard task list | Explicitly mandatory frontend/integration; backend authorizes and searches tasks, does not perform the specified browser recognition |
| FR23 | P177–P199 | Stage- and role-restricted clarification requests/responses; automatic recipient eligibility; subject/detail; Pending/Answered/Resolved; dates/history/record links; notifications | Explicitly mandatory backend/security/integration; exact stage mapping is open |
| FR24 | P202–P205 | Complete at least two of QR generation, digital signing, personalized watermarking | Required choice constraint; individual features are optional candidates |

## Non-functional and security requirements

| ID | Source | Requirement | Backend consequence and limitation |
|---|---|---|---|
| NFR01 | P029–P034 | Better communication/comparison, fewer manual errors/delays, transparency and past-tender tracking | Persist authoritative records and histories; no numeric performance or improvement targets supplied |
| NFR02 | P030, P106, P209 | Complete clear audit for offers, approvals, decisions and security | Identify actor, action, record, time and reasons; storage, retention and audit readers require decisions |
| NFR03 | P034, P101 | Accurate real-time/dynamic reports from actual data | Query consistent business records; no fabricated metrics; refresh latency is unspecified |
| NFR04 | P036, P071, P099, P107, P193, P209 | Server-enforced roles, record restrictions and competitor isolation | Enforce on every operation, attachment, report and history; do not rely on hidden controls |
| NFR05 | P098, P167, P170 | Verified supplier activation and secure authentication failures | Pending/rejected/suspended suppliers cannot receive active participation privileges; exact pending-account access needs agreement |
| NFR06 | P169 | Static face uploads cannot authenticate | File upload is not a biometric sign-in proof |
| NFR07 | P178–P199 | Only directly involved users communicate/view clarifications | Role alone is insufficient; check record relationship and current stage |
| NFR08 | P207 | Modern professional consistent navigation, responsive desktop/tablet/mobile UI/UX | Required responsive Razor Views, HTML, CSS and JavaScript throughout the MVC application |
| NFR09 | P208 | Client- and server-side validation using Data Annotations across forms | MVC form/ViewModel validation on the server and corresponding client validation; server checks remain authoritative |
| NFR10 | P209 | Secure file uploads | Validate accepted formats/size/content, storage and access; policy details are open |
| NFR11 | P026 | Evaluation rules fixed before tender opening, exceptional documented procedure after receipt | Preserve criterion versions and evidence; exception authority/process not provided |
| NFR12 | P173 | AI is advisory | Persist human decision separately; AI cannot authorize or decide |

No availability SLA, load target, supported browser list, localization, retention period, recovery objective, hosting model, legal jurisdiction, or integration transport is stated. These are questions, not invented requirements.

## Source ambiguities and full application obligations

- P018/P079/P081 condition Top Committee review on high-value/critical/policy-required tenders. The general example includes Top Committee review. Do not force every tender through the committee or bypass it without an agreed policy.
- The manager is both one of four evaluators and a subsequent reviewer. The source explicitly permits these roles but does not specify self-review safeguards.
- P062 combines a technical-result approval bullet with the words “Financial Tasks”; the requirement for approved technical results and financial review is retained without supplying the missing sentence ending.
- P014 ends “are reviewed to check.” No missing compliance rule has been supplied.
- “11.4” is the clarification heading inside the mandatory section; it is the fourth mandatory feature despite numbering errors. Section 11 still requires at least two optional choices.
- Public home page, dashboards, forms, responsive layout, biometric login interaction and SpeechRecognition search are required implementation work in the MVC application. Biometric hardware and browser support must be verified alongside the server implementation.
- Physical samples cannot be inspected by software alone. Backend can store human observations and references; sample logistics are not requested.
- P212 requires a 15-minute presentation (five-minute slides and ten-minute live website demonstration). P213 requires an approximately ten-minute individual technical discussion. Plan to demonstrate the full MVC website and explain its code, architecture, database and features.
- P217–P222 suggest Orange and cosmetics, pharmaceutical, food, and children's clothing companies. These are examples, not a selected domain or extra regulatory requirements.
- Both footer parts label the document “Orange Restricted.” Preserve that classification; distribution rules are not specified.

## Documentation map

| Document | Purpose |
|---|---|
| [Roles and permissions](roles-permissions.md) | Seven roles, data boundaries and operation permissions |
| [Business workflows](business-workflows.md) | Source stages/statuses, guard requirements and unresolved transitions |
| [Database design](database-design.md) | All 19 source tables and explicitly tentative field/relationship suggestions |
| [Authentication and authorization](authentication-authorization.md) | Identity lifecycle, access enforcement and security verification |
| [Mandatory features](mandatory-features.md) | Four advanced features, optional choices and client dependencies |
| [Development roadmap](development-roadmap.md) | Manageable future delivery sequence, acceptance and testing |
| [Open questions](open-questions.md) | Decisions required before implementation |

## Complete source text and paragraph classification

The following inventory preserves every nonempty paragraph's text, including headings, table cells, examples and assessment statements. Paragraph numbering includes empty paragraphs, so gaps identify empty paragraphs rather than omitted requirements. Classification applies to each entire paragraph; the registers above split compound requirements. Context/title rows are retained and are not independent acceptance criteria. Table structure in the source consists of table-name/purpose pairs; these are reconstructed in database-design.md. Source punctuation and numbering errors are retained.

| Paragraph | Classification | Original text |
|---|---|---|
| P001 | Source title or heading (context) | Sourcing & Tender Management System |
| P002 | Source title or heading (context) | 1. Project Introduction |
| P003 | Required scope or workflow context | Companies and organizations constantly need to buy hardware, equipment, materials, and services. Usually, a specific department requests items, and the purchasing/sourcing department contacts suppliers, collects price quotes, compares offers, and selects the best supplier based on price, quality, specifications, and delivery terms. |
| P004 | Required scope or workflow context | The main goal of the Sourcing & Tender Management System is to organize this entire purchasing process digitally—from start to finish. Instead of using paperwork, scattered emails, and separate Excel sheets, this system provides one central platform to store purchase requests, tenders, supplier offers, documents, evaluations, decisions, notifications, and reports. |
| P005 | Source title or heading (context) | 2. How the Sourcing Process Works |
| P006 | Required scope or workflow context | Simple Example: The IT department needs to buy 10 computers. IT creates a Purchase Request stating the quantity, specifications, reason, and needed date. After review and approval, the Sourcing department creates a Tender and invites suppliers to submit bids. Suppliers submit their prices, technical details, delivery times, and documents. The system and evaluation team review these offers. The Sourcing Manager reviews the recommendation before sending it to the Top Committee for final decision. |
| P007 | Source title or heading (context) | Step-by-Step Workflow: |
| P008 | Required scope or workflow context | 1. Identify Need: The requesting department identifies the product or service needed. |
| P009 | Required backend / integration | 2. Create Purchase Request: The department enters title, reason, priority, target date, items, quantities, and specifications. |
| P010 | Required backend / integration | 3. Request Review: The request is checked and approved, returned for changes, or rejected. |
| P011 | Required backend / integration | 4. Create Tender: The Sourcing Officer sets the tender title, description, terms, and submission dates. |
| P012 | Required backend / integration | 5. Select & Invite Suppliers: Suitable suppliers are selected and sent invitations. |
| P013 | Required backend / integration | 6. Receive Bids/Offers: Suppliers submit prices, technical details, and documents before the deadline. |
| P014 | Required backend / integration | 7. Compliance Check: Mandatory documents and essential requirements are reviewed to check. |
| P015 | Required backend / integration | 8. Technical & Financial Evaluation: A 4-person evaluation committee scores the offers based on pre-set criteria and weights. |
| P016 | Required backend / integration | 9. Unified Report Creation: The committee prepares a single combined report with scores, feedback, and recommendation, then sends it to the Sourcing Manager. |
| P017 | Required backend / integration | 10. Sourcing Manager Review: The Sourcing Manager reviews the report and approves, rejects, or requests changes with comments. |
| P018 | Required backend / integration | 11. Final Decision (Top Committee): High-value or critical tenders are forwarded to the Top Committee for a final group decision. |
| P019 | Required backend / integration | 12. Announcement & Closure: The winning supplier is recorded, the tender is closed, and all parties are notified. |
| P020 | Source title or heading (context) | 2.1 Compliance Check Details |
| P021 | Required backend / integration | The system allows officers to review whether suppliers submitted all mandatory requirements. Results are marked as: |
| P022 | Required backend / integration | Compliant: Meets all mandatory terms and document requirements. |
| P023 | Required backend / integration | Non-Compliant: Fails mandatory requirements (reasons must be clearly logged). |
| P024 | Required backend / integration | Pending Clarification: Waiting for extra documents or clarification. |
| P025 | Source title or heading (context) | 2.2 Technical & Financial Evaluation Details |
| P026 | Required backend / integration | Evaluation is based on pre-approved criteria and weights set before opening the tender. These rules cannot be changed after receiving bids unless an official documented procedure is followed. The evaluation committee reviews both technical and financial aspects and produces one unified report for the Sourcing Manager. |
| P027 | Source title or heading (context) | 3. System Objectives |
| P028 | Required backend / integration | Centralize all purchase requests and tenders in one system. |
| P029 | Required backend / integration | Improve communication between departments, sourcing officers, and suppliers. |
| P030 | Required backend / integration | Maintain a complete, clear audit log of every offer, decision, and approval. |
| P031 | Required backend / integration | Simplify price and specification comparisons across suppliers. |
| P032 | Required backend / integration | Reduce manual errors and delays caused by paper-based processes. |
| P033 | Required backend / integration | Increase transparency and enable easy tracking of past tenders. |
| P034 | Required backend / integration | Provide accurate real-time reports and analytics to help management make smart decisions. |
| P035 | Source title or heading (context) | 4. User Roles & Permissions |
| P036 | Required backend / integration / security | The system uses role-based access control (RBAC). Each user only sees pages and features permitted for their role. |
| P037 | Source title or heading (context) | 4.1 Admin |
| P038 | Required backend / integration / security | Responsible for system configuration, user accounts, master data, and department management (not involved in technical/financial evaluations). |
| P039 | Required frontend / backend integration | Dashboard: Shows counts of users, suppliers, departments, requests, and tenders. |
| P040 | Required backend / integration / security | Manage Users & Departments: Create, edit, enable/disable accounts, and set roles. |
| P041 | Required backend / integration / security | Manage Suppliers: Review supplier registration applications, legal documents, tax numbers, and licenses. Set status to Pending, Approved, Rejected, or Suspended with clear reasons. |
| P042 | Source title or heading (context) | 4.2 Requesting Department |
| P043 | Required scope or workflow context | Represents internal teams needing products/services (e.g., IT, HR). |
| P044 | Required frontend / backend integration | Dashboard: View department requests and their current status. |
| P045 | Required backend / integration | Create Purchase Request: Enter title, reason, priority, target date, items, quantities, and specs. |
| P046 | Required backend / integration | My Requests & Details: Track request history and view review notes. |
| P047 | Source title or heading (context) | 4.3 Sourcing Officer |
| P048 | Required scope or workflow context | Manages approved purchase requests and turns them into active tenders. |
| P049 | Required frontend / backend integration | Dashboard: View approved requests, active tenders, and incoming offers. |
| P050 | Required backend / integration | Manage Tenders: Create tenders, set dates/terms, select suppliers, and send invitations. |
| P051 | Required backend / integration | Review & Compare Offers: Compare bids side-by-side (price, specs, delivery time). |
| P052 | Required backend / integration | Compliance Check: Verify mandatory criteria and document compliance. |
| P053 | Required backend / integration | Track Evaluation: Ensure unified reports reach the Sourcing Manager. |
| P054 | Source title or heading (context) | 4.4 Technical & Financial Evaluation Committee |
| P055 | Required backend / integration | A 4-person committee: Sourcing Manager, Department Representative, Financial Representative, and Technical Representative. |
| P056 | Source title or heading (context) | Technical Tasks: |
| P057 | Required backend / integration | •Review technical proposals, documents, and attachments. |
| P058 | Required backend / integration | •Examine images, videos, and samples, where provided. |
| P059 | Required backend / integration | •Evaluate each proposal against established technical criteria and assign evaluation scores. |
| P060 | Required backend / integration | •Document technical observations and identify any instances of non-compliance. |
| P061 | Required backend / integration | •Request technical clarifications when necessary. |
| P062 | Required backend / integration | •Document and approve the technical evaluation results, and prepare a summary for inclusion Financial Tasks: |
| P063 | Required backend / integration | •Review submitted price quotes, including the details of pricing and quantities. |
| P064 | Required backend / integration | •Examine taxes, fees, and any additional costs. |
| P065 | Required backend / integration | •Review costs related to transportation, installation, and maintenance, where applicable. |
| P066 | Required backend / integration | •Examine payment terms, currency, and the validity period of the offer. |
| P067 | Required backend / integration | •Compare the total cost of the offers and document financial observations. |
| P068 | Required backend / integration | •Record the outcome of the financial evaluation and include it in the consolidated report. |
| P069 | Required backend / integration | The consolidated report must include technical and financial evaluation scores, observations, instances of non-compliance, a comparison between suppliers, the final result, and a substantiated recommendation.. |
| P070 | Source title or heading (context) | 4.5 Supplier |
| P071 | Required backend / integration / security | External vendor participating in tenders. Suppliers CANNOT see competing suppliers' offers. |
| P072 | Required backend / integration | Registration: Create an account and submit company information and official documents. |
| P073 | Required frontend / backend integration | Dashboard & Invitations: View invitations, accept or decline participation. |
| P074 | Required backend / integration | Submit Offer: Enter pricing, delivery timeframe, technical details, and upload documents. |
| P075 | Required backend / integration | My Offers: Track submitted bids and downloadable personal summary reports. |
| P076 | Source title or heading (context) | 4.6 Sourcing Manager |
| P077 | Required scope or workflow context | Reviews unified evaluation reports and recommendations from the committee. |
| P078 | Required backend / integration | Manager Decision: Approve recommendation, reject it, or send it back for review with comments. |
| P079 | Required backend / integration | Forwarding: Pass high-level tenders to the Top Committee when required by policy. |
| P080 | Source title or heading (context) | 4.7 Top Committee |
| P081 | Required scope or workflow context | The final authority for high-value tenders requiring collective decision-making. |
| P082 | Required backend / integration | 4-member committee making documented group decisions. |
| P083 | Required frontend / backend integration | Dashboard: Provides an overview of bids awaiting final action. |
| P084 | Required backend / integration |  Review of Results: Access to purchase requisitions, submitted bids, consolidated reports, and the Sourcing Manager’s recommendations. |
| P085 | Required backend / integration | •Monitoring of technical and financial evaluation outcomes, including AI-driven analysis (where available). |
| P086 | Required backend / integration | • Final Decision: Selection of a supplier, or a request for further details—with all notes and justifications recorded. |
| P087 | Required backend / integration | • Bid History: Access to past tenders and their corresponding decisions. |
| P088 | Source title or heading (context) | 5. Public Home Page |
| P089 | Required scope or workflow context | The public home page is accessible before logging in and must include: |
| P090 | Required frontend; integration where applicable | Navbar: System Logo, Name, Home, About, How It Works, Contact, Login, Supplier Registration. |
| P091 | Required frontend; integration where applicable | Hero Section: Headline ('Unified Platform for Sourcing & Tender Management'), brief description, and CTA buttons. |
| P092 | Required frontend; integration where applicable | How It Works: Visual summary of the 6 core steps. |
| P093 | Required backend / frontend integration | Public Statistics: Real database metrics (e.g., active tenders, approved suppliers, completed requests). |
| P094 | Required frontend; integration where applicable | About & Contact: Mission statement and contact form/details. |
| P095 | Source title or heading (context) | 6. Registration & Authentication |
| P096 | Required backend / integration / security | Authentication and authorization are built using ASP.NET Core Identity. |
| P097 | Required backend / integration / security | Employee Login: Done via credentials created by Admin. |
| P098 | Required backend / integration / security | Supplier Registration: Self-registration form collecting company details, tax/registration numbers, contact info, and official document uploads. Account requires Admin verification before activation. |
| P099 | Required backend / integration / security | Role & Page Protection: Enforced strictly server-side. |
| P100 | Source title or heading (context) | 7. Reports & Analytics |
| P101 | Required scope or workflow context | Reports must be based on real system data and update dynamically. Key report categories include: |
| P102 | Required backend / integration | Purchase Request Reports: All requests, status breakdown (Draft, Submitted, Under Review, Approved, Rejected, Returned), department breakdown, and average approval duration. |
| P103 | Required backend / integration | Tender Reports: Open/closed/awarded tenders, supplier participation rate, tenders with zero bids, evaluation delay reports. |
| P104 | Required backend / integration | Supplier Reports: Registered/approved/suspended suppliers, legal verification status, document expiration tracking, supplier performance metrics. |
| P105 | Required backend / integration | Bids & Evaluation Reports: Side-by-side bid comparison, technical evaluation summary, financial analysis summary, unified report, excluded offers summary. |
| P106 | Required backend / integration | Approvals & Decision Reports: Committee recommendations, Sourcing Manager decisions, Top Committee voting records, full audit trail, final awarded contracts. |
| P107 | Required backend / integration / security | Special Features: Search/filter options, PDF/Excel export capabilities, role-specific data access (e.g., Suppliers can download their bid history. |
| P108 | Required backend / integration |  The top committee can download a report displaying comprehensive statistics for the site, such as the number of suppliers and the frequency of transactions with each supplier.). |
| P109 | Source title or heading (context) | 8. System Statuses (State Machine) |
| P110 | Source title or heading (context) | Purchase Request Statuses |
| P111 | Required backend / integration | Draft, Submitted, Under Review, Approved, Rejected, Returned, Converted to Tender |
| P112 | Source title or heading (context) | Tender Statuses |
| P113 | Required backend / integration | Draft, Published, Open, Closed, Under Evaluation, Awaiting Manager Review, Awaiting Committee Decision, Awarded, Cancelled |
| P114 | Source title or heading (context) | Invitation Statuses |
| P115 | Required backend / integration | Pending, Accepted, Declined, Expired |
| P116 | Source title or heading (context) | Bid Statuses |
| P117 | Required backend / integration | Draft, Submitted, Under Review, Compliant, Non-Compliant, Accepted, Rejected, Withdrawn |
| P118 | Source title or heading (context) | Recommendation & Decision Statuses |
| P119 | Required backend / integration | Recommendation Pending, Manager Approved, Manager Rejected, Returned for Review, Committee Approved, Committee Rejected |
| P120 | Source title or heading (context) | 9. Database Architecture Overview |
| P121 | Required scope or workflow context | Preliminary outline of database entities (trainees may adjust details as needed): |
| P122 | Source title or heading (context) | Table Name |
| P123 | Source title or heading (context) | Description / Purpose |
| P124 | Suggested original database outline; backend | AspNetUsers |
| P125 | Suggested original database outline; backend | Stores user login accounts managed by ASP.NET Core Identity. |
| P126 | Suggested original database outline; backend | AspNetRoles |
| P127 | Suggested original database outline; backend | Stores user roles (Admin, Supplier, SourcingOfficer, etc.). |
| P128 | Suggested original database outline; backend | Departments |
| P129 | Suggested original database outline; backend | Stores company departments (IT, HR, Finance, etc.). |
| P130 | Suggested original database outline; backend | SupplierProfiles |
| P131 | Suggested original database outline; backend | Stores company details, tax info, legal verification status, and linked UserID. |
| P132 | Suggested original database outline; backend | PurchaseRequisitions |
| P133 | Suggested original database outline; backend | Stores purchase requests created by departments. |
| P134 | Suggested original database outline; backend | PurchaseRequisitionItems |
| P135 | Suggested original database outline; backend | Stores line items, quantities, and specs for each purchase request. |
| P136 | Suggested original database outline; backend | Tenders |
| P137 | Suggested original database outline; backend | Stores tender data created from approved requisitions. |
| P138 | Suggested original database outline; backend | TenderSuppliers |
| P139 | Suggested original database outline; backend | Links tenders to invited/participating suppliers. |
| P140 | Suggested original database outline; backend | Invitations |
| P141 | Suggested original database outline; backend | Stores invitation details and supplier responses. |
| P142 | Suggested original database outline; backend | Bids |
| P143 | Suggested original database outline; backend | Stores offers submitted by suppliers. |
| P144 | Suggested original database outline; backend | BidItems |
| P145 | Suggested original database outline; backend | Stores itemized pricing and specifications for each bid. |
| P146 | Suggested original database outline; backend | BidDocuments |
| P147 | Suggested original database outline; backend | Stores uploaded bid attachments and legal docs. |
| P148 | Suggested original database outline; backend | EvaluationCriteria |
| P149 | Suggested original database outline; backend | Stores criteria list and percentage weights for each tender. |
| P150 | Suggested original database outline; backend | TechnicalEvaluations |
| P151 | Suggested original database outline; backend | Stores technical evaluation scores and notes. |
| P152 | Suggested original database outline; backend | FinancialEvaluations |
| P153 | Suggested original database outline; backend | Stores financial evaluation scores and notes. |
| P154 | Suggested original database outline; backend | EvaluationReports |
| P155 | Suggested original database outline; backend | Stores the unified evaluation report and committee recommendation. |
| P156 | Suggested original database outline; backend | TenderApprovals |
| P157 | Suggested original database outline; backend | Stores approval/rejection decisions by Manager and Top Committee. |
| P158 | Suggested original database outline; backend | Notifications |
| P159 | Suggested original database outline; backend | Stores user notifications and alerts. |
| P160 | Suggested original database outline; backend | AuditLogs |
| P161 | Suggested original database outline; backend | Stores detailed system logs for security and auditing purposes. |
| P162 | Source title or heading (context) | 10. Mandatory Requirements |
| P163 | Required scope or workflow context | All four of the following requirements MUST be implemented in the project: |
| P164 | Source title or heading (context) | 10.1 Biometric Authentication (Face Recognition & Fingerprint Verification) |
| P165 | Explicitly mandatory biometric; security / backend / frontend integration | Implement biometric authentication to allow users to securely log in using Face Recognition or Fingerprint Verification. |
| P166 | Explicitly mandatory biometric; security / backend / frontend integration | Biometric authentication must be linked to the user's registered account. |
| P167 | Explicitly mandatory biometric; security / backend / frontend integration | The system must verify the user's identity before allowing access. |
| P168 | Explicitly mandatory biometric; security / backend / frontend integration | Both Face Recognition and Fingerprint Verification must be supported on the login page. |
| P169 | Explicitly mandatory biometric; security / backend / frontend integration | Static image uploads must NOT be accepted as valid face recognition. |
| P170 | Explicitly mandatory biometric; security / backend / frontend integration | The system must handle authentication failures securely and prevent unauthorized access. |
| P171 | Source punctuation only | . |
| P172 | Source title or heading (context) | 10.2 AI Bid Analysis using Ollama |
| P173 | Explicitly mandatory Ollama; backend / integration | Integrate Ollama to analyze supplier bids and extract critical data (prices, specs, delivery time, missing files). Compare bids, highlight strengths/weaknesses, and generate initial AI recommendations. Note: AI output is an assistant tool and does not replace human committee decisions. |
| P174 | Source title or heading (context) | 10.3 Voice Search for Tasks |
| P175 | Explicitly mandatory SpeechRecognition; frontend / backend integration | Enable speech-to-text voice search on the Employee Dashboard task list using SpeechRecognition. |
| P177 | Source title or heading (context) | 11.4 Clarification and Inquiry Management System (Role-Based Clarification System) |
| P178 | Explicitly mandatory clarification; backend / integration / security | The system must provide an internal clarification system that allows users to send questions and request additional information based on their roles and the current stage of the process. Communication must be limited to users who are directly involved in the relevant process. |
| P179 | Source title or heading (context) | First: Allowed Communication Between Roles |
| P180 | Explicitly mandatory clarification; backend / integration / security | Requesting Department ↔ Sourcing Officer: To ask questions and clarify purchase requests, specifications, quantities, and required changes. |
| P181 | Explicitly mandatory clarification; backend / integration / security | Sourcing Officer ↔ Technical & Financial Evaluation Committee: To clarify submitted offers, documents, and evaluation results. |
| P182 | Explicitly mandatory clarification; backend / integration / security | Technical & Financial Evaluation Committee ↔ Sourcing Manager: To discuss evaluation results, the combined evaluation report, and recommendations. |
| P183 | Explicitly mandatory clarification; backend / integration / security | Sourcing Manager ↔ Top Committee: To clarify reports, recommendations, and final decisions. |
| P184 | Explicitly mandatory clarification; backend / integration / security | Sourcing Officer ↔ Supplier: To request missing documents or clarification about submitted offers and receive responses. |
| P185 | Source title or heading (context) | Second: System Functions |
| P186 | Explicitly mandatory clarification; backend / integration / security | The system must provide the following functions: |
| P187 | Explicitly mandatory clarification; backend / integration / security | Automatically identify the users who are allowed to communicate based on their roles and the current process stage. |
| P188 | Explicitly mandatory clarification; backend / integration / security | Allow users to send clarification requests with a subject and detailed description. |
| P189 | Explicitly mandatory clarification; backend / integration / security | Allow users to receive clarification requests and submit responses. |
| P190 | Explicitly mandatory clarification; backend / integration / security | Track the status of each clarification request: Pending, Answered, or Resolved. |
| P191 | Explicitly mandatory clarification; backend / integration / security | Save all questions, responses, and their dates, and link them to the related Purchase Request, Tender, or Supplier Offer. |
| P192 | Explicitly mandatory clarification; backend / integration / security | Send notifications when a new clarification request or response is received. |
| P193 | Explicitly mandatory clarification; backend / integration / security | Apply role-based permissions to ensure that clarification requests and responses are only visible to authorized users. |
| P194 | Source title or heading (context) | Third: Workflow |
| P195 | Explicitly mandatory clarification; backend / integration / security | The system identifies the users who are allowed to communicate based on the current process stage and their roles. |
| P196 | Explicitly mandatory clarification; backend / integration / security | The user sends a clarification request to the responsible party. |
| P197 | Explicitly mandatory clarification; backend / integration / security | The receiving user receives the request and submits a response. |
| P198 | Explicitly mandatory clarification; backend / integration / security | The original sender receives the response, and the request status is updated. |
| P199 | Explicitly mandatory clarification; backend / integration / security | All clarification requests and responses are saved in the related transaction history. |
| P201 | Source title or heading (context) | 11. Optional Requirements |
| P202 | Required minimum of two optional choices | Each team must complete AT LEAST TWO optional requirements from this list: |
| P203 | Optional candidate; backend / frontend integration | QR Code Generation: Generate a QR code for each tender or purchase request to access its details quickly. |
| P204 | Optional candidate; backend / frontend integration | Digital Signature Integration – Enable electronic signing of documents and official decisions. |
| P205 | Optional candidate; backend / frontend integration | Custom Watermark System: Implement a system that adds a customized watermark to tender documents and reports when they are viewed or downloaded. The watermark should include the information of the user who accessed or downloaded the document. |
| P206 | Source title or heading (context) | 12. General Quality & Architecture |
| P207 | Required frontend; integration where applicable | Creative UI/UX: Modern, professional, consistent visual layout, clear navigation, and responsive design (desktop, tablet, mobile). |
| P208 | Required frontend / backend validation | Data Validation: Client-side and server-side validation using Data Annotations across all forms. |
| P209 | Required backend / integration / security | Security & Isolation: Role-based authorization, strict data isolation between competing suppliers, secure file upload handling, and audit logging. |
| P210 | Source title or heading (context) | 13. Assessment & Presentation |
| P211 | Source title or heading (context) | Projects will be presented and evaluated as follows: |
| P212 | Required full-project assessment obligation | Presentation (15 Mins Total): 5 minutes slide presentation + 10 minutes live website demonstration. |
| P213 | Required full-project assessment obligation | Individual Technical Evaluation (~10 Mins per student): One-on-one technical discussion to verify individual understanding of code, architecture, database design, and implemented features. |
| P217 | Source title or heading (context) | 16. Suggested Project Idea : |
| P218 | Suggested project domain; not a selected requirement | Orange. |
| P219 | Suggested project domain; not a selected requirement | Cosmetics Manufacturing Company. |
| P220 | Suggested project domain; not a selected requirement | Pharmaceutical Company. |
| P221 | Suggested project domain; not a selected requirement | Food Manufacturing Company. |
| P222 | Suggested project domain; not a selected requirement | Children’s Clothing Company. |

Empty main-document paragraphs: P176, P200, P214, P215, P216, P223. Both footer parts contain three paragraphs: `Orange RestrictedOrange Restricted`, `Orange Restricted`, `Orange Restricted`. The footnotes and endnotes parts each contain two empty separator paragraphs and no substantive requirements.
