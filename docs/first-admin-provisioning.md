# First Admin provisioning

Implementation is ready for review. Real execution requires separate explicit approval. No default credentials are supplied or generated.

After execution approval, run from a private interactive PowerShell terminal at the solution root:

```powershell
dotnet run --project src/SourcingTenderManagementSystem.Web --no-build --no-launch-profile -- --provision-first-admin
```

Use this flag alone. The fixed username is `admin`. The command reuses the existing configured SQL connection, UserManager, RoleManager, and Admin role. It refuses redirected input/output, a missing Admin role, or any existing user. Email is requested only when existing Identity options require it; no email confirmation is fabricated. Password and confirmation are read with console echo disabled. Escape cancels. Passwords must not be passed as arguments, configuration, environment variables, chat messages, or redirected input.

Identity validates and hashes the password through `UserManager.CreateAsync`. The account and its sole Admin membership are created in the same scoped DbContext transaction. A serializable transaction and exclusive user-table lock protect the final empty-database check against concurrent user creation. Credential entry happens before taking that lock. Any failure before commit disposes the uncommitted transaction and rolls back the changes. No website starts, no roles are initialized, and no migrations run. A later attempt is refused as soon as any user exists. Command logging providers are disabled, and raw errors and validation descriptions are not printed.

Exit code 0 means provisioning committed. Exit code 1 means refusal, cancellation or failure. If commit/connection failure leaves the outcome uncertain, inspect database state before retrying; do not delete or alter accounts to bypass the guard.

Run offline safeguard tests:

```powershell
dotnet run --project tests/SourcingTenderManagementSystem.AuthenticationChecks --no-build --no-restore -- --provisioning-checks-only
```

These tests use in-memory stores and simulated transactions, not SQL. They do not generate credentials or create persistent test accounts. They cover redirected consoles, missing roles, existing users, cancellation, mismatched input, real Identity rejection of empty passwords, changes during prompting, role-assignment/commit exceptions and rollback, unexpected roles, configured email requirements, fixed username/sole role, and refusal of repeated provisioning. Successful password creation is simulated; live SQL transaction/locking behavior and real-account verification await separately approved execution.

After approved execution, verify only non-secret account metadata and role membership: exactly one `admin` user, membership only in Admin, and unchanged tables/migration history. Never select password hashes, security stamps or tokens for display.

## Implementation verification

- Solution build passed with zero errors and two existing NU1900 warnings for unavailable NuGet vulnerability-feed data.
- All 19 offline provisioning safeguard assertions passed.
- Existing offline schema/model/migration checks passed.
- The actual provisioning command has not been executed. No SQL connection was opened during this implementation step, and no real Admin or persistent test account was created.

Files added: `Security/FirstAdminConsole.cs`, `Security/FirstAdminDatabase.cs`, `Security/FirstAdminProvisioner.cs` in the MVC project; `FirstAdminChecks.cs` in the existing authentication checks project; and this document. Files modified: MVC `Program.cs` for the explicit command branch and service registration, and authentication-checks `Program.cs` for the offline test switch. No entities, migrations, schema, connection settings, or unrelated features were changed.
