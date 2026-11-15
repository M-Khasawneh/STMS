# Acceptance review — 2026-11-15

Application baseline: `f7604d0` (award/contract-reference reporting). This is an automated regression/acceptance review, not a claim that an end-to-end production SQL journey or physical biometric/microphone acceptance is complete. No business functionality, settings, migrations, existing records or private uploaded documents were changed during this review.

## Repeatable safe runner

`tests/Run-SafeAcceptance.ps1` uses a fixed allowlist of the existing .NET diagnostic modes. By default it builds without restoring dependencies and runs the schema model, HTTPS Identity/employee/department tests and 21 memory/native-file suites, sequentially. It does not start interactive fixtures, provision users in SQL, apply migrations, seed data or invoke any SQL-write diagnostic. Artifacts and logs must be outside the repository. Each run gets a unique directory, raw test logs and a JSON summary with exit codes, timings, explicit completion and enabled modes. Failed checks produce a nonzero exit; missing outputs mark the run incomplete. Only its own timed-out child process tree is terminated. There is no quota monitoring, scheduling or automatic resume behavior.

Example after the project's approved dependencies are already restored:

```powershell
pwsh -NoProfile -File tests/Run-SafeAcceptance.ps1 -ArtifactsPath C:/STMS-QA/artifacts -LogDirectory C:/STMS-QA/logs
```

Optional `-IncludeLocalModel` runs two existing synthetic-only local qwen3:4b probes; it does not install/start/download a model. Optional `-IncludeSqlReadOnly` runs the existing SELECT-only reporting diagnostic against its guarded configured target. Neither option enables SQL-write tests. `-SkipBuild` is appropriate only for current existing Debug artifacts. The process timeout defaults to 600 seconds; the application AI request retains its existing three-minute deadline.

## Automated result

The complete default allowlist was executed against the current Debug build: **23 suites, zero failures**, about 84 seconds sequential execution. Individual modes also ran while preparing the baseline. Logs/JSON are outside Git under the local QA workspace. The runner's final completion metadata and output-path guard additionally passed negative probes: missing assemblies return failure/incomplete, and repository-local output paths are rejected before directory creation.

| Area | Evidence from current safe run | Limit |
|---|---|---|
| Identity, employees, departments | Real HTTPS MVC/Identity over memory stores; login/logout, roles, antiforgery, lockout, Admin safeguards | Not a live SQL account-write exercise |
| Profile | 22 HTTPS profile checks and 12 private-picture checks | No real user changed |
| First Admin safeguards | Provisioning orchestration tests using simulated creation | Actual provisioning command not executed |
| Requisitions and tenders | Existing ownership, department, transition, criteria, quantities and UTC/deadline rule checks, exercised by default/bid runners | These are not a single connected SQL requisition-to-award journey |
| Supplier bid/document workflows | Bid phases 1–3 through the Phase 3 runner; 56 Phase 4 service/internal-MVC checks | Private files are synthetic; SQL locks/concurrency are not inferred from memory transactions |
| Compliance and evaluation | 61 compliance rules/private-file checks, 25 compliance MVC checks, 15 evaluation MVC checks | Full real SQL scoring/persistence still needs its approved separate integration exercise |
| Manager report review | 40 rules/immutable-version checks plus 29 MVC checks | Not a live SQL manager-decision write |
| Top Committee | 128 voting/award rules, 49 production-service memory-transaction checks, 23 MVC checks | No real votes, award or committee assignment written |
| Internal inquiries | 127 lifecycle/scope checks plus 17 MVC checks | Approved role/stage rules unchanged |
| Contact/messages/notifications | 19 contact and 21 inbox/read-state checks | No messages delivered to real accounts |
| Reports | 18 participation, 22 award-reference, 50 existing rule/export and 68 MVC checks | Actual-reference search cases cannot be claimed from empty SQL awards |
| QR/private personalized copies | 31 native worker/original-integrity/export checks | Physical-phone QR scanning on deployed HTTPS remains manual |
| OCR | Native PDF, scanned PDF, PNG/JPG and isolation checks | Broad document quality/handwriting accuracy is not certified |
| AI policy/service | 22 schema/transport plus 26 memory service/MVC checks | Fake-model success is distinct from actual inference success |
| Voice | 13 audience/MVC checks | Does not exercise a physical microphone |
| Passkeys | 16 native options/ticket/MVC checks | Does not demonstrate face/fingerprint hardware |
| Navigation/Admin dashboard | 59 workspace and 36 Admin projection checks | No new role grants or widgets |
| Schema | Eight existing migrations and 37 required tables/model constraints verified | No migrations applied |

Earlier verification at this same application baseline also passed 118 SELECT-only actual SQL read/report/export checks. The runner lacked an eligible existing TopCommittee account and returned no authorized award rows, so positive SQL award-search samples were skipped. The earlier independently approved supplier-expiry SQL test passed 30 checks with exact fixture cleanup and baseline content fingerprints; it was not rerun in this acceptance pass. Historical implementation documents retain their earlier counts and scopes; use this matrix for this run.

## Browser and real-model follow-up

- Native Identity/WebAuthn browser tests passed 27 checks with a virtual authenticator. The separate negative/security browser test passed 10 checks on a **fresh** memory-only TLS fixture, covering tampered origin/challenge, replay, missing user verification, disabled account and revoked eligibility. Its first invocation reused the lifecycle fixture and failed registration; fresh-fixture isolation passed. The original failed response did not record its HTTP status, so no precise cause is asserted. The test now includes registration HTTP status in its diagnostic and explicitly documents fresh-host isolation. No production rate limits or authentication behavior were changed.
- Voice browser: 38 checks passed using injected recognition events, including Arabic normalization, error/permission handling, keyboard/no-JavaScript fallbacks and responsive layouts. This remains a simulated speech event source.
- AI browser: 24 checks passed with a deterministic model and synthetic evidence, including encoded excerpts, source references, confirmation, audience/stage isolation and responsive layouts. This is distinct from actual local inference.
- Actual Arabic OCR was additionally run against the inspected, synthetic shaped-text image using STMS_ARABIC_OCR_FIXTURE: **28 native PDF/image/OCR/provenance/isolation checks passed**. This proves that sample, not general handwriting or layout accuracy.
- Actual qwen3:4b comparison of two synthetic offers **timed out at the existing three-minute deadline on the first attempt**. No result was persisted. After the other work completed, the actual extracted-PDF probe succeeded with a real document/page citation, and the isolated two-offer retry also succeeded with schema/evidence validation. These passes do not erase the first operational timeout or prove stable production latency.
- A local measurement while investigating showed about 0.7 GB free physical memory out of 15.7 GB, and the loaded model reported about 5.39 GB total residency with about 0.58 GB GPU residency. These are observations at that moment, not proof of a unique timeout cause. No model, context size, deadline, installed dependencies, service settings or user processes were altered to obtain a pass. Deployment resource/latency acceptance remains conditional.

## Actual-device and SQL acceptance still required

- A connected requisition-to-award journey in an explicitly approved isolated test database, including service transactions, stale forms, failure rollback, winner/supplier isolation and cleanup. Do not infer this from separate memory suites or SELECT-only queries, and do not create or clone a database automatically.
- Actual face and fingerprint sign-in on compatible authenticators, the approved PIN fallback, deployed HTTPS/RP configuration and cross-device behavior. A virtual authenticator verifies protocol/cryptography, not the biometric modality.
- Real English/Arabic speech recognition with browser permission and its speech service, plus denial/offline/unsupported-browser fallback. Injected recognition events are a simulation.
- Deployment QR scanning, private-store/font/native-parser configuration, backups/restore and resource/latency acceptance. No infrastructure or security configuration was changed as part of these tests.

The lack of physical interaction or separate SQL-write authorization does not justify inventing successful acceptance evidence. No contract-signing, fulfillment grading, bid withdrawal or other unapproved workflow is added to fill this checklist.
