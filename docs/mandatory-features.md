# Mandatory and Optional Feature Analysis

## Interpretation

Source P163 states that all four mandatory features must be implemented. These are biometric login, Ollama bid analysis, SpeechRecognition voice search and role-based clarification. The clarification heading is misnumbered “11.4” but belongs to this mandatory set. P202 separately requires at least two optional choices. An implementation roadmap may stage these features, but it cannot treat them as dispensable.

The confirmed full application is ASP.NET Core MVC on .NET 10 (`net10.0`), EF Core 10, SQL Server and ASP.NET Core Identity, with Razor Views, HTML, CSS and JavaScript in Visual Studio 2026. All required pages, dashboards and browser interactions are in scope; there is no Web API. Feature-specific libraries, biometric devices and Ollama model/deployment remain to be selected. External standards explain feasibility; the Word document remains the authority for business scope. No implementation has started.

## Face Recognition and Fingerprint Verification

**Exact obligation:** provide secure login using Face Recognition or Fingerprint Verification, tied to the registered account; verify before allowing access; support both on the login page; reject static-image uploads as face recognition; handle failure securely and prevent unauthorized access (P164–P170).

| Area | Backend responsibility | Frontend/device dependency |
|---|---|---|
| Enrollment | Bind verified authentication credential to the correct Identity account; authorize enrollment | Invoke biometric-capable hardware or trusted authenticator; obtain user interaction |
| Face login | Validate fresh account-bound proof and eligibility before creating authenticated state | Real biometric capture/verification; static uploaded photo cannot qualify; hardware/liveness capability must be established |
| Fingerprint login | Verify account-bound credential/proof, failures and replay controls | Fingerprint-capable sensor, operating-system/authenticator support; normal web pages cannot simply read arbitrary sensor data |
| Failures/recovery | Agree replay, retry, lockout, revoked-credential and recovery behavior; audit safely | Clear failure/fallback interaction without claiming successful biometrics |
| Demonstration | Confirm both modalities and account association work in the selected environment | Suitable face and fingerprint hardware plus login presentation; backend tests alone cannot demonstrate this |

### WebAuthn and passkey feasibility

WebAuthn is a candidate for public-key authentication linked to Identity. The relying party can verify challenge, origin, relying-party scope, signature and user-verification evidence while the authenticator handles local biometrics. Biometric data is not sent to the relying party. However, user verification can use a PIN; ordinary verification evidence does not prove face rather than fingerprint. Thus passkeys cannot silently replace the explicit two-modality requirement. These conclusions follow from [W3C WebAuthn Level 3](https://www.w3.org/TR/webauthn-3/).

The confirmed .NET 10 baseline provides Identity passkey facilities to assess for MVC integration. Microsoft's documentation illustrates a Blazor integration, but the project remains MVC: [Microsoft passkey guidance](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/passkeys/blazor?view=aspnetcore-10.0). Any selected passkey ceremony must be integrated through MVC account actions and Razor/JavaScript interaction, and must meet the source's face and fingerprint acceptance requirements. No passkey mechanism is selected merely by choosing .NET 10.

Open decisions: whether device-backed biometrics meet assessment criteria; whether application-level face matching is expected; liveness requirements; approved hardware/OS/browser; permitted fallback; enrollment identity proof; biometric privacy/retention if direct capture is chosen. Do not store raw biometrics or introduce a face-recognition vendor by assumption. If direct face matching is required, its anti-spoofing evidence and data handling must be designed explicitly rather than accepting an image upload.

## AI Bid Analysis using Ollama

**Exact obligation:** analyze supplier bids; extract prices, specifications, delivery time and missing files; compare bids; highlight strengths and weaknesses; generate initial recommendations. AI assists human committee decisions and never replaces them (P173). Top Committee may view AI analysis where available (P085).

### Backend analysis flow

1. Select only bids and documents the internal actor may access for the relevant tender and stage.
2. Read structured bid/item data and allowed attachments. Structured prices remain authoritative business facts. File text extraction is required where important facts appear only in documents; supported document/media formats and OCR needs are unspecified.
3. Compare received documents against the tender's actual mandatory-document list. AI can describe missing evidence, but the compliance determination belongs to the authorized human process; an absent required-file list cannot be invented by the model.
4. Supply bounded bid content and evaluation context to Ollama. Untrusted bid text must be treated as evidence, not instructions to alter permissions, scoring or decisions.
5. Validate the output structure and evidence references. Distinguish extracted facts, unsupported/uncertain statements, comparisons, strengths/weaknesses and preliminary recommendations.
6. Present the advisory result to authorized humans. Preserve their evaluation/recommendation/decision separately. AI must not write approvals, eligibility outcomes or awards.

These steps are a candidate technical decomposition of the required feature, not new procurement stages. A versioned analysis record with model identity, source/input references, time and output is suggested for explainability. Storage shape and review procedure require approval.

Ollama supports schema-constrained structured output, which is a possible way to validate a stable result shape. It does not guarantee factual accuracy: [Ollama structured outputs](https://ollama.com/blog/structured-outputs).

### Limitations and tests

Agree model, license, hardware capacity, hosting/access restrictions, supported attachments, extraction fidelity, maximum bid size and acceptable latency. Limit model access to necessary records; restrict connectivity according to the selected deployment. Do not let a supplier request cross-supplier analysis. Define failure behavior so unavailable/malformed AI results cannot become an approval or fabricate a recommendation.

Planned tests: accurate extraction against known bid values; taxes/currency/quantity ambiguity; unreadable or missing files; malicious instruction inside a bid; exaggerated AI claims; omitted suppliers; output schema failure; timeout; unavailable Ollama; human override and traceability. Numerical scoring/award rules remain human-approved business logic, not model improvisation.

## Voice Search using SpeechRecognition

**Exact obligation:** speech-to-text voice search using SpeechRecognition on the Employee Dashboard task list (P174–P175).

Razor Views and browser JavaScript must implement microphone permission, supported browser/device detection, SpeechRecognition invocation, recognition lifecycle, transcript display and employee-dashboard interaction. These client-side components are required work within the full MVC application. Do not replace SpeechRecognition with a server transcription service without approval.

Backend responsibilities are authorized task lookup and filtering using the recognized text as ordinary untrusted search input. Results must stay within the employee's role, department/involvement, assigned committee and process stage. The source does not define a standalone Task table, task assignment lifecycle or search grammar; infer no separate task-management product.

Open questions: what counts as a task; fields searched; languages; status/date filters; sort/paging behavior; recognition/browser support; failure behavior and speech-processing privacy. Source does not prescribe audio upload or audio retention, so neither is added. Planned MVC/browser tests cover cross-role leakage, empty/long/malformed transcripts, no results, unavailable microphone/recognition and unsupported browser. Server task search alone does not complete voice-search acceptance.

## Role Based Clarification and Inquiry Management

**Exact obligation:** restrict communication to directly involved users according to role and current stage; automatically identify eligible recipients; support subject/detailed question and responses; Pending/Answered/Resolved; retain dates/questions/responses and link request/tender/offer; notify on new requests/responses; keep authorized transaction history (P178–P199).

| Allowed role pair, both directions | Purpose |
|---|---|
| Requesting Department ↔ Sourcing Officer | Request specifications, quantities and changes |
| Sourcing Officer ↔ Evaluation Committee | Submitted offers, documents and evaluation results |
| Evaluation Committee ↔ Sourcing Manager | Results, unified report and recommendation |
| Sourcing Manager ↔ Top Committee | Reports, recommendation and final decision |
| Sourcing Officer ↔ Supplier | Missing documents and submitted-offer clarifications |

MVC server logic must determine recipient eligibility rather than trust a selected recipient's role label; validate record context; save question/response author/time; manage state; trigger notifications; restrict searches/history/files to involved users. Required Razor Views present eligible recipients, validated question/response forms, conversation history, status and notifications. No general chat, arbitrary direct messaging or email integration is requested.

Pending → Answered is supported by response receipt. Who resolves, whether a thread can reopen, response multiplicity, group versus named recipients, exact stage-to-pair rules and post-deadline document cures remain open. Do not hardcode invented communication windows. If a technical evaluator needs supplier information, the source permits officer-to-supplier and evaluator-to-officer communication; confirm whether this is the required mediation route.

Planned tests: each allowed pair with correct involvement/stage; denied pair; unrelated same-role user; Supplier A attempting Supplier B's thread; spoofed recipient/context; response by unrelated user; unauthorized resolution; preserved message dates/history; notification addressed to the correct user; membership change during a conversation.

## Optional features with a mandatory minimum of two

| Candidate | Exact source purpose | Backend responsibility | Client/deployment dependency and open points |
|---|---|---|---|
| QR Code Generation | QR for each tender or purchase request to access details quickly | Generate code content for the related MVC details page; protect the record after navigation | Deployed base address and protected destination behavior need finalization; QR must not embed credentials or grant unauthorized access; image generation library undecided |
| Digital Signature Integration | Electronic signing of documents and official decisions | Integrate selected signing/verification mechanism and bind signature to exact document/decision | Signer identity, legal standard, certificate/provider, signature type, revocation/timestamp and signing UI unspecified; an approval record alone is not an implemented signature |
| Custom Watermark System | Add accessing/downloading user's information to tender documents and reports on view/download | Produce user-specific rendered derivative while preserving original; ensure authorized access and correct identity | Supported document/image/media types and viewing behavior unspecified; frontend must show derivative for viewing; caching must not expose another user's watermark/data |

**Provisional recommendation for user selection:** QR generation and custom watermarking have direct backend responsibilities and avoid an unspecified signature infrastructure. This is not an approved feature selection. Watermarking source documents on viewing as well as downloading must be addressed; watermarking reports alone would not complete the requirement. Required watermark user information, file support, output fidelity and resource cost need decisions. Digital signatures remain an available choice and are not omitted.

No optional feature is implemented in this phase. At least two selected choices must be completed before claiming source compliance.
