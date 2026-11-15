# Technical and functional review - 2026-11-15

Scope: existing MVC application, current migrations/model, real read-only SQL, production authorization policy, protected files and workflow regressions, browser behavior, JavaScript syntax and installed-package vulnerability metadata. No migrations, new packages, production SQL writes or new accounts.

## Confirmed defects fixed

1. **Workspace tasks/search failed against string-backed SQL statuses.** The new discovery projection cast enum states to `int`, while the existing EF model stores enums as strings. Actual SELECT-only SQL reproduced error 245 converting `Pending` to int. `WorkRecord.State` and all discovery projections now use strings, and Razor labels use that state directly. SQL-shape regression checks forbid numeric casts of string-backed statuses. The unchanged database subsequently passed the complete workspace read-only probe.
2. **Supplier access was incorrectly blocked by the default employee authorization policy.** Bare `Authorize` on Reports and TopCommittee selected the employee-only default policy, preventing approved suppliers from reaching their personal reports and service-scoped outcome metadata. Both controllers now explicitly select the existing `SignedInAccount` policy; action-level roles and application-service ownership/stage/confidentiality checks remain unchanged. The memory MVC host now uses the production employee-only default policy, preventing this false-positive test gap. The test-only notes preview declares its intended signed-in policy explicitly. Tests still deny suppliers confidential committee actions, competitors' data, and unrelated reports.
3. **Requisition 'today' depended on the server's local timezone.** Requisition target-date validation now derives today from the established business timezone through TenderTime rather than the host timezone. A midnight-boundary regression proves identical behavior on UTC and UTC-8 hosts. No visible timezone labels were reintroduced and no dates/records were rewritten.

Earlier safe tests alone did not expose the first defect or the policy mismatch. The new actual SQL and production-policy checks reproduce those failure classes rather than treating successful compilation as functional proof.

## Final verification

- Debug and Release solution builds succeeded with warnings treated as errors: zero warnings/errors.
- All 24 fixed safe-acceptance suites passed after the fixes: schema, Identity/employee/department behavior, work discovery, UI, supplier expiry, bids/documents, compliance, evaluation, reports, committee/manager decisions, inquiries, contact/inbox, voice/passkey protocols, AI/extraction and personalized copies. The final safe run also passed both optional SELECT-only suites.
- Work discovery: 111 database-free query/role/redaction/channel checks plus 98 memory-only compiled MVC/live-notification checks passed.
- Reporting: 18 participation, 22 award, 50 rules/export and 70 MVC/policy checks passed.
- Actual SQL: 118 existing read/report/export checks and 47 workspace task/search/filter/audit/portal checks passed. The interceptor rejects non-SELECT commands. Existing eligible accounts covered six roles; no eligible TopCommittee account was available, so its positive live-SQL access was not manufactured. Committee role/stage/confidentiality tests passed in memory.
- Browser: 264 public navigation/footer/login/contact checks and 40 report filter/download/print/keyboard/responsive/reduced-motion checks passed using isolated, marked, memory-only hosts. No page JavaScript errors were reported. Contact submissions were memory-only, not sent to real Admin accounts.
- All 20 application JavaScript files passed `node --check`.
- NuGet vulnerability audit of direct/transitive dependencies reported no known vulnerable packages from the configured sources at review time. No dependency versions were changed.
- Git-tracked paths contain no actual appsettings.json, App_Data, database backups or SQL data files. Local QA outputs stayed outside the repository. Original Word requirements were not modified.

## Remaining verification limits

This review is evidence for the checked scenarios, not a guarantee that every future input, deployment or concurrency schedule is defect-free. No production SQL mutations or new destructive/concurrency integration runs were authorized or executed. Historical approved isolated SQL acceptance remains separate evidence. Face/fingerprint hardware, real microphone recognition and deployed HTTPS/origin behavior require device acceptance. A virtual authenticator or injected speech event is not a physical demonstration. Broad OCR/model accuracy and sustained production load are not certified by synthetic samples.

Actual local-model follow-up passed: the installed local qwen3:4b generated a schema/evidence-validated advisory comparison for two synthetic offers. No real supplier files, SQL records or workflow decisions were supplied or changed. This confirms that probe, not broad semantic accuracy or sustained latency under production load.
