# STMS — Sourcing & Tender Management System

An existing ASP.NET Core MVC application using Razor Views, .NET 10, EF Core 10, SQL Server and ASP.NET Core Identity.

The original V5 Word document, maintained locally in the project root and excluded from Git at the owner's direction, defines the requirements. Approved workflow decisions and completed-module verification are documented in [`docs/`](docs/). Repository presence does not imply that every procurement module is implemented.

## Local setup

1. Install the .NET 10 SDK and make an authorised SQL Server database available.
2. Copy `src/SourcingTenderManagementSystem.Web/appsettings.Example.json` to `appsettings.json` in the same directory, then configure your local SQL Server connection. Alternatively provide `ConnectionStrings__DefaultConnection` through your environment or a local secret store. Do not commit real configuration or credentials.
3. Build with `dotnet build SourcingTenderManagementSystem.slnx`.
4. Run with `dotnet run --project src/SourcingTenderManagementSystem.Web`.

Do not apply migrations, provision users or initialise roles against an existing database without approval. Follow the existing database/provisioning documentation for a new authorised environment. Runtime private files under `App_Data` are deliberately excluded from Git and need their own secure backup/transfer when deploying.

## Safe checks

From the repository root:

```text
dotnet run --project tests/SourcingTenderManagementSystem.AuthenticationChecks -- --experience-checks-only
dotnet run --project tests/SourcingTenderManagementSystem.AuthenticationChecks -- --admin-dashboard-checks-only
dotnet run --project tests/SourcingTenderManagementSystem.AuthenticationChecks -- --workspace-ui-checks-only
dotnet run --project tests/SourcingTenderManagementSystem.AuthenticationChecks -- --bid-phase3-checks-only
dotnet run --project tests/SourcingTenderManagementSystem.SchemaChecks
```

These explicit modes use memory data, temporary files or connection-free model/schema checks. Other test modes can access SQL Server; inspect their documentation and obtain approval before running database-changing checks. Browser checks reuse an existing Playwright/Edge installation and are guarded to run against marked test fixtures.

## Git workflow

Completed, verified changes are committed and pushed to GitHub after each authorised task. See [`AGENTS.md`](AGENTS.md). Keep real application settings, credentials, uploaded documents, database backups and local QA/build artifacts out of Git. Generated implementation notes under `docs/` record relevant test coverage and known limitations.

No licence is asserted for third-party components. Existing vendor licence notices remain in their asset directories; reference-only design sources are excluded from this repository.
