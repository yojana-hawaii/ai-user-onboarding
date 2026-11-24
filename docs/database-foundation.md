# Database schema and API persistence

The schema, including the WBS 1.7 mock provisioning extension, has 33 tables in the dahub_ita schema on SQL Server 2022. EF Core SQL Server is pinned to 10.0.14; the separately pinned dotnet-ef CLI is 11.0.0 and runs on the .NET 10 toolchain. InitialLookups creates four lookup tables; CompleteFirstReleaseSchema adds 27 tables, WorkerAttempts adds BackgroundJobAttempts and the four-attempt default, and MockProvisioningActions adds the durable per-step action table. Operational catalogs are loaded separately, not seeded by these public migrations. See background-worker.md for the execution contract and migration prerequisites. Assets remains deferred. Operational OU configuration and application workflows remain separate work.

The model contains 321 columns across 33 tables after `MockProvisioningActions`, covering lookup data, additive access templates, employee profiles and primary assignments, account/employment history, local users and external identities, roles/permissions, dated delegations, requests and immutable revisions, approvals and urgent authorizations, provisioning steps/actions, audit records, helpdesk ticket records, background jobs, exports and notifications. The new action table adds 14 columns for immutable execution identity, ownership, one prerequisite and bounded result evidence; see [the mock integration boundary](background-worker.md#mock-provisioning-boundary--wbs-1722). No employee records, app users, identity links or user-role assignments are seeded. No business HTTP endpoints or external provisioning actions are introduced.

## Constraints and catalog data

- All FKs use NO ACTION deletion, preserving referenced history. Composite request/revision FKs prevent approvals or urgent authorizations from targeting nonexistent revisions.
- EmploymentPeriods uses a composite account/employee FK and a supporting alternate key on EmployeeAccounts to enforce the documented account-ownership rule. This strengthens the illustrative single-column DDL without changing its columns.
- Exact issuer/subject keys use Latin1_General_100_BIN2 collation and a unique constraint. Duplicate keys remain reserved even after a link is revoked. Contacts such as UPN do not automatically link accounts or grant roles.
- The four employee-assignment junctions have composite primary keys and filtered unique indexes permitting at most one primary assignment. The application persistence guard below enforces required primaries before HR verification and on subsequent profile/assignment writes.
- Worker types, workflow states, delegation date ranges, positive revisions, JSON revision/action payloads and nonblank urgent reasons use CHECK constraints. Approval, execution and resolution states remain independent. Scheduling fields preserve datetime2 UTC instants.
- Filtered unique indexes protect non-null historical AD and Entra identifiers while allowing unknown values during import.
- Seeds define seven roles, 19 permissions and the 39 documented role-permission grants. The WBS row mentioning 16 permissions is historical; the current v4.6 permission list and matrix govern. UrgentOperator grants only provisioning.urgent; no base role receives it, and approvals.bypass is absent.
- Organization locations are no longer inserted by public migrations. Existing installed lookup IDs and edits remain untouched. Load approved locations, departments/OU paths, job titles, specialties and access templates through the explicit [organization seed upload](open-source.md); operational confirmation remains required. SQL tests create synthetic catalogs explicitly.

## Verified-worker profile guard — WBS 1.2.2.3

`EmployeeProfile.ValidateVerified` requires nonblank legal names, username and email; FT/PT/Contractor/Volunteer/Student/Temporary; a start date; an optional end date no earlier than the start; and HR verification evidence. A null manager with the HR attestation represents the explicitly confirmed no-manager case. A selected manager must exist (SQL FK) and cannot be the worker. Exactly one primary department, location and job title is required. Specialties are optional, with exactly one primary when selected. No end date is mandatory for a particular worker type.

Both synchronous and asynchronous `DaHubDbContext.SaveChanges` validate the resulting stored profile, including assignments not loaded into the tracker. PendingHR imports may retain missing dates and assignments; lifecycle request writes require Verified. Verification, later profile edits and assignment additions/removals are checked within a serializable transaction before commit. Caller-owned transactions must be serializable with savepoints; failures roll back the save without accepting tracked changes. Discard/reload the attempted tracker state after a failure. Callers retry a deadlocked transaction as a whole; the guard never retries a partial write. Null manager confirmation is covered by VerifiedByUPN/VerifiedAt rather than a new schema field.

These gates protect tracked application writes. Raw SQL, `ExecuteUpdate` and `ExecuteDelete` bypass SaveChanges and must not be used for profile/workflow mutations. Controlled migrations/maintenance must enforce the same rules. HR authorization endpoints, stable-actor WorkerVerified audit generation, import and provisioning workflows remain separate work; the guard grants no authority to verify a worker. Schema, columns, relationships and migrations are unchanged. No persistent database migration or external action was executed.

## Local setup

With Docker Desktop running Linux containers, use PowerShell 7 on the development workstation:

```powershell
./scripts/setup-dev-database.ps1
./scripts/start-dev.ps1
./scripts/test-dev-database.ps1
```

Setup invokes local SQL setup, applies outstanding migrations explicitly and provisions the dedicated dahub_app SQL identity. It adds that user to dahub_app_runtime. The role has explicit SELECT/INSERT/UPDATE/DELETE grants on each mutable table. AuditLog, RequestRevisions and UrgentExecutions allow SELECT/INSERT and explicitly DENY UPDATE/DELETE. Migration history is read-only. There is no schema-wide writer or schema-change grant. Production deployment must explicitly assign its own approved service identity to this role.

The runtime connection is stored in Git-ignored .secrets/database-connection with restricted file permissions. Preserve it with the SQL volume. A separate protected .secrets/database-migration file holds the local administrator connection only during setup and is removed in finally. Each is mounted only into its intended service. Ordinary build/test containers receive neither; database-check alone receives the local SQL administrator password to create and remove a uniquely named disposable database and login.

start-dev.ps1 enables compose.database.yaml when the runtime connection file exists. For a manual start:

```powershell
docker compose -f compose.yaml -f compose.database.yaml up -d api web
```

The API never migrates on startup. Configure ConnectionStrings:DaHub or Database:ConnectionStringFile outside Compose using protected configuration. The API rejects sa and optional/disabled encryption; TrustServerCertificate is permitted only in Development for the local SQL self-signed certificate. Deployment requires verified SQL certificates. The default development collation is SQL_Latin1_General_CP1_CI_AS.

SQL readiness at https://localhost:5081/api/health/ready (also through the frontend proxy) returns 200 only when connection and migration-history checks pass, otherwise 503 with safe ProblemDetails. Liveness remains independent at /api/health/live. The [API foundation](api-foundation.md) adds aggregate SQL/AD/Entra transport health at /health and /api/health. Live directory permissions, Okta/MFA and worker integration acceptance remain separate work.

## Migrations and rollback

The checked-in C# migrations and model snapshot are the schema source of truth. Apply all outstanding migrations before deploying the API, including `InvalidatePermissionKeyChanges`, which corrects permission-name invalidation for trailing-space edits without changing the EF model. To scaffold a later migration using the deliberate source-write override:

```powershell
docker compose -f compose.yaml -f .docker/format.compose.yaml run --rm --no-deps backend-check sh -c 'dotnet restore src/DaHub.Infrastructure && dotnet tool restore && dotnet ef migrations add MigrationName --project src/DaHub.Infrastructure --output-dir Persistence/Migrations'
```

Use dotnet ef migrations script --idempotent --project src/DaHub.Infrastructure in the tooling container for reviewable forward SQL. Use dotnet ef migrations script CompleteFirstReleaseSchema InitialLookups --project src/DaHub.Infrastructure for SQL that rolls back the 27-table increment. Both commands work offline through the credential-free design-time factory. Database updates require the explicit migration connection file.

Down removes runtime-role memberships and the role, then drops the 27 new tables in reverse dependency order. It retains the four original lookup tables and their data, including inserted site seeds, because existing sites may predate this migration and must not be deleted on a partial rollback. Upgrade again recreates the role; rerun setup to restore runtime membership. Rollback to zero also drops the original four tables. Test rollback only in disposable databases: it destroys the removed tables and their data.

The SQL integration test upgrades a populated lookup baseline, validates every table/column, primary key, specified index, trusted check and FK, exercises invalid worker/status/group/JSON/revision/date/identity/account/primary-assignment inputs, verifies the full access seed matrix, and tests actual runtime grants on all tables. It proves append-only insert/read and denied update/delete, read-only migration history, API readiness, partial rollback/data preservation, full rollback and reapplication. Ordinary verification skips this real-SQL test; test-dev-database.ps1 and security CI execute it. Test databases and logins use unique DaHub_Test_* / dahub_test_* names and are removed in finally.

## Remaining service work

Schema constraints do not implement authorization, OIDC/session validation, authorization-version invalidation, delegation-overlap transactions, HR verification, revision creation/supersession transactions, request completion rules, immutable audit-event generation, job claims/idempotency, provisioning, notifications, helpdesk integration or retention policy enforcement. Those services must enforce the application rules in the requirements before employee workflows are enabled. No production database or external employee account is changed by this work.

The SQL provider dependency graph initially selected vulnerable System.Security.Cryptography.Pkcs 9.0.11. A direct 10.0.14 reference keeps the existing NuGet audit gate passing without advisory suppression. See [Microsoft migration guidance](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying) and the [Microsoft advisory](https://github.com/advisories/GHSA-846f-f764-76mx).

## Validation — November 7, 2026

The real SQL Server test passed against all 31 tables and 300 columns, including the complete seed matrix, filtered primary assignments, exact-case identity keys, account ownership, revision/JSON/date/status constraints, runtime permissions, partial rollback with data preservation, full rollback and reapplication. The normal verification script passed backend/frontend builds, seven unit cases, two API integration tests, one frontend component test and formatting. The separate SQL test is deliberately skipped in the ordinary test run and executed by test-dev-database.ps1.

Local setup applied CompleteFirstReleaseSchema successfully, then ran again with no migrations pending. The runtime credential was preserved and the temporary administrator migration file was removed. The source secret scan found no leaks. Hosted CI evidence is available on the current pull request; local checks alone do not establish a hosted result.
