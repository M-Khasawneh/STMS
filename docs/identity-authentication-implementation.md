# Identity authentication implementation

This step adds employee credential authentication using the existing Identity stores and schema. There is no employee registration, supplier registration, user provisioning, or default account. Supplier and mixed supplier accounts cannot use the employee login; supplier authentication remains deferred rather than choosing application-status access rules.

## Roles

| Requirement name | Internal Identity role |
|---|---|
| Admin | Admin |
| Requesting Department | RequestingDepartment |
| Sourcing Officer | SourcingOfficer |
| Technical & Financial Evaluation Committee | TechnicalFinancialEvaluationCommittee |
| Supplier | Supplier |
| Sourcing Manager | SourcingManager |
| Top Committee | TopCommittee |

Initialize roles only with explicit database-change approval:

`dotnet run --project src/SourcingTenderManagementSystem.Web --no-launch-profile -- --initialize-roles`

The command uses RoleManager, creates missing roles only, checks creation failures, preserves existing roles/memberships, and exits without serving requests. Normal startup does not initialize roles or apply migrations. No users or credentials are created.

## Security boundary

Login uses Identity password verification and its existing password/lockout defaults. Cookies are session cookies with Secure, HttpOnly, SameSite=Lax and a host-only name; HTTPS is required. Identity security stamps and current roles are revalidated each request. Locked accounts, accounts without an employee role, and accounts linked to supplier profiles are rejected. No custom password storage is used.

MVC forms validate antiforgery tokens globally. Logout accepts authenticated POST requests only. Login accepts only local return URLs and returns a generic failure message. Public home, privacy, error and authentication views are explicitly anonymous. Other MVC actions require employee authentication by default; named policies matching each internal employee role are available for future role-specific actions. These policies do not grant record ownership, committee membership, evaluation authority, or cross-supplier access. No procurement endpoints are added in this step.

## Unresolved supplier decisions

- Q24: Mandatory official documents and expiry/renewal rules remain unresolved.
- Q34: Account/company cardinality and duplicate tax/registration number handling remain unresolved.
- Q37: Upload formats, size/count limits, storage protection and malware scanning remain unresolved.

No answers have been selected. Supplier registration, uploads, supplier sign-in and Admin verification are deferred. Existing broader account lifecycle and biometric questions remain outside this employee credential milestone.

## Files delivered

Created under `src/SourcingTenderManagementSystem.Web`:

- `Security/ApplicationRoles.cs`
- `Security/RoleInitializer.cs`
- `Security/EmployeeEligibility.cs`
- `Security/EmployeeSignInManager.cs`
- `Security/AuthenticationConfiguration.cs`
- `Controllers/AccountController.cs`
- `Models/Account/LoginViewModel.cs`
- `Views/Account/Login.cshtml`
- `Views/Account/AccessDenied.cshtml`

Modified: `Program.cs`, `Controllers/HomeController.cs`, `Views/Shared/_Layout.cshtml`, and the root `SourcingTenderManagementSystem.slnx`.

Created this document and `tests/SourcingTenderManagementSystem.AuthenticationChecks/{SourcingTenderManagementSystem.AuthenticationChecks.csproj,Program.cs,MemoryStores.cs}`. No entities, migrations, original requirements, or database connection settings were modified.

## Verification results

- Solution build succeeded with zero errors. Two NU1900 warnings report unavailable NuGet vulnerability-feed data; an elevated restore retry did not clear those warnings.
- HTTPS MVC checks passed using real Identity password hashing, SignInManager, MVC views, cookies and authorization with in-memory user/role stores and a test eligibility implementation. The tests do not connect to SQL or provision persistent users. They cover role initialization twice with existing-role preservation; login/logout; invalid/unknown/locked/unassigned credentials; server/client validation markup; antiforgery; no registration endpoints; role denial; safe local return URLs; cookie flags; current-role changes; stamp revocation; profile-linked supplier exclusion; and failed-password lockout.
- The existing offline schema checks passed, including all 36 application tables and migration snapshot consistency.
- With explicit approval, the initializer created the seven roles in SQL Server. Read-only verification confirmed seven roles, zero users, 37 tables including migration history, the unchanged initial migration record, unchanged non-role row counts, and no disabled/untrusted foreign keys or check constraints.
- Successful sign-in with a SQL-backed employee was not exercised because no employee accounts exist and provisioning users is outside this task. No default Admin was created.

Run authentication checks with `dotnet run --project tests/SourcingTenderManagementSystem.AuthenticationChecks`. On Windows, the test process may require permission for the local TLS listener/key container. The test certificate is temporary and is not installed into a trusted certificate store.
