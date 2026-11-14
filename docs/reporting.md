# Read-only reports and exports

Implements V5 section 7 over existing records and services. No entity, migration, workflow, status, role, seed or database-write operation was added.

## Available reports

- Requests: filtered records; status and department breakdowns; average approval days with a count of records having complete timing.
- Tenders: status distribution, submitted-offer/participation/zero-bid information only for readers already authorized to see the tender's submitted bids after closure. Admin and requester receive tender metadata without confidential participation details. Current UnderEvaluation elapsed time is descriptive, not an invented delay classification.
- Supplier verification: Admin-only application status, review dates, registration-document counts, expiry dates before today and unknown expiry counts. Missing dates do not imply indefinite validity. No document-renewal or suspension workflow is introduced.
- Offers: own offers for suppliers; existing closed-stage/tender-specific visibility for internal roles. Real JOD totals, itemized quantities/prices/specifications, payment/delivery/validity and taxes/fees/cost components. Totals reuse BidRules.Total; collections are fetched in batches within the authorized bid IDs.
- Evaluations: existing confirmed technical/financial weighted results and confirmation counts; Non-Compliant offers remain explicitly excluded. Existing evaluation aggregation and current membership rules remain authoritative.
- Decisions: stored approvals, report-version identifiers, reasons, existing voting rows and recorded award/contract-reference rows, scoped through authorized submitted bids. Empty sections remain empty; no final-decision or award functionality is implied.
- Unified report: authorized existing report service and immutable submitted snapshot, narrative, criteria/weights, comparison, itemized pricing/costs, observations and report transaction audit.
- Top Committee overview: V5 comprehensive aggregate counts and explicitly named recorded-award frequency. Only an enabled internal Top Committee member with an effective final-stage/awarded assignment can access this overview; tender/bid details remain assignment-scoped.

## User-approved metric definitions

Approval duration: first recorded Requisition.Submitted audit event to first Approved review. Resubmission does not reset it. Missing/reversed timestamps are excluded and the sample size is shown. The user approved submitted suppliers / invited suppliers × 100 for participation; zero invitations means unknown, not zero percent. Current evaluation elapsed days run from the first EvaluationStarted event through now (including rework) for current UnderEvaluation records; no lateness threshold is assigned.

Filters are parameterized: category, text, applicable status, authorized department/tender selection and inclusive Amman dates. Requests filter creation date; tenders filter submission deadline; offers/evaluations/decisions select source bids by submission date. Supplier applications have no registration timestamp, so unsupported date filters are rejected rather than approximated. The full applied filter description is included in exports. Large tables page at 25 rows; exports cover all matching rows. A 10,000-row/table limit and 250-page PDF cap reject oversize reports with a refine-filter message instead of silently truncating them.

## Security

Reporting uses fresh Identity/department/supplier eligibility. Admin restrictions win over combined employee roles; suppliers never gain internal reports. BidData.VisibleQuery remains the bid ownership/stage/membership source. Account access is refreshed before returning. Downloads rebuild the authorized report after rendering and reject a changed authorization/record scope. No-store, attachment disposition, sandbox and nosniff headers are applied. No report data is saved to disk or a new reporting table. HTML escapes values; XLSX uses inline strings for text, never formulas or external hyperlinks, with decimal numeric cells; amounts exceeding Excel’s 15-digit numeric precision are preserved as exact text rather than rounded.

The sidebar includes Reports & insights. A discovered pre-existing clarification sidebar literal was corrected to ApplicationRoles.EvaluationCommittee (the actual role is TechnicalFinancialEvaluationCommittee); the service/controller permissions were unchanged and a role-specific regression check was added.

## Export dependencies and deployment

Excel is genuine OOXML/XLSX built using framework ZIP/XML APIs, with styled/frozen/filterable headers and numeric cells. PDF uses the explicitly approved PDFsharp 6.2.4 dependency. Its exact version is MIT-licensed; later major/minor releases must be license-reviewed before upgrading. See third-party/pdfsharp-license.md. No React, Web API, frontend library, PDF service or browser process was added.

PDF is generated in memory, with wrapping, row continuation, repeated page headers and page numbers. Fonts resolve from Windows Arial by default, or DejaVu Sans at the documented Linux system path. STMS_REPORT_FONT_PATH can point to a deployment-owned Unicode TTF; fonts are not redistributed. Missing/unreadable fonts produce an unavailable-export message. PDFsharp does not provide full complex-script layout: Arabic/right-to-left shaping is not a completed localization capability; reports containing right-to-left text therefore hide/reject PDF export with a clear explanation. Web/Excel preserve the text; Arabic reports instead offer an authorized full-row print view using the browser’s native RTL layout and Save as PDF. Direct server-side Arabic PDF download remains unsupported; no corrupted PDF is generated.

## Remaining source requirements/dependencies

Supplier fulfillment/performance metrics and the meaning of transaction frequency beyond explicitly recorded awards remain undefined and lack completed workflow data. No proxy score is invented. Expiry capture/renewal remains a separate dependency; existing null dates are shown as unknown. Comprehensive global security-audit disclosure needs an approved role scope; only already-authorized transaction/report history is exported. Actual Top Committee voting/award execution is still a separate module; this report reads existing rows only.

## Verification

- Final build: zero warnings/errors.
- Reporting checks: 50 database-free permission/metric/query/export checks and 46 compiled MVC role/encoding/download/print checks passed.
- Browser: 40 Edge checks passed at 1440/768/390/320px, including real XLSX/PDF downloads, Arabic native PDF printing, filters, keyboard/mobile navigation, reduced motion and no JavaScript errors. English and Arabic fixture PDFs were rendered and visually inspected; screenshots/exports remain outside the repository.
- Real SQL: 87 read-only report/filter/export checks passed against existing eligible accounts; the SELECT-only interceptor rejected writes by construction. No eligible existing TopCommittee account was available, so that SQL path was not exercised. Current stored data does not cover every workflow state.
- Regressions: 59 workspace checks; internal clarifications (127 rules + 17 MVC); manager review (40 rules + 29 MVC); bid Phase 4 (54); default memory-backed HTTPS Identity/employee/department suite all passed. No SQL write tests were run.

Safe build and checks use external artifact/screenshot directories. --reporting-checks-only verifies permission categories, metric formulas, unknown denominators/timestamps, parameterized query translation, actual XLSX XML/numeric/literal-text output, parseable PDF, long-row pagination, compiled Razor routes/role guards/download headers and refusal when access changes during export. Browser tests check responsive layouts, filters, keyboard/mobile navigation, reduced motion, real downloads and JavaScript errors against a marked memory-only presentation host.

--reporting-read-only-checks uses the existing local configured database with an ApplicationIntent=ReadOnly connection and a SELECT-only EF command interceptor that rejects non-query and modifying statements. It reads eligible existing accounts only; no fixture users or records, SaveChanges, migrations, seeding, cleanup or export-file writes exist in that runner. Current data may not exercise every workflow state. In particular, no eligible existing TopCommittee account was available during this verification; its SQL path remains unexercised, while policy/MVC coverage uses memory fixtures. SQL write tests for internal clarifications remain separately unapproved.
