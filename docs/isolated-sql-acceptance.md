# Connected isolated SQL acceptance — 2026-11-15

The user explicitly approved the next acceptance stage after the safe regression pass: a connected SQL workflow in a separate test database. This diagnostic is **not** part of the default safe runner and must not be run without separate approval for database creation, schema preparation, temporary test data and cleanup.

## Guarded infrastructure

`--approved-isolated-sql-acceptance` generates an unconfigurable fresh `STMS_Acceptance_<32-hex-run-id>` catalog on the guarded configured local SQL instance. It first refuses an existing target. The original SourcingTenderDB connection is used only for SELECT-based before/after content fingerprints; no application service receives it. Private application settings are read, never changed.

The owned database is marked with an extended property matching its generated run ID. Existing migrations are reused unchanged. A historical data-dependent migration requires exactly one initial admin before employee department backfill: the diagnostic applies the initial schema, prepares that prerequisite through native Identity only inside the new empty database, then applies the remaining existing migrations. Eight migrations are verified. There is no new migration or production bootstrap behavior. This historical sequence is a deployment consideration, not permission to seed or change the real database.

The real production services use scoped EF/Identity stores connected only to the new catalog, a controlled test TimeProvider and private synthetic files under one generated temporary root. Time advances only inside this provider to pass the tender deadline; Windows time is not changed. Connections disable pooling, dependencies validate on build, and teardown disposes the provider before checking ownership and dropping the exact owned catalog. No SINGLE_USER, forced disconnection, broad database deletion or production-row cleanup is used. File deletion requires both the exact generated name and a resolved path below the temporary directory.

The explicit recovery mode `--cleanup-owned-isolated-sql <name>` accepts only the generated namespace and refuses deletion unless the stored marker matches the suffix. It does not accept production catalog names. Recovery remains a separately intentional operation, never automatic broad cleanup.

## Connected workflow verified

The final run passed 53 explicit checkpoints plus its completion record:

1. Native Identity employee accounts in the existing roles and two external supplier registrations, each with three real privately stored synthetic PDFs; Pending state and Admin approval through existing services.
2. Owner creates a purchase-request Draft; a same-department colleague cannot read it. Explicit submission, cross-department officer review and approval persist.
3. Saving the tender Draft links that same request and marks it ConvertedToTender atomically; duplicate conversion is rejected.
4. Officer configures synthetic-only 60/40 technical/financial criteria, distinct manager approves, tender publishes, two suppliers are invited, one accepts and the officer explicitly opens the tender. These are test inputs, never product defaults.
5. The accepted supplier saves an offer with the authoritative fractional quantity, JOD prices and optional charge. Real private Technical Proposal upload/verification integrates with explicit final submission. Competitor bid/document access and submitted-document mutation are denied.
6. A failure injected **after actual SaveChanges** rolls the bid, submission audit and receipt back. A subsequent real submission succeeds.
7. The clock advances beyond deadline and explicit closure expires the unanswered invitation. The officer records compliance; the manager assigns the four approved evaluation positions, including a source-department representative. All four confirm both criteria through the real SQL-backed service.
8. The Technical Representative saves and submits a unified report. A post-save injected failure rolls tender state, report submission, its audit and notification back; retry succeeds. The appointed manager approves the submitted recommendation.
9. Manager assigns four distinct Top Committee users and opens the round. Concurrent duplicate votes by the same member persist exactly once. Partial choices remain sealed from the officer. Four actual votes produce the collective approved outcome.
10. Concurrent officer award operations commit exactly one award. The stored award links the original tender, winning bid and approved round. The supplier receives only their own winning outcome.
11. Real SQL award reports return the actual winner by company search, tender/award-date/department filters, and assigned Top Committee access. Admin is denied. Missing contract reference remains Not recorded. Existing Excel/PDF exporters produce actual report bytes. Tender participation computes one submitted offer / two invitations = 50%.
12. Workflow notifications and final-award audit persist. Teardown removes the owned catalog and synthetic private files; all protected production table counts and SHA-256 content fingerprints match baseline.

This is a connected **production-service/SQL** test, not a claim that one browser session drove every Razor form, signed in as every role or tested every return/rejection/replacement branch. Existing HTTPS MVC, antiforgery and browser tests provide their separate evidence. SQL writes for real passkey ceremonies or live Ollama-result persistence were not exercised by this cycle.

## Harness corrections and evidence

- Initial preparation stopped at the unchanged department migration's first-Admin prerequisite; a pooled connection also blocked immediate drop. The exact owned failed database was recovered with marker validation and no forced disconnect. Pooling was then disabled, and the historical prerequisite was reproduced inside the isolated schema.
- The next run reached confirmed evaluations, then found a missing concrete EvaluationManagement registration in the **test container**. The container was corrected and validation enabled. Its database/files were cleaned and production fingerprints matched.
- The connected run then passed; a final strengthened run explicitly checked rolled-back audit/notification rows and positive assigned Top Committee/date-filter reporting. No application controllers, services, entities, permissions, workflows, migrations, settings or dependencies were modified to make the test pass.
- Final solution build: zero warnings/errors. Detailed logs stay outside Git in the local QA workspace. The default safe-runner allowlist remains unchanged and excludes this diagnostic.

## Physical device boundary

A read-only Windows preflight returned no Biometric-class devices and reported WbioSrvc stopped. This is not proof that all hardware/authenticators are unsupported, that PIN is unavailable, or that another user's device cannot use biometrics. No service was started, driver changed, camera enabled, audio captured or account credential enrolled.

Actual face/fingerprint/PIN and microphone acceptance requires the user at a compatible device. Do not substitute virtual-authenticator success or injected voice events for those demonstrations. Use [physical acceptance steps](physical-device-acceptance.md).
