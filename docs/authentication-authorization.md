# Authentication and Authorization Analysis

## Source requirements and confirmed MVC architecture

Source P036/P096/P099 requires ASP.NET Core Identity and strict server-side role protection. P097 assigns employee account creation to Admin. P098 requires supplier self-registration and Admin verification before activation. P164–P170 requires account-linked biometric authentication. P071/P107/P178–P199/P209 establishes supplier isolation, role-specific reports, secure uploads and involved-user clarification access.

The confirmed application uses ASP.NET Core MVC targeting .NET 10 (`net10.0`), ASP.NET Core Identity with EF Core 10 and SQL Server, and Razor Views with HTML, CSS and JavaScript, developed in Visual Studio 2026. All required authentication pages and dashboards are in scope. Authentication will use Identity's MVC-compatible cookie sign-in facilities; credential verification remains within Identity. Session policy, recovery rules and the biometric integration mechanism require detailed decisions. No ASP.NET Core Web API or API authentication endpoints are planned.

Use MVC controllers and Razor Views for the required account interactions, with Models/ViewModels for validated form input. Identity remains the authentication service; its default Razor Pages UI is not a requirement to replace the confirmed MVC account screens. State-changing MVC forms require antiforgery protection, and protected controller actions must enforce account, role, ownership and stage checks independently of rendered navigation.

## Account lifecycle

| Stage | Required behavior | Unspecified decision |
|---|---|---|
| Employee provisioning | Admin creates credentials, sets roles, enables/disables accounts and manages departments | Initial credential delivery, password change, reset/recovery, email confirmation, department reassignment |
| Employee sign-in | Authenticate registered account using Identity and apply server-side permissions | Password policy, session lifetime, lockout thresholds, concurrent sessions, transport |
| Supplier registration | Collect company details, tax/registration numbers, contact information and official uploads | Required fields/documents/expiry, duplicate company rules, contacts per company and validation limits |
| Pending verification | Admin reviews legal documents, tax numbers and licences; supplier not activated before verification | Whether pending applicants may sign in only to view/correct application; how corrections are submitted |
| Approved supplier | Activate participation subject to role and ownership checks | Exact activation behavior and handling future expiry |
| Rejected/Suspended supplier | Record status and clear reasons | Reapplication, appeal, reinstatement, pending invitation and submitted-bid handling |
| Account disabled or role changed | Admin account controls and server restrictions remain effective | Existing-session revocation mechanism and timing, historical author access |
| Biometric enrollment/sign-in | Credential linked to registered account; verify identity before granting access; secure failure handling | Enrollment identity proof, supported authenticators, recovery, hardware and proof of both biometric modalities |

Registration status and user-account enablement are related but distinct facts. Identity authentication alone does not establish supplier approval. A successful sign-in must not activate an unverified supplier or grant procurement participation automatically.

## Authorization responsibilities

Use the [role matrix](roles-permissions.md) as the analysis baseline. Apply Identity roles and appropriate authorization checks to MVC controller actions and underlying record operations. They must enforce:

- Account eligibility and assigned role, plus record ownership/involvement and current process stage.
- Department boundaries for request tracking and involved officer/committee scope where applicable.
- Supplier ownership of invitations, bids, attachments, summaries, histories and clarification threads.
- Four-person evaluation and Top Committee participation according to actual membership, once assignment rules are agreed.
- Admin exclusion from technical/financial evaluation.
- Locked criteria and authorized workflow actions; a role is not blanket permission to set arbitrary status values.
- Read restrictions across ordinary access, search, reports, exported files, notification contents and media downloads.

Backend authorization must run independently of buttons, pages or client validation. Supplier/company, department, reviewer, approval outcome and trusted actor identity must be resolved or verified server-side; client-submitted identity fields cannot confer permission.

An allowlist of role pairs is required for clarification, but membership in an allowed pair is not enough: both users must be directly involved with that request/tender/offer and authorized at its current stage. No direct supplier-to-evaluator channel is stated.

## Security implementation questions and recommendations

The following technical measures support explicit security requirements within the confirmed MVC stack; they do not add business features:

| Risk | Planned MVC security control | Acceptance evidence |
|---|---|---|
| Credential compromise | Use Identity's supported credential verification; protect transport; agree password, lockout, reset and recovery policies | Invalid and locked/disabled credentials fail securely without disclosing unnecessary account detail |
| Privilege escalation | Restrict role/account changes to Admin; independently validate record scope | Supplier cannot self-approve or assign employee roles |
| Stale access | Define session/security-state revalidation on disable, suspension, role removal and membership removal | A previously authenticated user cannot continue a newly prohibited operation |
| Cross-supplier leakage | Apply ownership to queries, downloads, reports, file metadata, comparisons and notifications | Supplier A cannot discover or retrieve Supplier B's offers through alternate access paths |
| Untrusted input | Server validation using Data Annotations as required; separate cross-record/business checks | Invalid quantity/state/relationships do not change records; meaningful safe validation feedback |
| Upload abuse | Agree extension/type/content allowlists, size/count limits, quarantine/scanning approach and storage isolation | Executable, disguised, excessive and unauthorized content is rejected or quarantined |
| Sensitive download exposure | Authorize at access time; use safe filenames and disposition; avoid public storage references | Guessing a filename/storage identifier does not grant access |
| Concurrent business actions | Choose a consistency/concurrency strategy for decisions, deadlines and submissions | Duplicate/competing actions do not produce incompatible decisions or duplicate awards |
| Logging disclosure | Separate security/audit facts from diagnostic detail; exclude credentials/raw biometrics | Audit remains useful without secrets or unrestricted offer content |
| Client-dependent attacks | Antiforgery validation on state-changing cookie-authenticated MVC forms; safe output encoding and appropriate session controls | Cross-site form submission fails; untrusted input is not rendered as executable markup; replay/session policies are tested |

ASP.NET Core upload guidance supports server-controlled file handling and validation in the MVC application: [Microsoft file upload guidance](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-10.0). Required upload forms and protected download/view actions are in scope; files must not become public merely because a view references them.

## Biometric security boundary

The server must verify an account-bound authentication proof; an uploaded face image or fingerprint photograph is insufficient. Razor login views and browser JavaScript must capture or invoke the approved biometric operation with suitable hardware. Both face and fingerprint support must be verified as required by the source. Raw biometric storage is not mandated.

Passkeys/WebAuthn are a candidate, not a selected substitute for the requirement. WebAuthn keeps biometric verification local and proves a credential operation to the relying party; user verification may also use a PIN. Therefore a generic successful passkey sign-in does not establish that both face and fingerprint were used. Confirm modality and presentation acceptance before selecting it. See [W3C WebAuthn user verification](https://www.w3.org/TR/webauthn-3/) and [mandatory-features.md](mandatory-features.md).

## Security testing plan

| Area | Planned cases | Required result or unresolved decision |
|---|---|---|
| Authentication | Valid/invalid credentials, disabled user, expired/replayed authentication proof, failed biometric verification | No access before successful verification; secure failures and account eligibility |
| Supplier lifecycle | Pending/rejected/suspended/approved application, duplicate registration, changed legal documents | No activation before Admin verification; duplicates/reinstatement rules need decisions |
| Authorization | Every role × relevant operation; role combination; unrelated department/officer; unrelated committee; removed membership | No ungranted action or unrelated record access; combined-role rules must be settled |
| Supplier isolation | Two competing suppliers; guessed IDs; list/search; attachment preview; personal report/export; clarification | No competing-offer exposure through any access surface |
| Workflow | Invalid transition, deadline boundary, simultaneous submissions/decisions, outdated report | Enforce approved rules without lost decisions; unspecified outcomes first become questions |
| Data validation | Missing required values, excessive text, invalid quantities/prices/dates, mismatched bid/criterion/tender | Server rejection before durable business changes |
| File security | Disguised content, unsafe name/path, oversize/too many files, unauthorized download, interrupted upload | Safe handling, no unintended execution/path access, no ownership bypass |
| Auditing | Offer/approval/decision success and failure; account/role/supplier status changes | Source-required events attributable and traceable; detailed event selection/retention agreed |
| Error handling | Unexpected processing/storage/AI failure | Safe user-facing result; no stack traces, credentials or other supplier data exposed |

Tests are planned only. The application framework and persistence stack are confirmed; no test library, packages or code have been installed or created. Verification will cover actual MVC pages, controller actions, forms, cookies, protected file access and SQL Server persistence. Test both client validation and independent server rejection of invalid input.
