# Roles and Permissions

## Scope and permission interpretation

The confirmed application uses ASP.NET Core MVC on .NET 10 (`net10.0`), EF Core 10 with SQL Server, ASP.NET Core Identity, and Razor Views with HTML, CSS and JavaScript, developed in Visual Studio 2026. All required pages and dashboards are in scope. There is no ASP.NET Core Web API. Permissions apply to MVC controller actions, underlying record queries, forms, views, downloads and exports; hiding navigation alone never grants protection.

Source: sections 2, 4, 6, 7, 10 and 12; paragraph locators are in [requirements-analysis.md](requirements-analysis.md). The seven role names below follow the source. Department Representative, Financial Representative and Technical Representative are evaluation-committee positions, not three additional system roles. “Technical & Financial Evaluation Committee” is the source's full committee name.

**Allowed** means expressly assigned or necessary to carry out an expressly assigned responsibility. **Restricted** means outside the role's stated remit; it does not assert that the source defines every denial. Where authority is missing it is **Unassigned**, rather than granted to a convenient role. Cross-cutting checks require current account status, role, record involvement and process stage.

## Role responsibilities

| Role | Allowed operations | Restricted operations | Data scope |
|---|---|---|---|
| Admin | System configuration; create/edit/enable/disable users; assign roles; manage departments/master data; review supplier application, tax/licence/legal documents; Pending/Approved/Rejected/Suspended outcomes with reasons; administrative counts | Explicitly not involved in technical/financial evaluation; no automatic award/decision permission; no implied unrestricted bid-content access | Administrative users, departments, supplier verification records and permitted summary counts; procurement-content access needs a separate rule |
| Requesting Department | Create requests/items; provide title/reason/priority/date/quantity/specifications; track department requests, history, status and review notes; clarify with involved sourcing officer | Cannot approve own request merely by holding this role; cannot create tenders, score bids, see competitor bid data or decide awards | Department requests; “My Requests” versus all department requests requires clarification for editing and detailed visibility |
| Sourcing Officer | Handle approved requests; create/manage tender dates/terms; select/invite suppliers; review and compare offers; compliance review and reason logging; monitor evaluations/report handoff; permitted clarifications | No implied request approval authority, committee scoring rights, manager or final decision rights; cannot change received bids on supplier's behalf | Related procurement records; assignment/all-officer visibility and sealed-bid timing are open |
| Evaluation Committee | Assigned committee reviews technical proposals/documents/media/samples; scores criteria, records noncompliance and observations; technical clarifications; approves technical results; reviews pricing/quantities/tax/fees/transport/installation/maintenance/payment/currency/validity; produces unified report and recommendation | No unrelated tender access; cannot perform manager/final approval by membership alone; cannot unilaterally change locked criteria | Evaluation assignment and tender records needed for that work; finance-versus-technical visibility timing is unspecified |
| Supplier | Register company/legal/contact details and official documents; view invitations; accept/decline; provide bid pricing/delivery/specifications/documents before deadline; track own bids; download own summaries/history; clarify with involved officer | Explicitly cannot view competing offers; no internal scores/report/audit/decision-detail access is implied; cannot approve own registration or edit another supplier's records | Own company account relationship, addressed invitations, own bid/items/documents and directly involved clarification threads |
| Sourcing Manager | Participates as one of four evaluation members when assigned; reviews unified report; approves/rejects/returns with comments; forwards high-level tenders when policy requires; clarifies with evaluation and Top Committee | Manager role alone must not confer arbitrary committee membership or final collective-decision authority; no assumed request approval power | Reports/recommendations and supporting records for manager review; organizational scope remains open |
| Top Committee | Four-member group; review requisition/bids/unified report/manager recommendation, evaluations and available AI; select supplier or seek details; documented decisions/notes/justifications; past tender/decision history; comprehensive statistics and supplier transaction-frequency export | No supplier bid editing or unilateral final action; cannot replace committee action with a single member's vote; quorum/aggregation not specified | Assigned decisions and necessary supporting records; source permits historical decisions and overall statistics but does not define historical confidentiality scope |

## Operation authorization matrix

Legend: **A** = role responsibility; **D** = department-bound; **O** = own supplier; **C** = current assigned committee; **I** = directly involved record; **U** = authority unresolved; **—** = no source grant. Conditions after the table apply to every cell. A dash is not an implemented authorization policy.

| Operation | Admin | Requesting Dept | Officer | Evaluation | Supplier | Manager | Top Committee |
|---|---|---|---|---|---|---|---|
| Manage employee accounts/roles/departments | A | — | — | — | — | — | — |
| Supplier self-registration | — | — | — | — | O | — | — |
| Verify/approve/reject/suspend supplier | A | — | — | — | — | — | — |
| Create request/items | — | D | — | — | — | — | — |
| View request progress/review notes | — | D | I | C | — | I | C |
| Submit/resubmit request | — | D | — | — | — | — | — |
| Review/approve/return/reject request | U | U | U | U | — | U | U |
| Convert approved request/create tender | — | — | A | — | — | — | — |
| Set tender terms/dates/invite suppliers | — | — | A | — | — | — | — |
| Set/approve evaluation criteria | U | U | U | U | — | U | U |
| View invitation/respond | — | — | I | — | O | — | — |
| Submit bid/items/documents | — | — | — | — | O | — | — |
| Read own bid/summary/history | — | — | — | — | O | — | — |
| Compare supplier bids | — | — | I | C | — | I | C |
| Compliance classification and reasons | — | — | A | Technical observations | — | As assigned evaluator | — |
| Technical/financial scoring | Explicitly excluded | — | — | C | — | C | — |
| Unified report/recommendation preparation | — | — | Track only | C | — | C as evaluator | — |
| Manager approve/reject/return/forward | — | — | — | — | — | A | — |
| Vote/review final collective decision | — | — | — | — | — | — | C |
| Record winner and close | U | — | U | — | — | U | Collective decision, execution U |
| Read own notifications | Own | Own | Own | Own | O | Own | Own |
| Search tasks | Permitted employee tasks | D | I | C | — | I | C |
| Read role-specific reports/export | Administrative counts | D | I | C | O | I | C plus source statistics |
| Read comprehensive security audit | U | — | U | U | — | U | Decision trail; broader scope U |
| Direct criteria changes after bids | Documented exception authority U | — | U | U | — | U | U |

Request submission/resubmission and a closure executor are necessary workflow operations, but the source does not prescribe who performs every step or the interface mechanism. The matrix does not settle those missing rules. Similarly, the source's “full audit trail” report does not explicitly appoint an audit administrator.

## Mandatory data boundaries

1. Supplier ownership must follow the authenticated account's trusted company relationship. Do not accept a user-supplied company identifier as proof. The source links SupplierProfiles to UserID; multiple contacts per company are an open question.
2. Apply isolation to searches, counts, exports, downloads, media previews, document metadata, clarifications and notifications as well as ordinary record views. A supplier's own report must exclude competing prices, identities where restricted, comments, attachment names and internal recommendations.
3. Committee access requires membership in the specific process. Holding the general role cannot reveal all tenders. There are four evaluation positions, including the manager, and four Top Committee members. Membership selection, replacements, effective dates, conflicts and quorum are unprovided.
4. Department membership and officer assignment rules must be defined before broad lists or detail access are enabled. The requirement to show department requests supports department-level tracking; it does not settle whether colleagues may edit each other's drafts.
5. Role combinations must not bypass an explicit restriction. In particular, Admin is excluded from evaluation and Supplier is isolated from competitors. Whether Admin can also hold an operational role is unresolved; no superuser bypass is assumed.
6. Disabling accounts, removing roles/membership or suspending suppliers must affect subsequent authorization. Handling already-submitted bids and historical committee work after these events needs business clarification.
7. A record's allowed state matters: owning a bid does not imply permission to alter it after the deadline; membership does not authorize changing locked criteria.

## Clarification communication matrix

Source P180–P184 permits only these role pairs, in both directions, with direct involvement and relevant stage checks:

| Pair | Source purpose | Required record connection |
|---|---|---|
| Requesting Department ↔ Sourcing Officer | Request, specification, quantity, required changes | Related request and participants |
| Sourcing Officer ↔ Evaluation Committee | Offers, documents, evaluation results | Related tender/bid and assigned committee |
| Evaluation Committee ↔ Sourcing Manager | Results, unified report, recommendations | Related evaluation/report and reviewer |
| Sourcing Manager ↔ Top Committee | Reports, recommendations, final decisions | Related tender/report/decision and committee |
| Sourcing Officer ↔ Supplier | Missing documents or offer clarification | Supplier's own offer and responsible officer |

No direct Supplier ↔ Evaluation Committee or Supplier ↔ Supplier channel is granted. The source's technical-clarification responsibility must be reconciled with these pairs: mediation through the officer is a candidate interpretation requiring confirmation. Stage-specific cutoffs, recipient substitution, thread participants and handling closed matters remain open.

## Verification plan

Test every operation against an authorized actor, a wrong role, a correct role with an unrelated record, an inactive actor, and an inappropriate state. Use two departments, at least two competing suppliers, two tenders with different committees, and old/revoked memberships. Exercise MVC page/action access directly as well as navigation, forms, reports, downloads, searches and identifier substitution. Verify protected state-changing forms use antiforgery validation. Confirm that denied attempts produce no business-state change or cross-supplier disclosure. These are planned tests; none have been implemented or executed.

## Confirmed requisition permissions

User-approved and implemented: RequestingDepartment users access only their own requisitions, may create and edit Draft/Returned content and submit. SourcingOfficer may view all and review across departments, excluding both own-created and own-department requests; officers cannot edit content. Admin may view all but has no requisition mutation rights. Suppliers and unrelated roles have no access. Restrictive Admin/Officer permissions take precedence in combined-role accounts. These rules supersede earlier unresolved requisition matrix cells above. See purchase-requisitions.md for transitions and tests.
