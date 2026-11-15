# Final awards and contract-reference report — 2026-11-15

## Requirement and scope

V5 P106 includes final awarded contracts within approvals/decision reporting. The current schema has TenderAward with tender, winning bid, final approval, award timestamp and optional ContractReference. It does not have a signed-contract document, execution dates or an authoritative contract value. This delivery reports the available award/reference records honestly and leaves missing references as Not recorded. It adds no contract creation, reference editing, signing or fulfillment workflow.

## Delivered report

Reports > Awards & contract references (`Category=awards`) is a separate read-only presentation of data already within the existing internal decisions-report permissions. It shows tender title, winning supplier, award time in Asia/Amman, recorded reference, award ID, tender ID, winning bid ID and approval ID. Metrics distinguish matching awards, references present and references not recorded.

Search matches tender title, winning supplier name or the recorded reference. Filters support an available awarded tender, source department and inclusive Amman award-date range. Date filters apply to AwardedAt rather than bid submission or tender deadline. No new contract status is invented; a posted status filter is rejected. Records are ordered by award time descending and ID, with the existing 10,000-row limit and 25-row web pagination. Empty results remain empty.

Existing responsive Razor report components, current-user watermarking, browser Print, genuine XLSX and PDF exports are reused. Arabic report content uses the existing browser Print/Save PDF or Excel path because native PDF RTL shaping remains unsupported. No new libraries or frontend framework are introduced.

## Authorization and integrity

The category is available to the same roles as internal decisions: SourcingOfficer, SourcingManager, assigned evaluation members after closure and assigned Top Committee members at their permitted final/awarded stage. Admin confidentiality overrides mixed roles. Admin, suppliers and RequestingDepartment gain no award-report permissions. Existing supplier-specific outcome/history pages remain unchanged.

The source query requires an internal actor and the authoritative `BidData.VisibleQuery` for the winning bid, matches bid and award tender IDs and verifies the tender-supplier linkage. Current membership and stage predicates remain in SQL. It projects descriptive metadata only, without proposal fields, documents, unit prices or inferred contract amounts. After projection, winner access and account permissions are rechecked; the existing export reauthorization compares a scope key containing a SHA-256 digest of each report row so changed references or metadata invalidate a stale export.

Tender options are limited to authorized winning bids with actual award records. Existing approvals/votes/award tables and workflows are untouched; the original decisions report remains available.

## Verification

- Solution build: zero warnings/errors; Razor compilation passed.
- 22 focused database-free tests cover field fidelity, unknown references, Amman display/date boundaries, parameterized search/department/tender SQL, Admin override, role/membership/stage restrictions and genuine Excel/PDF generation.
- Reporting runner passed 18 participation checks, 50 existing reporting checks and 68 real MVC checks, including authorized award pages, restricted-role page/download/print denial, original filters and actual XLSX/PDF downloads.
- 118 actual SQL read/report/export checks passed under the SELECT-only interceptor; the runner includes award date filters and, where existing rows permit, source-title/company/reference searches and tender-filter checks. No test accounts, data writes, migrations or files are created by it. The existing data had no eligible TopCommittee account, so live Top Committee execution was skipped; its query translation and MVC presentation were covered. No existing authorized award rows were returned in this run, so positive SQL search/tender-filter samples were skipped; those predicates were verified through SQL translation and memory rendering, not newly inserted award records.
- Edge verified the compiled award report and print page, correct filter labels, absence of a fabricated status selector, unknown references, reduced-motion presentation, 1440/768/390 layouts and no page overflow/JavaScript errors using a memory fixture. Screenshots are outside the repository.

No database schema, migrations, stored records or private documents were modified. This report is not evidence of contract signing or execution; those data/workflows are not supplied by the current requirements/schema.
