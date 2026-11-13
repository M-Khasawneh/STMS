# Consistent Admin interface

The dashboard's shell styling was previously loaded only by `Views/Workspace/Admin.cshtml`. Other sidebar destinations therefore reverted to the older green theme. This UI-only follow-up extracts the shared shell into `wwwroot/css/admin-shell.css` and loads it from the shared layout for authenticated Admin pages. Dashboard widget styling remains in `admin-dashboard.css` and is loaded only on the dashboard.

The shared theme covers the page background, sidebar/active navigation, logo treatment, topbar/account identity, spacing, breadcrumbs, primary/secondary buttons, tables, existing status indicators, form surfaces and focus states. It also applies in MVC form dialogs without restoring their hidden duplicate sidebar/header. Mobile rules and the existing drawer/dialog behavior are preserved. Other roles and anonymous pages do not load the Admin shell.

Production files changed:

- `Views/Shared/_Layout.cshtml`: shared Admin stylesheet/body class, consistent identity/icons, and accessible current-page navigation.
- `wwwroot/css/admin-shell.css`: new shared shell and existing component theme.
- `wwwroot/css/admin-dashboard.css`: remove the extracted shared shell; retain dashboard-specific widgets.

Verification:

- Solution/Razor build passed with zero warnings/errors.
- 36 database-free Admin HTTP/data checks passed, including themed Departments/Employees/Privacy views without dashboard widgets.
- 32 shared-shell browser checks passed: sidebar navigation between Dashboard, Departments and Employees yields identical computed page/sidebar/header colors and accent; active navigation/account identity; Privacy theme; listing/dialog layouts at 1440/768/390/320px; blue form actions; mobile drawer/Escape; other-role isolation; zero JavaScript errors.
- The 52 existing workspace/popup browser checks passed with the updated Admin overview expectation, covering validation, save/refresh, employee decisions, focus/discard handling, supplier draft/documents/download/submission and reduced motion.
- External screenshots/results are in `admin-shell-qa` and `admin-shell-popup-qa` under the provided workspace. Data in browser fixtures is temporary test data only.

Tests updated: `AdminDashboardChecks.cs`, `admin-dashboard-browser.cjs`, `workspace-ui-browser.cjs`; added `admin-shell-browser.cjs`.

No production controllers, services, entities, authorization policies, routes, database records, schema, migrations or original Word document changed. No SQL commands were executed for this follow-up. Temporary browser fixtures are stopped and their private storage removed after verification.
