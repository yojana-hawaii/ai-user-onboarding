# DaHub development workspace

Angular frontend, ASP.NET Core API, domain/infrastructure libraries and tests. All 31 first-release schema tables, Okta sign-in/server-side sessions, local permission policies, the API foundation and the web application shell are implemented. Employee workflows and live deployment/provider acceptance remain subsequent work.

See [Okta setup and session security](docs/authentication.md) to configure the opt-in sign-in integration, enroll verified identities and validate MFA in your tenant. The server-side session store supports one API instance and invalidates sessions on restart; live tenant validation remains required.

## Prerequisites

Docker Desktop running Linux containers, Git and **PowerShell 7**. No workstation .NET SDK, Node.js or npm installation is required. Production remains Windows Server 2022 / SQL Server 2022; Windows-specific integrations need separate validation.

## First setup and daily startup

```powershell
./scripts/setup-dev-https.ps1 -Trust
./scripts/start-dev.ps1
docker compose logs -f api web
```

Certificate setup is idempotent for this user's managed DaHub development certificate. It refuses to replace unrelated/ACME certificates. Startup checks trust and requires at least 14 days before expiry, then builds the pinned tool images and starts the servers.

- Frontend: https://localhost:4200
- API liveness: https://localhost:5081/api/health/live
- Proxied liveness: https://localhost:4200/api/health/live

Both listeners are HTTPS-only and published on loopback. The proxy verifies the API certificate and hostname. Liveness confirms only that the API process responds; it does not establish database, directory or Okta readiness.

```powershell
docker compose restart api     # Rebuild/restart after backend edits
./scripts/verify.ps1           # Builds, tests and formatting checks
./scripts/verify-security.ps1  # Secret, dependency and development-image scans
./scripts/format.ps1           # Explicitly allow source writes for formatting
docker compose down           # Stop without deleting caches
```

Angular polls source files for live reload. The frontend lockfile is included with the scaffold; default starts use npm ci and never generate a replacement lockfile.

## Local SQL Server

The SQL development image applies APT/Brotli security fixes and removes unused R/Python extensibility helpers containing vulnerable Go code. External scripts are unsupported and must remain disabled. SQL Server requires TLS 1.2 for every connection. See the [SQL validation record](docs/development-security.md#sql-server-validation--november-7-2026) for the original findings and remediation evidence.

With Docker Desktop running Linux containers, run this once from PowerShell 7:

```powershell
./scripts/setup-dev-sql.ps1
```

This builds and starts the patched development image based on pinned Microsoft SQL Server 2022 Developer (licensed for development/testing), waits for a successful SQL login, and creates an empty `DaHub` database if needed. Repeating the command preserves the database and password. After setup, `start-dev.ps1` also starts SQL Server.

Connect from a SQL client using server `localhost,1433`, SQL authentication, login `sa`, and the generated `MSSQL_SA_PASSWORD` in `.env.sql`. Use encryption with **Trust server certificate** for this local container's self-signed certificate. From another service on the Compose network, the server address is `sql,1433`. The password file is excluded from Git and restricted to the current Windows user and SYSTEM. Keep it with the database volume: replacing the file does not reset an existing database login's password. The `sa` account is for local administration; run ./scripts/setup-dev-database.ps1 to apply the first-release schema migrations and provision a separate restricted API identity. See [database foundation](docs/database-foundation.md) for setup, readiness, rollback and remaining service work.

```powershell
./scripts/test-dev-sql-security.ps1 # Check required TLS and SQL/container restrictions
docker compose ps sql
docker compose logs --tail 50 sql
docker compose stop sql
./scripts/setup-dev-sql.ps1    # Start again and verify the database
```

SQL Server binds only to `127.0.0.1:1433` and stores its files in the `dahub-dev_sql_data` Docker volume. Container stops/recreation and `docker compose down` preserve data. **`docker compose down -v` deletes the database volume**, along with the other project volumes. SQL Server has a 4 GB container limit and a 2 GB engine memory target; leave memory for the API/frontend and Docker itself.

## Container boundaries

The running API and frontend use non-root users, read-only root filesystems, dropped Linux capabilities and no-new-privileges. Source is mounted read-only from specific project paths. Git metadata, root .env files and unrelated workspace folders are not mounted into these services. Build outputs and caches use writable Docker volumes; /tmp and .NET's per-user runtime-lock directory use temporary filesystems. CPU, memory and PID limits bound development processes.

Only api/web receive the certificate/key as read-only Compose secrets. Check services do not receive them. Compose file-based secrets are local file mounts, not an encrypted secret manager; Windows ACLs protect the host key directory. Do not add production credentials to this development environment.

The formatter uses an explicit Compose override to grant writes to the source directories it formats. Normal startup, builds, tests and security scans retain read-only source access. Dependency manifest/lockfile updates also require an intentional source-write override; review their diff before committing.

## Certificate maintenance

```powershell
./scripts/check-dev-https.ps1   # Trust, key pairing, validity and 14-day renewal window
./scripts/renew-dev-https.ps1   # Stop running app services, rotate, remove old trust/key, restart
./scripts/remove-dev-https.ps1  # Stop app and remove its exact managed certificate/key
./scripts/test-dev-https.ps1    # Windows-only isolated ownership/rollback/removal tests
```

The certificate lasts 90 days. Its PEM files are ignored by Git in .certs, with access limited to the current Windows user and SYSTEM. Renewal validates exact certificate ownership, stages replacements, preserves the old pair if replacement fails, and removes the previous certificate from CurrentUser Root/My together with its stored private key. A pending cleanup record allows retry after interrupted store cleanup. Unrelated or ACME certificates are not removed or renewed by these local scripts.

The servers accept standard fullchain.pem/privkey.pem output from ACME clients such as Certbot. No public domain is owned, so public issuance and automated ACME renewal are not configured. For a future owned domain, supply its certificates, set DAHUB_TLS_SERVER_NAME in a local .env, configure host filtering/Angular allowed hosts for that hostname, and add a monitored renewal deployment hook that restarts api/web. Use a production frontend host rather than ng serve for deployment.

## Security hardening actions

The following development-environment controls have been applied:

| Area | Hardening actions | Configuration / commands |
| --- | --- | --- |
| Container privileges | Run API, frontend and checks as non-root users; use read-only root filesystems, drop all Linux capabilities, prevent privilege escalation, and limit CPU, memory and process counts. | [compose.yaml](compose.yaml), [.docker/](.docker/) |
| Source and secret isolation | Mount specific application source paths read-only; exclude root .env files and Git metadata from application containers. Give only the running API/frontend their read-only certificate/key mounts. Keep build outputs and caches in writable volumes. | [Container boundaries](#container-boundaries), [.gitignore](.gitignore) |
| HTTPS and certificates | Bind HTTPS listeners to loopback; verify the API certificate and hostname in the proxy. Protect host key files with Windows ACLs. Check certificate expiry, rotate managed certificates, roll back failed replacements, and remove old trust entries and stored keys. | [Certificate maintenance](#certificate-maintenance), [proxy.conf.cjs](src/dahub-web/proxy.conf.cjs) |
| Supply chain | Pin checkout by commit and base/scanner images by digest. Apply the Ubuntu Brotli/APT fixes and patch npm's bundled Undici dependency. Configure Dependabot for npm, NuGet, Dockerfiles, Compose and Actions. | [.docker/backend/Dockerfile](.docker/backend/Dockerfile), [.docker/frontend/Dockerfile](.docker/frontend/Dockerfile), [dependabot.yml](.github/dependabot.yml) |
| Security gates | Scan source and fetched Git history with redacted findings; audit transitive NuGet dependencies and the frontend lockfile; scan actual built backend/frontend/SQL/E2E images. Fail on NuGet advisories, npm HIGH/CRITICAL findings, image HIGH/CRITICAL findings, or scanner errors. No blanket vulnerability exceptions are configured. | [verify-security.ps1](scripts/verify-security.ps1), [compose.security.yaml](compose.security.yaml), [.gitleaks.toml](.gitleaks.toml) |
| Repository protections | Enable secret scanning, push protection and Dependabot security updates. Use read-only workflow tokens and disable workflow PR approvals. Protect main with required PRs, current build/security checks, resolved conversations and linear history; include admins and disable force pushes/deletion. | [configure-github-security.ps1](scripts/configure-github-security.ps1) |

Run the checks from the workspace root:

```powershell
./scripts/verify.ps1                   # Build, tests and formatting
./scripts/verify-security.ps1          # Secrets, dependencies and built-image vulnerabilities
./scripts/check-dev-https.ps1          # Certificate trust, key pairing and expiry
./scripts/test-dev-https.ps1           # Windows-only certificate ownership and recovery tests
./scripts/configure-github-security.ps1 # Inspect GitHub settings without changing them
```

Repository administrators can explicitly apply the safeguards with `./scripts/configure-github-security.ps1 -Apply`. The script preserves existing branch protection rather than overwriting a custom policy. Credentials stay in process memory and are not written to files or output.

The first image scan downloads a vulnerability database into a dedicated Docker cache. Temporary image archives require local disk space and are removed after scanning. Scanners receive no Docker socket or credentials. Revisit the temporary Ubuntu/npm package patches when refreshing upstream images; remove them only after the updated images pass security checks.

Local verification on **November 7, 2026** passed backend/frontend builds, one API integration test, one component test, formatting, secret scans and dependency audits. Both development images reported **zero HIGH/CRITICAL findings** after patching the reported vulnerabilities. HTTPS checks passed with certificate validation enabled. This records that verification date, not a guarantee against future advisories.

### Okta/session security verification — November 9, 2026

The complete `./scripts/verify-security.ps1` suite passed locally, including working-tree/fetched-history secret scans (11 commits), transitive NuGet auditing and scans of all four built development images:

| Image | HIGH/CRITICAL findings |
| --- | --- |
| `dahub-backend-dev:local` | 0 |
| `dahub-frontend-dev:local` | 0 |
| `dahub-sql-dev:local` | 0 |
| `dahub-e2e-dev:local` | 0 |

The initial npm audit reported 10 moderate lint-tool findings caused by [CVE-2026-118764 / GHSA-g3hv-75wg-m249](https://github.com/advisories/GHSA-g3hv-75wg-m249). A scoped npm override replaces affected `minimatch` 10.x with patched 11.0.1, and the lockfile was regenerated. A fresh clean install and full npm audit now report **zero vulnerabilities**. No vulnerability suppression, threshold reduction or ESLint downgrade was used. Revisit the override when the upstream lint dependencies adopt the corrected version.

The Okta implementation passed the full functional gate: 42 backend tests, 8 frontend tests, builds, lint, formatting and coverage thresholds. API line coverage is 94.21%. After the dependency fix, frontend build/lint/formatting, all 8 frontend tests and all 6 Chromium/Firefox/WebKit smoke tests passed again. See [the security record](docs/development-security.md#okta-session-and-dependency-verification--november-9-2026) for scope and evidence.

These are local development results. Live Okta/MFA policy and logout validation, production controls, and hosted CI for the current authentication changes remain outstanding. Sessions support one API instance and expire on restart. The credential-free functional gate skips the real-SQL migration test; separate SQL migration/runtime-security probes were not rerun in this security-only follow-up. Scanning the SQL image does not replace those probes.

GitHub settings were applied and verified on November 2, 2026. At that verification, only one collaborator had write access, so independent approval was not required. Add an eligible reviewer and raise the approval requirement when available. The CI file defines PR/push checks plus a weekly advisory scan. The weekly schedule becomes active after the workflow reaches the default branch. Use the pull request checks and GitHub Actions history to confirm hosted execution; local verification alone does not establish a hosted CI result.

Origin: https://github.com/yojana-hawaii/ai-user-onboarding.git. See [the security change record](docs/development-security.md) for detailed evidence and remaining release controls. Okta sign-in, local session revocation, CSRF protection and login auditing are implemented; live Okta/MFA verification, application permission policies, broader audit logging and production secret management remain required before employee workflows or privileged integrations are enabled.

Use main for the reviewed baseline, develop for integration and feature/<task-id>-<description> for work. Keep the local requirements/ documents excluded as currently configured. See CONTRIBUTING.md.

## Project layout and remaining work

- [AGENTS.md](AGENTS.md): repository agent instructions, security boundaries and verification handoff.
- src/DaHub.API: HTTP entry point, health endpoints, Okta OIDC and server-side session authentication.
- src/DaHub.Core and src/DaHub.Infrastructure: first-release entities, SQL Server context and versioned EF Core migrations.
- src/dahub-web: Angular application and HTTPS development proxy.
- tests/DaHub.Tests.Unit: unit-test structure; domain tests begin with implementation.
- tests/DaHub.Tests.Integration: in-memory API host and liveness test.
- scripts/: all environment PowerShell entry points.
- .docker/: pinned development tool images and explicit formatting override.

Phase 1 requires server-side Okta OIDC with MFA, locally owned authorization, immediate revocation checks, secure session cookies and CSRF protection before employee workflows. Authentication now provides secure sessions and next-request account/link/version checks. MFA must be enforced and verified in the configured Okta tenant. Local permission enforcement is implemented in WBS 1.4; workflow audit events beyond login and Directory/M365 service identities remain pending. Migrations are applied only to the local development database. No employee data, production credentials or production provisioning action is part of this scaffold.

## API documentation and quality checks

Swagger UI is available at https://localhost:5081/swagger and its OpenAPI document at https://localhost:5081/openapi/v1.json when the API runs in Development. Documentation is disabled in Staging and Production. The document describes process liveness and SQL readiness, including readiness failure responses.

Run ./scripts/verify.ps1 for the complete local/CI gate: Release build and .NET analyzers, backend tests with merged coverage, dotnet format, Angular ESLint (including inline-template accessibility rules), production frontend build, Vitest coverage, Prettier and Chromium, Firefox and WebKit browser E2E tests. Docker Desktop is the only runtime prerequisite.

Run ./scripts/test-e2e.ps1 to run just browser tests. It builds a pinned Playwright tools image with matching Chromium, Firefox and WebKit binaries and starts a separate, unpublished HTTP API on a dedicated Compose network. Playwright starts Angular inside the runner and checks the home page, unknown-route redirect, and a browser fetch through the Angular proxy to the real API. It does not use the development TLS private key, SQL credentials, employee data, or production services. Daily development continues to use HTTPS. SQL integration tests remain in ./scripts/test-dev-database.ps1.

Coverage fails verification below 80%:
- Backend: at least 80% line coverage in every DaHub application assembly, using merged unit and integration results. The minimum assembly result controls the gate. EF-generated migrations, SDK-generated sources and compiler-generated automatic properties are excluded; handwritten configuration, database validation and API code remain included. Migration behavior is checked separately against SQL Server.
- Frontend: statements, branches, functions and lines, each at 80%, including untested src/app TypeScript files. Test files and the main.ts bootstrap entry point are outside this application-code scope.

Reports persist in Docker volumes: backend JSON/Cobertura under /artifacts/coverage in backend_artifacts, frontend HTML/LCOV/JSON under /workspace/src/dahub-web/coverage in check_coverage, and Playwright HTML/traces/screenshots under /reports in e2e_reports. Compose prefixes volume names with dahub-dev_. Repeated backend runs discard old hits before merging, so stale coverage cannot satisfy the gate. Browser reports retain failure traces and screenshots; the runner exits nonzero on failure and the script stops and removes its E2E API container.

Tool references: [Angular coverage](https://angular.dev/guide/testing/code-coverage), [Coverlet MSBuild](https://github.com/coverlet-coverage/coverlet/blob/master/Documentation/MSBuildIntegration.md), [ASP.NET OpenAPI](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/aspnetcore-openapi?view=aspnetcore-10.0), [Playwright containers](https://playwright.dev/docs/docker).

Browser coverage uses desktop Chrome, Firefox and Safari device profiles. WebKit provides Safari engine coverage; native Safari on macOS remains a release validation step. Linux Playwright execution does not establish native Safari compatibility. Keep the Playwright npm package and Docker image versions aligned when upgrading. See [Playwright browser support](https://playwright.dev/docs/browsers).

For application behavior, follow TDD: write a test that fails for the intended behavior, implement the smallest change that passes, refactor while tests remain green, run the relevant checks, and commit. Backend tests use xUnit; component tests use Angular TestBed/Vitest; browser workflows use Playwright. See CONTRIBUTING.md for security and synthetic-data conventions.

### PR preparation verification — November 10, 2026

Both complete local gates were repeated after the dependency fix: 42 backend tests, 8 frontend tests and 6 browser smoke tests passed, with all build/lint/format/coverage checks. The complete security suite passed: no source/history secrets (15 fetched commits), no NuGet advisories, zero npm vulnerabilities and zero HIGH/CRITICAL findings in each of the four images. Root `AGENTS.md` is included in secret scanning; other `requirements/` files remain ignored. See [the dated verification record](docs/development-security.md#pr-preparation-verification--november-10-2026). Live Okta acceptance and hosted verification remain separate.

## Local RBAC (Sprint 1, WBS 1.4)

Local active roles resolve to a distinct, additive permission set cached in the server-side session. API permission attributes/policies return 401/403 ProblemDetails; urgency requires both execution and urgent grants, delegation checks the manager's local employee link, and retention requires the separate IT permission. Angular guards check current session permissions and filter navigation. See [RBAC usage and deployment boundaries](docs/authentication.md#local-rbac--wbs-14).

The new `InvalidateAuthorization` migration transactionally increments affected users' authorization versions through SQL triggers, including direct SQL changes. Deploy the migration before the API; old sessions fail closed after authorization changes. Role administration/enrollment, employee workflow execution and its audit/recovery orchestration remain separate work. No persistent development/production database or external identity provider was changed by implementation; SQL verification uses a disposable database.

Local verification passed: 60 backend tests, 12 frontend tests, 6 browser smoke tests, coverage/build/lint/format gates, isolated SQL migration/runtime tests and SQL security checks. The complete security suite passed with no source/history secrets, no NuGet advisories, zero npm vulnerabilities and zero HIGH/CRITICAL findings in each of four images. See [the RBAC verification record](docs/development-security.md#sprint-1-wbs-14--local-rbac-november-10-2026) for scope and remaining workflow/release work. These are local verification results. [RBAC PR #23](https://github.com/yojana-hawaii/ai-user-onboarding/pull/23) tracks hosted checks separately for its current head.

RBAC review is stacked on authentication [PR #22](https://github.com/yojana-hawaii/ai-user-onboarding/pull/22), which depends on [PR #17](https://github.com/yojana-hawaii/ai-user-onboarding/pull/17). Retarget to develop after prerequisites merge. The WBS 1.5 follow-up implements the remaining WBS 1.4.2.2 permission refresh criterion: active sessions reload changed local permissions without a provider login, recheck the version after resolution, and renew only their server-side ticket. Inactive users/revoked identity links and changes racing the refresh fail closed. Unchanged requests do not repeat the permission join. This working-tree follow-up is separate from the earlier PR/CI evidence.

## API foundation (Sprint 1, WBS 1.5)

The API foundation adds consistent ProblemDetails, correlation IDs, safe structured JSON request logs to console and rolling files, SQL/AD/Entra health reporting, user-secret/environment configuration and HTTPS/HSTS/TLS controls. See [API foundation setup and deployment checklist](docs/api-foundation.md) for configuration, exact environment variable names, protected secret sources and verification boundaries. `Logging__Directory` selects a restricted log directory; the local API uses an ephemeral `/logs` mount. `/health` and `/api/health` aggregate dependency health; local directory checks are explicitly mocked. Production must configure Live probes and persistent operational logging.

Production configuration binds HTTP 80 for 301 redirects and HTTPS 443 with a separately provisioned certificate. No production host, IIS binding, real directory, Okta tenant or persistent database is configured by this implementation. Actual Windows/IIS deployment, live provider outcomes and native Safari remain release acceptance work. New local verification is recorded separately in [development security](docs/development-security.md).

The final local functional gate passed 100 credential-free backend tests, 12 frontend tests and 6 browser tests; the disposable SQL test and SQL-security checks passed separately. The full security gate passed with no source/history secrets or NuGet advisories, zero npm vulnerabilities and zero HIGH/CRITICAL findings in all four images. Notion WBS 1.4 is Completed; 1.5 retains the actual host/certificate/80→443 deployment acceptance under 1.5.5.1. These local results were collected on `feature/1.5-api-foundation` before publication. The requested PR review base is `feature/1.4-rbac`; hosted results are separate evidence. Merge prerequisites #17, #22 and #23 in order before retargeting this follow-up to develop. No merge is authorized by this handoff.

[Draft PR #24](https://github.com/yojana-hawaii/ai-user-onboarding/pull/24) publishes implementation commit `155c784343297b9c5e901b7e4a716cb844e14c48` against `feature/1.4-rbac`. Both local gates were repeated successfully before publication. Inspect [the current PR head's hosted checks](https://github.com/yojana-hawaii/ai-user-onboarding/pull/24/checks) for build/test/format and security results; documentation follow-ups can advance the head. The review base is unprotected, so passing checks do not establish enforced merge blocking or execution on main.

The PR #24 security review adds exact application-level issuer/subject comparisons to session refresh and privileged authorization checks: SQL Server equality can ignore trailing spaces even under binary collation. Disposable SQL regressions cover both padded claims and changed stored identity keys. Apply the additional `InvalidatePermissionKeyChanges` migration before deploying the API: it invalidates authorization when a permission key changes only by trailing spaces. Non-400 `BadHttpRequestException` responses now use the correct HTTP reason phrase in ProblemDetails. See the [review verification record](docs/development-security.md#pr-24-security-and-acceptance-review--november-10-2026). At review, PRs #17, #22, #23 and #24 were closed and unmerged; the user authorized reopening #24 as a draft. Its prerequisite changes still need integration before release. Deployment/live-provider acceptance remains open.

Both full local gates passed after the review fixes: 8 unit, 93 credential-free integration/protocol, 12 frontend and 6 Chromium/Firefox/WebKit tests; the disposable SQL test and SQL security checks passed separately. Secret/dependency gates passed and all four images reported zero HIGH/CRITICAL findings. Current-head hosted evidence is recorded separately in PR #24 and the related Notion tasks.

## Web application shell (Sprint 1, WBS 1.6)

The Angular shell keeps the header, local-permission navigation and CSRF-protected sign-out available while lazy-loaded pages change. Requests, Provisioning and Audit remain workflow placeholders. Unknown URLs retain their address and show Page not found; denied routes show Access denied. Protected navigation checks the current server session, including query-only navigation, and updates the menu from the returned local grants. An older overlapping session response cannot restore obsolete menu grants. The server remains the authorization boundary.

Navigation shows an accessible loading skeleton and clears it when navigation succeeds, redirects, cancels or fails. Desktop and mobile layouts include a skip link, visible keyboard focus and active-page navigation. The six business-role permission scenarios, plus unknown and UrgentOperator-only grants, run with Angular Testing Library 19.6.0 and DOM Testing Library 10.4.2; both test dependencies are pinned in the lockfile. No dashboard, employee workflow or administration feature is added by the shell.

The existing interceptor keeps OAuth tokens out of browser storage, retrieves antiforgery proof for application mutations and preserves the local path/query for expired protected requests. Browser UI tests use synthetic session responses and a simulated sign-in return; they do not validate live Okta MFA, logout or reauthentication. WBS 1.6.4.2 and its parents retain the live expired-session → Okta → original-route acceptance. Native Safari and deployed Windows HTTPS acceptance also remain open. See [the WBS 1.6 verification record](docs/development-security.md#web-application-shell--wbs-16--november-10-2026).

Final local `scripts/verify.ps1` passed: 8 backend unit, 93 credential-free integration/protocol, 25 frontend and 30 Chromium/Firefox/WebKit tests. Frontend coverage is 100% lines/statements/functions and 97.05% branches. The complete security suite passed during implementation; the final regenerated lockfile audit also reports zero vulnerabilities. See the verification record for the image/source scan scope and remaining live acceptance. The SQL-only test was skipped and not rerun for this frontend-only change. The ignored local WBS and all 17 [Notion WBS 1.6 records](https://app.notion.com/p/412ee4cf5eb481cf840ce9ca880679a8) now record 11 of 12 leaves complete, preserving acceptance criteria, estimates and historical notes. Live-provider return-route acceptance remains open. The November 11 documentation/tracker follow-up does not rerun the functional gate or publish a commit/PR; implementation remains uncommitted on `feature/1.6-ui-shell`, based on `47490d57815152f959932b1f14311cdbc39cc427`.

The **November 11 security rerun failed**, superseding the earlier clean image result. Source/history secret scans and NuGet/npm audits passed, but the refreshed scanner reports three HIGH findings in backend ASP.NET Core runtime 10.0.14: CVE-2026-114146, CVE-2026-114147 and CVE-2026-114149. The scanner lists 10.0.15 as patched. Independent scans of the remaining frontend, SQL and E2E images each reported zero HIGH/CRITICAL findings. Runtime/image remediation and a fresh security gate are required before publication; no suppression, threshold change or runtime update was made in this testing/documentation follow-up. See the [November 11 security follow-up](docs/development-security.md#wbs-16-security-rerun-and-tracker-sync--november-11-2026) for the complete scan scope.

### WBS 1.6 PR verification — November 14, 2026

The November 11 runtime findings above are resolved by refreshing the pinned backend SDK image to **10.0.404 / ASP.NET Core 10.0.15**. The complete `scripts/verify-security.ps1` rerun exited 0: source/history scans found no leaks (21 local commits), transitive NuGet auditing found no advisories, npm reported zero vulnerabilities, and backend/frontend/SQL/E2E images each reported zero HIGH/CRITICAL findings. Existing container restrictions, APT fixes, scanner isolation, severity gates and coverage thresholds are preserved; no advisory suppression was added. See the [November 14 verification record](docs/development-security.md#wbs-16-pr-verification--november-14-2026).

The fresh full `scripts/verify.ps1` passed (8 unit, 93 credential-free integration/protocol, 25 frontend and 30 Chromium/Firefox/WebKit tests). Its backend stage began on the previous SDK; after the image refresh the entire backend build/analyzer/test/coverage/format command was repeated under SDK 10.0.404 and passed. E2E ran against the refreshed backend image. Backend line coverage remains API 96.17%, Infrastructure 96.76%, Core 100%; frontend statements/branches/functions/lines remain 100%/97.05%/100%/100%. The SQL-only test was skipped; real SQL/runtime-security probes were not rerun. No schema, migration, tenant or deployment action was executed.

PR preparation targets `develop`: prerequisites #17, #22, #23 and #24 are verified merged. `develop` was verified unprotected; local success does not establish hosted checks, enforced merge blocking or execution on `main`. The Notion WBS 1.6 records retain 11/12 leaves complete, with actual expired-session → live Okta reauthentication → original-route acceptance open. Live MFA/logout/outage, native Safari and Windows deployment acceptance remain separate work. Earlier uncommitted/failed-gate statements describe their dated handoffs and are superseded by this verification.

Published implementation commit: `ed1b989cf82d93642dac6107fbe18a744499bc1c`; [draft PR #25](https://github.com/yojana-hawaii/ai-user-onboarding/pull/25) targets `develop`. The initial head has hosted build/test/format in progress and security queued; no hosted pass is claimed. Documentation follow-ups can advance the head: inspect [current-head checks](https://github.com/yojana-hawaii/ai-user-onboarding/pull/25/checks) separately. No merge or deployment was executed.
