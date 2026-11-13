# Admin department management

The existing Department entity and Departments table are reused. Only department name is editable. The existing department key and procurement relationships are retained; no deletion, status, code, employee assignment, or duplicate-name rule is introduced.

## MVC pages and actions

| Method | Route | Behavior |
|---|---|---|
| GET | `/Departments` | List departments by name with an empty state and edit links |
| GET | `/Departments/Create` | Display the create form |
| POST | `/Departments/Create` | Validate the name, create a server-generated key, and redirect to the list |
| GET | `/Departments/Edit/{id}` | Load an existing department or return 404 |
| POST | `/Departments/Edit/{id}` | Validate and update only the existing row's name, or return 404 for an unavailable row |

The controller enforces the existing Admin policy on every action. Anonymous users are challenged to log in, and authenticated non-Admins are denied. The navigation link is visible to Admins, but authorization runs independently on direct requests. Existing global MVC antiforgery validation protects both POST actions. All pages disable caching and reuse the Bootstrap layout and validation scripts.

The form ViewModel accepts only Name, trims surrounding whitespace, requires a nonblank value, and limits it to the existing 256-character database length. Data Annotations supply server and client validation. Edit identity comes exclusively from the URL route; submitted entity identifiers and other properties cannot modify keys or relationships. Razor encodes names in lists, links and forms. Successful changes redirect to the list with confirmation. Invalid forms redisplay validation errors without saving.

## Files

Created in `src/SourcingTenderManagementSystem.Web`:

- `Controllers/DepartmentsController.cs`
- `Data/DepartmentStore.cs`
- `Models/Departments/DepartmentFormViewModel.cs`
- `Views/Departments/Index.cshtml`
- `Views/Departments/Create.cshtml`
- `Views/Departments/Edit.cshtml`
- `Views/Departments/_DepartmentFields.cshtml`

Modified `Program.cs` to register the EF-backed department store and `Views/Shared/_Layout.cshtml` to add Admin navigation. Added `tests/SourcingTenderManagementSystem.AuthenticationChecks/DepartmentChecks.cs` and updated that project's `Program.cs` to run the department suite using in-memory fixtures. Added this document. No entity, account, role, schema, migration, connection setting, or original Word document was modified.

## Verification

- Solution build succeeded with zero errors and two existing NU1900 warnings about unavailable NuGet vulnerability-feed data.
- HTTPS tests passed for real MVC views, form binding, Identity cookies, Admin authorization, list/create/edit, empty lists, server validation, client-validation markup, antiforgery rejection, ID tampering, redirects and confirmations, invalid/missing edit targets, unavailable update targets, HTML encoding, the maximum name length, and absence of deletion endpoints.
- Existing authentication regression and offline schema/model/migration checks passed.
- Test users and departments existed only in memory. The live SQL database was not accessed or modified. End-to-end SQL writes were not exercised; the production store uses the existing scoped DbContext and EF SaveChanges.

Run with `dotnet run --project tests/SourcingTenderManagementSystem.AuthenticationChecks --no-build --no-restore`. On Windows, the HTTPS test harness may require permission outside the sandbox for TLS.
