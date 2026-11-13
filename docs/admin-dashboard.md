# Admin dashboard

> UI follow-up: the dashboard's shared background, sidebar, header and component identity now extend to all authenticated Admin pages. See `admin-shell.md`. The original dashboard data/permission behavior and its verification below remain unchanged.

## Scope and requirements

V5 section 4.1 requires Admin counts of users, suppliers, departments, requests and tenders, plus the existing account/department/supplier administration responsibilities. Admin is not a technical/financial evaluator. The redesigned `Workspace/Admin` view replaces the welcome banner and navigation-card grid only for Admin. Other roles still receive the unchanged `Workspace/Index` view. All existing functional routes, dialog behavior, authorization policies and logout remain in place.

The five KPI cards show Employees, Departments, Suppliers, Requisitions and Tenders. The employee card additionally shows the total Identity user-account count, fulfilling V5's user count requirement while keeping supplier accounts distinct. Employee counting exactly matches existing Employee Management: exclude accounts with either a Supplier role or a supplier profile; include Admin and disabled/non-supplier accounts.

## Read-only data

`IAdminDashboard` / `AdminDashboard` is a read-only composition service. Production registers the EF-backed implementation; no production test/demo data or fallback statistical values exist.

- Query aggregates use actual Identity, Department, SupplierProfile, PurchaseRequisition and Tender records. Status distributions use the existing enums; later tender stages appear only when present in real records. Unknown stored status values are included rather than silently reducing totals.
- Operational counts: Pending supplier applications; Submitted + Under Review requisitions; Approved requisitions not yet converted; Open tenders at or past their configured deadline. Admin requisition/tender links remain read-only. No statuses are advanced or deadlines changed.
- Upcoming deadlines: at most four Open tenders with a future deadline, ordered by the existing deadline. Times use the established Asia/Amman helper.
- Recent activity: at most six existing PurchaseRequisition/Tender audit events joined to extant records. Only record title/ID, event, status and timestamp are selected. No audit logging, actor identification, reasons, bid details, proposals, documents, evaluation payloads or notification infrastructure is added. Events pointing at missing records are omitted to avoid dead links.
- Queries execute sequentially on the scoped context using no tracking/aggregates. Dashboard reads are ordinary read-committed observations rather than a cross-query transactional snapshot. Refresh performs a normal GET; there is no invented trend, real-time feed or background polling.
- Empty records yield true zero counts and explicit empty states. SQL/time-out failures produce an unavailable state with dashes, never fabricated zero counts. Response caching is disabled.

## Authorization

The existing authenticated Workspace route calls the aggregate service only when the principal has Admin. The service independently rejects anonymous/non-Admin callers before SQL access and refreshes the actual Identity user, roles and lockout state. It also checks the existing employee-department relationship and absence of a supplier profile/role, matching the current requisition/tender access requirements. A failed eligibility check returns Forbidden. Non-Admin roles never invoke the service and do not receive its model, stylesheet or layout modifications.

All card, queue, activity and deadline links use implemented MVC actions. No new role permissions, review actions or protected bid metadata links were added.

## Design and behavior

The Admin-only stylesheet supplies a Navy/Blue/Cyan administration shell, consistent sidebar icons/active state, compact heading, authenticated identity, five KPI tiles, operational queues, deadlines, proportional status bars and a recent-activity table. The existing sidebar/mobile drawer and keyboard behavior are reused. All other Admin pages and other-role dashboards retain their existing styling. CSS entrance/hover/bar transitions use transform/opacity and honor reduced motion; no animation/UI framework or new JavaScript dependency is used.

## Files

Created:

- `src/SourcingTenderManagementSystem.Web/Models/AdminDashboardModel.cs`
- `src/SourcingTenderManagementSystem.Web/Data/AdminDashboard.cs`
- `src/SourcingTenderManagementSystem.Web/Views/Workspace/Admin.cshtml`
- `src/SourcingTenderManagementSystem.Web/wwwroot/css/admin-dashboard.css`
- `tests/SourcingTenderManagementSystem.AuthenticationChecks/AdminDashboardChecks.cs`
- `tests/admin-dashboard-browser.cjs`
- `docs/admin-dashboard.md`

Modified:

- `src/SourcingTenderManagementSystem.Web/Controllers/WorkspaceController.cs`: select the Admin view only for Admin; preserve other roles' workspace.
- `src/SourcingTenderManagementSystem.Web/Program.cs`: register the read-only aggregate service.
- `src/SourcingTenderManagementSystem.Web/Views/Shared/_Layout.cshtml`: conditional Admin-dashboard body class, header identity/context, and two decorative navigation icons. Existing links/visibility/logout remain unchanged.
- Existing test `Program.cs`, `BidPhase3Checks.cs`, `WorkspaceUiChecks.cs`: register test-only dashboard data, add explicit test modes and adjust the Admin view expectation.

No entity, DbContext, migration, original Word document, business workflow, Identity configuration or unrelated production page was modified.

## Verification

- Solution/Razor build passed with zero warnings and zero errors.
- 33 database-free checks passed: populated/empty/unavailable/encoded views; no-store headers; role isolation and non-Admin query bypass; SQL translation; supplier exclusion; bounded, narrow activity projections; later-stage/unknown-status handling.
- 44 Edge/Playwright checks passed: real MVC rendering against isolated test fixtures; existing routes/active navigation/logout token; all five metrics, operation rows, status distributions and activity; 1440/1280/1024/768/390/320px layouts; mobile drawer/Escape; visible keyboard focus; empty/failure states; reduced motion; other Admin pages and all six other roles unaffected; zero JavaScript errors.
- 45 existing workspace routing/role checks passed. Existing HTTPS Identity, employee/department, lockout, roles/session and antiforgery regression checks passed in their explicit auth-only mode. That mode retains the previously documented exclusion of two unrelated legacy public-home assertions; no passing full-suite claim is made.
- Five checks against the configured existing SQL Server database passed under a command interceptor that permits only SELECT and blocks non-query writes. The real dashboard loaded, independent entity counts matched, the employee KPI matched existing Employee Management, and projections were bounded with no pending writes. No test records, user accounts, schemas or migrations were changed in SQL Server.

Browser screenshots use explicitly synthetic data in the test assembly to exercise populated layouts, empty states and error states; they are UI previews, not claims about production counts. Production uses only the EF-backed data service. Screenshots and logs are external QA artifacts under `admin-dashboard-qa` and `admin-dashboard-*.log` in the provided workspace. Test fixture memory/private storage is discarded when its host stops.

No migrations or database-changing commands were executed. Supplier Bids Phase 4 and other functional modules were not started.
