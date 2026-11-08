# Database foundation

This increment covers WBS 1.1.2.3 and the first part of 1.2.1/1.2.2. EF Core SQL Server and dotnet-ef are pinned to 10.0.14. The initial migration creates Locations, Departments, JobTitles and Specialties in dahub_ita with identities, required fields, bounded Unicode strings, UTC timestamp defaults, active flags and unique lookup keys. It implements sections 2.1–2.4 of the local Data Model v4.6. SQL Server's development database uses SQL_Latin1_General_CP1_CI_AS; the model uses the same collation.

The remaining 27 first-release tables, lookup seeds, local authorization, append-only audit grants and employee workflows are pending. Department OU paths require operational confirmation. Assets remains deferred. No employee data or seeded application access is created. No lookup CRUD HTTP endpoints are exposed before authorization exists.

## Local setup

With Docker Desktop running Linux containers, use PowerShell 7:

```powershell
./scripts/setup-dev-database.ps1
./scripts/start-dev.ps1
./scripts/test-dev-database.ps1
```

Setup first invokes the existing local SQL setup, applies migrations explicitly, and creates dahub_app with SELECT/INSERT/UPDATE/DELETE on only the four lookup tables and SELECT on migration history. The API is never given sa or schema-change permissions. Setup preserves existing runtime credentials and database data when repeated.

The runtime connection is stored in Git-ignored .secrets/database-connection, protected with Windows ACLs (or mode 0600 on Linux). Keep it with the SQL volume. The administrator migration connection exists in a separate protected .secrets/database-migration file only during setup; the script removes it in finally. Migration and runtime secrets are mounted into different services as read-only Compose secrets. The ordinary build/test services receive neither secret. The isolated database-check service receives the local SQL administrator password only to create and remove its own disposable database and login.

start-dev.ps1 enables compose.database.yaml when the runtime file exists. If starting API manually, use both files:

```powershell
docker compose -f compose.yaml -f compose.database.yaml up -d api web
```

Database readiness is at https://localhost:5081/api/health/ready (also available through the frontend proxy). It returns 200 only after SQL connection and migration-history checks pass; otherwise it returns 503 with a generic Unhealthy body. Liveness stays independent at /api/health/live. This readiness check covers SQL, not AD or Okta.

Outside Compose, configure ConnectionStrings:DaHub or Database:ConnectionStringFile using a protected deployment configuration. The API rejects sa and optional/disabled encryption. TrustServerCertificate is allowed only in Development for the local SQL self-signed certificate; deployment requires verified SQL certificates. No connection string is stored in application settings.

## Migration and rollback

Migrations run only through an explicit tooling operation; the API does not migrate on startup. The initial migration includes a Down implementation. The real-SQL test applies Up twice, exercises constraints and restricted runtime access, applies Down to zero, checks unhealthy readiness, and reapplies Up. Each run uses a unique DaHub_Test_* database and dahub_test_* login, removed in finally. It never resets the developer's DaHub database. Ordinary verification reports this SQL test as skipped; test-dev-database.ps1 and the security CI job run it against real SQL.

To scaffold another migration, use the deliberate source-write override:

```powershell
docker compose -f compose.yaml -f .docker/format.compose.yaml run --rm --no-deps backend-check sh -c 'dotnet restore src/DaHub.Infrastructure && dotnet tool restore && dotnet ef migrations add MigrationName --project src/DaHub.Infrastructure --output-dir Persistence/Migrations'
```

For reviewable SQL, run dotnet ef migrations script --idempotent --project src/DaHub.Infrastructure in the same tooling container. For a rollback script, use dotnet ef migrations script InitialLookups 0 --project src/DaHub.Infrastructure. Neither script-generation command connects to SQL. Rollback drops the four lookup tables and their data; exercise it only in disposable test databases. The checked-in C# migration and snapshot are the source of truth.

The tools use a design-time factory with a credential-free placeholder for offline scaffolding. Database updates require the explicit migration connection file configured by setup. Future production deployment should use reviewed SQL or a migration bundle and a separately authorized deployment identity.

## Dependency remediation

The SQL provider's transitive dependency graph selected System.Security.Cryptography.Pkcs 9.0.11, which failed the existing NuGet audit gate. A direct 10.0.14 reference selects the patched version; no advisory suppression is configured.

References: [EF Core SQL Server package](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore.SqlServer/10.0.14), [Microsoft migration deployment guidance](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying), [Microsoft cryptography advisory](https://github.com/advisories/GHSA-846f-f764-76mx).

## Validation — November 7, 2026

Local verification passed backend/frontend builds, seven unit cases, two API integration tests, one frontend component test and formatting. The separate real-SQL migration/rollback/restricted-identity test passed. Setup ran twice successfully; the second run applied no migrations and preserved the runtime credential. Direct and proxied HTTPS readiness returned 200 Healthy with certificate validation enabled. Working-tree secret scanning and NuGet/npm audits passed. The administrator migration file was removed after setup and the runtime file is Git-ignored. Hosted validation is recorded on the pull request when published.
All three development images (backend, frontend and SQL Server) reported zero HIGH/CRITICAL findings in the final image scan.
