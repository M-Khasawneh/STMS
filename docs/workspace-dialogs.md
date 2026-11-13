# Internal dashboard and MVC form dialogs

## Behavior

- Anonymous `/` and `/Home/Index` retain the existing public website. Authenticated requests redirect to `/Workspace` before reading public statistics. Default successful login now redirects to Workspace; valid local return URLs are preserved. An already authenticated GET of Login also returns to Workspace.
- Workspace is an authenticated MVC page using the existing `SignedInAccount` policy. It presents links to implemented modules according to the current roles. It adds no statistics, new permissions, procurement features, or unfinished-module links. The authenticated brand, Dashboard sidebar link and workspace breadcrumb lead to Workspace rather than public Home.
- Create/Edit forms across Departments, Employees, Requisitions, Tenders and Bids open in an accessible native dialog. Criteria, invitation management and bid confirmation follow the same pattern. Existing inline decisions on details/list pages receive launch buttons; the employee enable/disable form is explicitly supported even on a directly opened edit page.
- Dialogs host the original same-origin MVC page in an iframe. There are no AJAX mutations, copied forms, rewritten antiforgery tokens or new API endpoints. Existing Razor bindings, named submit controls, file inputs, validation scripts, concurrency versions, cookies and service authorization remain authoritative. Normal links/forms remain usable as full-page fallbacks without dialog support or JavaScript.
- The embedded page hides its duplicate workspace shell. Inline decision dialogs retain the exact original form DOM and relevant validation feedback. All ordinary validation and file handling remain native MVC posts inside the dialog. Transient add/remove-row commands retain an unsaved-change warning across their server redisplay.
- Escape/Close support a discard prompt and focus restoration. Successful terminal actions refresh the original list/details page; ongoing bid editing, file uploads, criteria editing and add-item commands stay in the dialog. A terminal action launched from a direct editor fallback returns to a valid details/list page instead of reloading a now-ineligible editor.
- Navy/Blue/Cyan dialog and dashboard styling is isolated from the anonymous site. Large forms scroll within the dialog; simple/inline forms have compact dialogs. Focus indicators, reduced motion and mobile sizing are retained.

## Files

Created:

- `src/SourcingTenderManagementSystem.Web/Controllers/WorkspaceController.cs`
- `src/SourcingTenderManagementSystem.Web/Views/Workspace/Index.cshtml`
- `src/SourcingTenderManagementSystem.Web/wwwroot/css/workspace-ui.css`
- `src/SourcingTenderManagementSystem.Web/wwwroot/js/workspace-ui.js`
- `tests/SourcingTenderManagementSystem.AuthenticationChecks/WorkspaceUiChecks.cs`
- `tests/workspace-ui-browser.cjs`
- `docs/workspace-dialogs.md`

Modified:

- `Controllers/HomeController.cs` and `Controllers/AccountController.cs`: navigation redirects only; authentication/service rules unchanged.
- `Views/Shared/_Layout.cshtml`: authenticated Dashboard destinations and shared UI assets only.
- `Views/Employees/Edit.cshtml`: mark the existing enable/disable form for dialog enhancement; preserve text while converting its legacy Windows-1252 encoding to UTF-8.
- Test `Program.cs`, `DepartmentChecks.cs`, `EmployeeChecks.cs`: use the new authenticated landing destination and preserve local-return-URL expectations.
- Test `BidPhase3Checks.cs`: expand the isolated memory-only fixture to provide existing department/employee services and role-aware UI sessions; no SQL DbContext is registered.
- `tests/bid-browser.cjs`: follow final bid confirmation inside its new dialog.

No entity, DbContext, migration, role configuration, procurement service, uploaded document policy or Word requirements document changes were made. No SQL commands, migration applications or persistent test-account creation occurred.

## Verification

- Solution and Razor compilation passed with zero warnings/errors.
- 45 database-free workspace HTTP checks passed: all seven roles, authenticated Home/Login redirects, anonymous Home, role-aware links, anonymous dashboard denial, and proof that authenticated Home does not query public statistics.
- 52 browser checks passed: all role landings; department client/server validation; create/edit/discard/focus; employee creation and exact enable/disable form; 1440/768/390/320px parent/dialog layouts; supplier draft editing, real private-file upload/download and final submission; reduced motion; zero JavaScript errors.
- The existing 36 supplier-bid browser checks passed with dialog-based confirmation, including a directly opened editor's transition to read-only details.
- 61 Phase 3 MVC, 97 Phase 2 and 92 Phase 1 database-free bid checks passed, along with existing tender/requisition rule and supplier-upload checks.
- `--workspace-auth-regressions-only` passed the existing real HTTPS Identity/cookie, employee/department, permission, lockout, role/session and antiforgery checks using memory stores.
- Screenshots/results are outside the application under `workspace-ui-qa` and `workspace-bid-qa`. Browser fixture stores and private files are temporary and removed when the fixture shuts down.

## Known verification limits

The unfiltered legacy regression runner still fails its pre-existing public-home assertion requiring the literal `Unavailable` statistics label. The unchanged public Home view currently contains neither that statistics markup nor the old contact-form wording expected by its next assertion. The original full-suite assertions remain enabled. The explicit auth-only mode skips just those two unrelated legacy homepage assertions to exercise Identity regressions; it does not claim a passing full suite or repair public content outside this task.

No live SQL persistence tests or production-user sessions were used. Request/criteria/invitation inline forms use the same tested dialog transport, but every role's live SQL-backed workflow was not exercised end-to-end. Their server authorization and business logic are unchanged. No Supplier Bids Phase 4 implementation was started.
