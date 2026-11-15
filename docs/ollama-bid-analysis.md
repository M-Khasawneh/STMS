# Local Ollama advisory bid analysis

## Delivered scope and authorization

V5 10.2 requires Ollama-assisted bid extraction/comparison, strengths/weaknesses and initial recommendations, while human committees retain decisions. The module now uses the real local Ollama qwen3:4b model. It extracts structured prices, requested specifications, offered technical details, quantities, optional charges, delivery, payment and validity from existing authorized bid/requisition records; document presence is derived from the actual bid attachment inventory. The document-text/OCR extension is now implemented; see bid-document-extraction.md for the current pipeline, provenance, setup and verification.

GET /AiAnalysis/Details/{tenderId} previews authoritative evidence and the current preserved result. POST /AiAnalysis/Run/{tenderId} explicitly generates an advisory result with antiforgery and stale-source validation. The entry link is on the existing authorized offer-comparison page. The original advisory increment added no Web API, frontend framework, NuGet package or role. The separately approved document extension now adds the three documented parsing/OCR dependencies.

Source loading reuses BidReviewPages and BidManagement/IBidData. Admin, RequestingDepartment, Supplier and anonymous actors cannot read or generate comparative analysis. Officers/managers follow existing closed-stage access; evaluation and Top Committee members require current tender assignments and their existing stage restrictions. Mixed Admin/Supplier role accounts cannot bypass internal eligibility. Fresh source authorization is repeated before returning a result and inside the persistence transaction.

## Authoritative facts versus AI advice

Server-derived decimal totals and source quantities remain authoritative. Supplier prose is treated as untrusted evidence, not instructions. The model has no tools, URLs, approvals, scores or workflow mutation actions. Its structured output must include each supplied offer exactly once, use known offer IDs and cite actual fact keys from that same offer for each observation. Dynamic schema enums constrain offered IDs and evidence references; server validation repeats those checks. Any suggested bid must already be Compliant; otherwise the suggested ID must be null. This does not grant eligibility or create an approval.

Reference validation proves that cited facts exist; it does not prove the model's natural-language interpretation is correct. The page explicitly requires human verification and links to original offers and protected documents. Model text is Razor-encoded, not rendered as HTML or executable markdown.

## Local deployment and bounds

The user separately approved downloading qwen3:4b. It was installed through the existing local Ollama service; no application dependency was installed. The approved download reported 2,497,293,931 bytes and digest 359d7dd4bcdab3d86b87d73ac27966f4dbb9f5efdfcc75d34a8764a09474fae7. See third-party/qwen3-license.md.

- HTTP endpoint is fixed to http://127.0.0.1:11434/api/chat, with proxies and redirects disabled. No posted/custom remote endpoint or cloud model is accepted. Model defaults to qwen3:4b; unsupported configured names fail clearly.
- Stream=false, think=false, schema-constrained JSON, temperature zero, context 16384 and bounded output generation.
- One model operation at a time per app instance; busy requests receive a safe retry message. Cached current results avoid duplicate inference.
- At most 20 visible offers and 16,000 UTF-8 bytes of serialized evidence. Oversized tenders fail clearly instead of silently omitting offers. Human comparison remains available.
- Model transport response <=128,000 bytes; advice JSON <=64,000 characters, with per-field/observation bounds. Malformed, incomplete, foreign-offer, unsupported-evidence and invalid recommendation outputs are rejected without persistence.
- A linked three-minute deadline covers headers and response-body reading. Cancellation, unavailable model, network errors and timeout cannot create a fabricated result. No automatic model download, Ollama startup or generation on GET/app startup is implemented.

## Storage and concurrency

Reuse AiBidAnalyses exactly as it exists: TenderId, requesting Identity user, model name, SHA-256 input reference, validated AnalysisJson and UTC CreatedAt. Input fingerprint covers the actual offered records, requested facts, visible tender stage and attachment inventory. Only a result matching the freshly authorized current fingerprint is shown; historical rows remain preserved without exposing stale content.

Inference runs outside a database transaction. Saving acquires the existing serializable tender lock, reloads permitted evidence and checks the fingerprint, reuses a concurrently saved equivalent result, and atomically inserts the analysis plus a metadata-only audit entry. No prompt, private storage reference or document bytes are logged in audit. Eligibility/source is checked again before commit. No bids, compliance decisions, scores, reports, approvals, awards or notifications are modified.

No migration or schema change is needed. Implementation and tests did not execute SQL writes or persist an analysis on existing supplier data. Actual persistence/write/concurrency behavior on SQL Server remains unverified until a separately approved integration test.

## Files

Models/Analysis/AiModels.cs; Data/{AiBidRules,AiBidData,AiBidManagement,OllamaBidClient}.cs; Controllers/AiAnalysisController.cs; Views/AiAnalysis/{Details,Problem}.cshtml; wwwroot/{css,js}/ai-analysis.*. Existing Program DI, portable appsettings.Example.json and ReviewBids/Compare link are updated. Actual local appsettings and database configuration remain untouched.

## Verification

- Build and compiled Razor: zero warnings/errors.
- --ai-checks-only: 22 response-schema/evidence/transport checks plus 26 real service/compiled MVC checks using memory data and a deterministic test model. Covers authorization, assignments/stages, antiforgery, confirmation, stale inputs/results, disabled accounts, concurrent inference, duplicate cache reuse, encoding and persistence failure. Workflow statuses remain unchanged.
- --ai-local-model-checks-only: real qwen3:4b generated a validated comparison for two synthetic suppliers, with no SQL, real suppliers or private files. A prior real one-offer probe also passed after tightening the evidence schema. Earlier rejected invalid-reference output was not persisted.
- tests/ai-analysis-browser.cjs: 22 Edge checks against an explicitly marked memory fixture: generation flow, confirmation, source-fact expansion, encoding, role isolation, no JS errors and 1440/768/390/320 px layouts. Browser test uses a test model; it is distinct from the real local-model probe.
- Regression suites passed: bid activity/internal visibility 54; reporting/export 50+46; manager review 40+29; Top Committee rules/service/MVC 128+49+23.
- SELECT-only SQL runner: 100 actual read/report/export checks, including new AI source/history queries for eligible existing SourcingOfficer, evaluation member and SourcingManager accounts. No model invocation or persistence was performed on SQL-backed evidence. No existing eligible TopCommittee account was available; its stage/assignment behavior was covered by memory checks.

## Explicit limitations

Authorized PDF text, rendered scanned pages and JPG/PNG OCR excerpts are now analyzed locally through the approved document extension. Exact excerpts, page references, OCR confidence and truncation/unreadable coverage are preserved and shown. See bid-document-extraction.md; source files remain private and immutable, and full visual/semantic accuracy is not claimed.

Large-tender analysis, semantic accuracy, broader model quality benchmarks and real SQL write/concurrency tests remain bounded/unverified as described above. Actual human approval and award workflows are unchanged.

Primary protocol reference: https://docs.ollama.com/capabilities/structured-outputs .
