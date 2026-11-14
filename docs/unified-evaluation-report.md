# Unified committee report

The approved author is the currently appointed Technical Representative. Preparation requires the UnderEvaluation stage and confirmed evaluations from all four current members for every Compliant offer. The author saves a draft containing observations, additional non-compliance notes, a final result, a chosen compliant offer and a substantiated recommendation. The saved draft has a separate submission preview and explicit confirmation.

Technical/financial results, criterion averages and weights, individual confirmed assessments, prices, authoritative quantities, optional costs, payment/delivery/validity terms and compliance reasons are gathered from the existing application data. The existing evaluation service supplies the weighted calculations. No automatic supplier selection is performed. Excluded offers remain documented in the comparison and cannot be recommended.

## Storage and consistency

The existing EvaluationReports table is reused without a migration. Comparison stores a versioned JSON snapshot of the protected report inputs. TechnicalSummary and FinancialSummary contain generated weighted summaries. Existing narrative fields retain the author's findings. The snapshot excludes uploaded-file storage references and Identity credentials. Submitted legacy reports whose Comparison contains ordinary text remain readable.

The snapshot is captured when saving the draft. Submission requires the source snapshot and saved findings to match the reviewed draft. Changed evaluation inputs require reviewing the full report and saving a refreshed draft before confirmation. A version hash covers tender state, stored report fields and current source data. Existing serializable tender locks protect concurrent draft creation, saves and final submission. Duplicate existing report rows produce a data-conflict error rather than silent selection or overwrite.

Submission fixes SubmittedAt, preserves the full comparison snapshot, moves the tender from UnderEvaluation to AwaitingManagerReview, adds report/tender audit entries and sends one in-app notification to the committee's Sourcing Manager. These writes share one SQL transaction. Subsequent report edits are rejected. Later membership replacements do not recalculate the submitted report. Retired members still lose protected access according to the existing bid rules.

## Access and pages

Reader authorization reuses current Identity accounts and submitted-bid role/tender/stage scopes. Managers and officers have read-only report access. Current assigned committee members can read; only the Technical Representative can prepare/submit. Top Committee access remains limited to its assigned final-decision/history stages. Supplier, RequestingDepartment-only and Admin accounts gain no confidential report access.

`UnifiedReports/Details/{tenderId}` displays live preparation progress or the submitted snapshot. `Save` is a prefixed Razor form POST. `Submit` provides a GET preview and an antiforgery-protected POST. Links appear in internal offer groups and evaluation details. Manager notifications route directly to the report. Forms, tables and confirmation use the existing visual identity. Comparison tables scroll within labeled keyboard-accessible regions on mobile.

## Files

- Controllers/UnifiedReportsController.cs
- Data/UnifiedReportManagement.cs
- Models/Bids/UnifiedReportModels.cs
- Views/UnifiedReports/Details.cshtml, Submit.cshtml and Problem.cshtml
- wwwroot/css/unified-reports.css
- Existing Program registration and evaluation/offer/sidebar/notification integration
- AuthenticationChecks/UnifiedReportSqlChecks.cs and UnifiedReportPresentationChecks.cs, shared fixture registration/cleanup and tests/report-browser.cjs

## Verification

- Build passed with zero warnings/errors.
- Read-only SQL preflight found zero existing reports and no duplicate report/tender records.
- `--report-sql-checks-only` passed 31 report-specific checks after the existing 29 evaluation checks. Cases include exact persisted decimal comparisons, all current member notes, writer/reader isolation, stale forms, foreign recommendations, optional draft fields, incomplete recommendation rejection, legacy/outdated draft refresh, injected audit failure rollback, concurrent submission, unique notification/audit, immutable submission and snapshot preservation after membership replacement.
- `--report-checks-only` passed 18 database-free MVC checks using the real compiled Razor pages and a presentation-only service. Cases include role filters, CSRF, form prefixes, empty optional field binding, text length validation, submission action wiring and escaped observations.
- `tests/report-browser.cjs` passed 47 Edge checks at 1440, 768, 390 and 320 px. Checks cover draft/confirmation/submitted screens, read-only manager display, readable horizontally scrolling comparison columns, keyboard focus, explicit confirmation, reduced motion and no JavaScript errors.
- Existing bid Phase 4, compliance and schema regressions passed. The model remains at 37 tables and six migrations.
- Temporary SQL records, memberships, scores, reports, notifications and files were cleaned. Existing Identity accounts and role assignments were compared against their baseline.

Manager decision actions and later report revision/return policies belong to the following module. This increment provides the submitted report and manager handoff.
