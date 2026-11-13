# STMS repository workflow

- Continue the existing ASP.NET Core MVC / Razor / .NET 10 / EF Core / SQL Server / Identity application. Preserve the V5 Word requirements, approved business rules, working routes, role permissions and completed modules.
- Keep work within the user's requested scope. Do not start another functional module without approval. Ask before database-changing operations; do not run migrations or seed data automatically.
- After each user-authorized change or completed phase, run the relevant safe checks, review the diff, commit the completed changes with a descriptive message, and push to the configured GitHub origin on the current branch. The user has requested this ongoing commit-and-push workflow; separate approval for an ordinary push is not needed unless they say otherwise.
- Include only the intended changes. Preserve unrelated work, and do not force-push, overwrite remote history, discard changes or delete branches without explicit authorization. If remote changes conflict or authentication is unavailable, report the issue rather than overwriting anything.
- Before committing, inspect staged files for secrets, private uploaded documents, local runtime data, database files/backups and machine-specific settings. Respect `.gitignore`; never force-add sensitive or ignored files.
- Use `appsettings.Example.json` for portable configuration guidance. Real `appsettings*.json`, credentials, all `App_Data`, local QA artifacts, build outputs and reference-only component sources remain local.
- Report the commit/push outcome and any blocked checks honestly. Do not claim a passing full suite when only targeted checks ran.
