# Approved supplier participation indicators — 2026-11-15

V5 P104 requests supplier performance metrics without specifying fulfillment data or a quality formula. The user explicitly approved participation indicators from current data: invitation count, explicitly submitted offer count, submitted offers / invitations x 100, and recorded awards within existing visibility. No overall grade, delivery assessment or new business workflow is introduced.

## Where to find the report

- Reports > Tenders: an additional supplier-participation table for Sourcing Officers, Sourcing Managers and assigned evaluation/Top Committee members, only across matching tenders whose confidential participation data they may view after closure.
- Top Committee statistics: the same scoped table alongside the already implemented comprehensive statistics and recorded award frequency. Company search applies to the supplier tables. Existing site-wide frequency permissions are preserved; the new participation table is explicitly scoped to authorized tenders.
- Admin, RequestingDepartment and suppliers do not gain confidential supplier-participation permissions. Admin supplier verification reports retain their existing purpose. No new navigation, category, role or authorization grant was added.

## Definitions and source data

`Reporting.SupplierParticipationQuery` performs read-only SQL aggregates over existing TenderSuppliers, Invitations, Bids, TenderAwards and SupplierProfiles. The source tender scope applies existing role/department/assignment/stage restrictions; submitted offers additionally use the existing `BidData.VisibleQuery`, including current tender-specific committee membership. Matching tender IDs are authoritative server-side inputs from the existing report filters.

- Invitations: count tender-supplier links with an actual invitation. A link counts once even if inconsistent duplicate invitation rows were present. All invitation response statuses remain part of issued invitations.
- Submitted offers: matching offers with a non-null submission timestamp and a non-Draft status. They must belong to an invited link for that same tender. Drafts never count.
- Participation percentage: submitted offers / invitations x 100 using decimal arithmetic. No denominator means Not available, not a fabricated zero. Suppliers with no invitation in the authorized scope are omitted.
- Recorded awards: existing awards connected to an authorized submitted offer and the same tender. Reporting does not create or modify awards.

The table contains supplier name and aggregate counts only. It never projects unit prices, financial terms, proposal contents, documents or evaluation scores. A supplier that was subsequently suspended retains its recorded historical activity; approval status is not a performance score.

## Integration and verification

Existing Razor report rendering, responsive table overflow, pagination, definitions, current-identity watermarks, browser Print and genuine Excel/PDF downloads are reused. Participation counts contribute to the export ScopeKey so a changed aggregate scope is rejected by the existing post-render authorization check. Existing report filters select tenders by deadline/title/status/department; the percentage covers the filtered authorized scope, not the current table page.

Build passed with zero warnings/errors. Checks passed: 18 focused database-free formula/role/stage/SQL-translation/export checks, 50 existing reporting checks and 53 report MVC checks including four authorized role presentations and three restricted-role exclusions. Edge verified the actual Razor table at 1440/768/390 with no page overflow or JavaScript errors using synthetic memory data. Actual read-only SQL report/export checks passed (106 checks, SELECT-only interceptor); the existing runner had no eligible TopCommittee account, so live SQL Top Committee execution was skipped while its SQL translation/membership predicates and MVC presentation were covered.

No schema, migrations, dependencies, production data writes, test-account creation or private-file access were required. Fulfillment-quality scoring remains outside the approved definition and is not claimed as implemented.
