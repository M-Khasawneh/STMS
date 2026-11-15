# V5 implementation status — 2026-11-15

This is a code-grounded current delivery checklist, not a replacement for the original Word document or an assertion that all production SQL/browser/device acceptance tests passed. Older planning documents describe their historical phase and should not be read as the current delivered state.

## Implemented modules

- Identity credentials, role enforcement, employees/departments, account status and profile settings: Account/Employees/Departments/Profile controllers and their existing services.
- Supplier registration, private official documents and Admin verification: SuppliersController, SupplierRegistration/SupplierReview.
- Purchase requisition draft/submission/review/rework: RequisitionsController and RequisitionManagement.
- Tender conversion, configured/approved criteria, publication, explicit opening/closure, invitations and responses: TendersController/TenderManagement.
- Supplier drafts, explicit bid submission, private bid documents, own summary and scoped internal viewing: BidsController/ReviewBidsController and bid services.
- Compliance and supplier clarification, four-person committee assignments, confirmed evaluations, versioned unified reports and manager decisions: Compliance/Committees/Evaluations/UnifiedReports controllers.
- Top Committee rounds, final decisions, report rework and explicit award: TopCommitteeController/TopCommitteeManagement. Actual SQL write/concurrency tests remain separately pending; memory service/MVC and SELECT-only queries passed.
- Internal role/stage/involvement-scoped inquiries and notifications: ClarificationsController/InternalClarificationManagement and NotificationsController.
- Public Home/About/Contact, real public aggregates, contact inbox and administrative messages: HomeController, PublicContact, MessagesController.
- Reports and Excel/PDF export, role-scoped data and Top Committee supplier award frequency: ReportsController/Reporting/ReportExports. See docs/reporting.md for limitations; actual award frequency is not a fulfillment/performance score.

## Newly implemented mandatory feature

Employee dashboard voice search (V5 10.3): real browser SpeechRecognition integration and typed fallback over currently rendered authorized recent records. See docs/dashboard-voice-search.md for scope and verification. Actual microphone recognition is not yet manually demonstrated, and this is not a global historical task search.

## Remaining mandatory advanced features

1. **Ollama bid analysis (10.2): not implemented.** No Ollama service/client, analysis action, persisted output or advisory review UI exists. It must extract/compare permitted bid facts, identify strengths/weaknesses/missing evidence and display preliminary advice without making human decisions. Deployment/model/license, allowed document extraction and analysis retention need to be settled before external integration or schema changes. Existing bid/compliance/human evaluation modules must be reused.
2. **Face and fingerprint login (10.1): not implemented.** Current Account/Identity login is credential-based. Must verify actual account-linked biometric proofs and reject static-photo login. Device/OS/browser support and whether an authenticator that also permits PIN satisfies the explicit modalities need a concrete acceptance decision. Do not add cosmetic buttons, raw biometric storage or pretend passkeys prove a specific modality.
3. **Actual voice microphone demonstration:** the production browser integration now exists; English/Arabic device/service verification remains. Browser tests use injected recognition events only.

## Two required optional choices are still outstanding

No QR generation, user-specific document/report watermarking or digital-signature integration was found in application code. V5 requires at least two of these three choices. QR plus watermarking remains a proposed pair, not an approved library/signing/privacy policy. Watermarking must cover viewing and downloading of supported tender documents and reports, preserve originals and keep private access restrictions. Digital signatures require defined signer/proof/verification expectations if selected.

## Partial requirements needing precise definitions

- **Supplier-document expiry:** SupplierDocument.ExpiresOn and expiry/unknown-date reporting exist, but there is no date-capture/edit workflow in supplier review. Do not fabricate expiry dates or silently suspend companies; agree how true dates are entered and verified.
- **Supplier performance:** existing transaction frequency counts recorded awards. Fulfillment/performance scoring has no agreed source data or formula; it must not be invented from bids or approval status.
- **Final contract reporting:** TenderAward includes an optional ContractReference; this is not contract creation/signing or fulfillment management. V5's final-contract reporting scope needs definition before adding a contract workflow.
- **Cancelled/Accepted/Rejected/Withdrawn enum values:** enum presence alone is not an implemented action. Current approved bid rules explicitly prohibit withdrawal/replacement/resubmission; do not add such actions merely because the Word status list mentions them. Any later cancellation or award-to-bid-status rule needs an approved transition policy.
- **Final acceptance:** real biometric hardware, Ollama behavior, supported speech browsers, remaining SQL write/concurrency tests and presentation/demo material still require evidence. No completed module should be rebuilt just to satisfy this checklist.

## Suggested order

Finish manual voice verification; implement Ollama advisory analysis once its concrete integration choices are approved; resolve and implement biometric authentication with suitable hardware; select and implement two optional features; complete defined report/expiry gaps and final acceptance. This checklist does not authorize migrations, dependency installation, data seeding or unrelated modules.
