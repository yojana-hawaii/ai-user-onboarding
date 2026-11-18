# DaHub development workspace

PR #52 merged into develop on November 18, 2026 as `e8e43e55d919e1707b040de05647d44ab6591599`; its distinct develop CI passed functional, security, browser and separate SQL checks. See [AGENTS.md section 28](AGENTS.md#28-pr-52-merge-and-develop-acceptance--november-18-2026) for exact revisions and evidence. Earlier PR #52 review/pending-merge notes are historical. Keep the reviewed picomatch patches and all gates intact; PR #25 remains separate and no main promotion is authorized.

The documentation-only publication handoff passed a fresh complete security gate: no source/history leaks across 93 local commits, no NuGet advisories, zero npm vulnerabilities, ten installed-copy security regressions passed and all four images zero HIGH/CRITICAL. Documentation references and whitespace checks passed. Application/browser/SQL suites were not rerun locally; final source and new PR-head hosted evidence are tracked separately. This authorizes no further merge or deployment.

Dependency integration completed November 17, 2026: all eleven dependency PRs, including the reviewed picomatch remediation in #51, are merged into develop. Exact-head PR CI and separate develop push CI passed; see [AGENTS.md section 26](AGENTS.md#26-dependency-integration-complete--november-17-2026) for revisions, evidence and maintenance guidance. Earlier dependency preparation checkpoints are historical. Main remains unchanged and PR #25 remains separate; deployment, live-provider and release acceptance remain open.

November 18 follow-up: the new pre-PR image scan found a separate vulnerable picomatch bundled inside npm, despite the clean application audit. Frontend and browser tool images now apply the reviewed, integrity-checked patch to npm's nested copy, and the regression suite covers both installed copies. See [the tool-image security record](docs/development-security.md#npm-tool-image-picomatch-remediation--november-18-2026) for verification and the temporary-patch maintenance boundary. Publication requires fresh complete gates; no merge or main promotion is authorized by this follow-up.

The follow-up passed fresh full local functional and security gates: ten application/npm security regressions on both Node versions, 28 unit/173 credential-free integration/19 frontend/nine browser tests, preserved coverage/lint/format checks, no source/history leaks (91 local commits), no NuGet advisories, zero npm vulnerabilities and zero HIGH/CRITICAL findings in all four images. Local SQL runtime tests were not rerun for this tool-image change; exact published-head hosted checks are recorded separately in Notion. Existing deployment and release limitations remain open.

Angular frontend, ASP.NET Core API, domain/infrastructure libraries and tests. All 32 first-release schema tables, Okta sign-in/server-side sessions, local permission policies and the API foundation are implemented. Employee workflows and live deployment/provider acceptance remain subsequent work.

## Open-source goal and current readiness

Our goal is to publish DaHub as a reusable, organization-neutral open-source employee-lifecycle project. Keep organization catalogs in private deployment packages, load them through the validated seed upload, and use synthetic identities and examples in the shared source. Preserve the existing security, authorization, audit and verification gates.

The November 15, 2026 audit of main `2c693cd1f8656966e290b990def8ec410ba12d7a` confirmed that GitHub already exposes the repository publicly. That visibility is an observed state, not completion of open-source preparation; the audit did not change it. Current source is cleaned, but older commits/branches/retained PR refs still contain organization branding, location seeds and private project references. Some older PR bodies retain private workspace links, and historical author metadata includes a personal email address. Do not copy those values into new public documentation.

Before broader release, complete the open-source workstream: decide the authorized history/publication approach and exposure handling; sanitize historical and PR metadata surfaces; confirm ownership and the intended existing GPL v3 licensing; establish private vulnerability reporting in SECURITY.md; and repeat source/history/metadata/log checks on the final release revision. A clean snapshot does not erase earlier public copies. Track these as the separate Notion OS workstream; existing sprint scope and estimates are unchanged.

The audit detected no secrets in exact-main files, fetched history, PR/comment text or all 80 retained CI log archives. This is dated, pattern-based evidence, not proof of complete sanitization. Source publication does not establish production readiness: first-admin enrollment, operational catalog approval, live-provider validation, multi-instance sessions, native Safari and workflow/deployment acceptance remain open. See [the open-source release boundary](docs/open-source.md#migration-compatibility-and-release-boundary). These goal/tracker updates do not authorize visibility changes, history rewrites, remote cleanup, a new repository, deployment or tenant/provider actions.

Organization catalogs are loaded explicitly through the [organization seed upload](docs/open-source.md). Public migrations contain no organization locations; existing installations retain their data. Start with [the synthetic package](examples/organization-seeds.example.json) and keep operational packages in the Git-ignored `organization-data/` directory. Both `templates.manage` and `users.manage` are required to preview/apply. Public release still requires a decision and review of historical publication surfaces; this cleanup does not rewrite Git history or publish the repository.

Organization-seed local verification (November 14, 2026): full functional gate passed with 28 unit, 165 credential-free integration/protocol, 19 frontend and 9 three-engine browser tests, with coverage thresholds satisfied. Separate disposable SQL/runtime/API tests and SQL security checks passed. The full security gate passed with no source/history secrets, no NuGet advisories, zero npm vulnerabilities and zero HIGH/CRITICAL findings in all four images. See the [verification scope and remaining release work](docs/development-security.md#organization-neutral-source-and-seed-upload--november-14-2026). No persistent migration, publication or live-provider action occurred.

The November 14 preparation snapshot recorded uncommitted `feature/1.2.3.4-organization-seeds` changes and six reconciled Notion WBS records. That publication state is historical: PR #31 subsequently merged into develop, and approved exact-tree PR #33 promoted the verified tree to main after conflicted PR #32 was closed. PR and main checks passed; operational acceptance remains open. See the [preparation record](docs/development-security.md#organization-seed-pre-pr-preparation--november-14-2026) and the dated promotion completion notes below.

See [Okta setup and session security](docs/authentication.md) to configure the opt-in sign-in integration, enroll verified identities and validate MFA in your tenant. The server-side session store supports one API instance and invalidates sessions on restart; live tenant validation remains required.

WBS 1.2.2.3 now enforces verified-worker profile completeness and required primary assignments on tracked persistence writes, including later edits/removals. PendingHR imports may remain incomplete; lifecycle request writes require HR verification. A null manager with the HR attestation records the confirmed no-manager case. See the [profile guard and transaction contract](docs/database-foundation.md#verified-worker-profile-guard--wbs-1223). Authorized HR endpoints and verification auditing remain subsequent workflow work.

November 14 profile validation: the full local functional gate, separate disposable SQL/SQL-security checks and redacted working-tree secret scan passed. Results: 28 unit, 146 credential-free integration, 12 frontend and 6 Chromium/Firefox/WebKit tests; coverage thresholds passed. The subsequent full pre-PR security gate passed: no source/history leaks, no NuGet advisories, zero npm vulnerabilities and zero HIGH/CRITICAL findings in all four images. See [verification scope](docs/development-security.md#wbs-1223--verified-worker-profiles--november-14-2026); no schema migration or external action was executed.

## Prerequisites

Docker Desktop running Linux containers, Git and **PowerShell 7**. No workstation .NET SDK, Node.js or npm installation is required. Production remains Windows Server 2022 / SQL Server 2022; Windows-specific integrations need separate validation.

The backend verification gate restores the pinned local `dotnet-ef` tool, discovers migrations offline, checks for pending model changes and generates an idempotent SQL script in the container artifact volume. The dotnet-ef 11.0.0 CLI was verified with the existing EF Core 10.0.14 libraries and .NET 10 SDK, including apply/rollback/reapply in a disposable database. See [the tooling verification scope](docs/development-security.md#ef-cli-verification--november-16-2026).

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
./scripts/configure-github-security.ps1 -Owner YOUR_OWNER -Repository YOUR_REPOSITORY # Inspect only
```

Repository administrators can explicitly apply the safeguards with `./scripts/configure-github-security.ps1 -Owner YOUR_OWNER -Repository YOUR_REPOSITORY -Apply`. The script preserves existing branch protection rather than overwriting a custom policy. Credentials stay in process memory and are not written to files or output.

Dependency PR #41 retains .NET 10 LTS on the explicit `10.0-noble` tag and the already-current servicing digest. Its proposed Dependabot configuration targets `develop` for scheduled version updates and excludes automatic SDK major upgrades. These settings take effect after merge/promotion of the configuration; security updates continue to target the default branch. No repository default-branch or protection change is included. See [the LTS and dependency-routing decision](docs/development-security.md#net-10-lts-and-dependency-routing--november-16-2026).

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

Origin: [private repository reference] See [the security change record](docs/development-security.md) for detailed evidence and remaining release controls. Okta sign-in, local session revocation, CSRF protection and login auditing are implemented; live Okta/MFA verification, application permission policies, broader audit logging and production secret management remain required before employee workflows or privileged integrations are enabled.

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

Dependency PR #45 pairs `@playwright/test` 1.65.1 with the digest-pinned `v1.65.1-resolute` browser image. The Node 24 browser runtime, temporary npm/APT fixes and container restrictions remain unchanged. See [the browser update verification](docs/development-security.md#playwright-packageimage-alignment--november-16-2026).

For application behavior, follow TDD: write a test that fails for the intended behavior, implement the smallest change that passes, refactor while tests remain green, run the relevant checks, and commit. Backend tests use xUnit; component tests use Angular TestBed/Vitest; browser workflows use Playwright. See CONTRIBUTING.md for security and synthetic-data conventions.

### PR preparation verification — November 10, 2026

Both complete local gates were repeated after the dependency fix: 42 backend tests, 8 frontend tests and 6 browser smoke tests passed, with all build/lint/format/coverage checks. The complete security suite passed: no source/history secrets (15 fetched commits), no NuGet advisories, zero npm vulnerabilities and zero HIGH/CRITICAL findings in each of the four images. Root `AGENTS.md` is included in secret scanning; other `requirements/` files remain ignored. See [the dated verification record](docs/development-security.md#pr-preparation-verification--november-10-2026). Live Okta acceptance and hosted verification remain separate.

## Local RBAC (Sprint 1, WBS 1.4)

Local active roles resolve to a distinct, additive permission set cached in the server-side session. API permission attributes/policies return 401/403 ProblemDetails; urgency requires both execution and urgent grants, delegation checks the manager's local employee link, and retention requires the separate IT permission. Angular guards check current session permissions and filter navigation. See [RBAC usage and deployment boundaries](docs/authentication.md#local-rbac--wbs-14).

The new `InvalidateAuthorization` migration transactionally increments affected users' authorization versions through SQL triggers, including direct SQL changes. Deploy the migration before the API; old sessions fail closed after authorization changes. Role administration/enrollment, employee workflow execution and its audit/recovery orchestration remain separate work. No persistent development/production database or external identity provider was changed by implementation; SQL verification uses a disposable database.

Local verification passed: 60 backend tests, 12 frontend tests, 6 browser smoke tests, coverage/build/lint/format gates, isolated SQL migration/runtime tests and SQL security checks. The complete security suite passed with no source/history secrets, no NuGet advisories, zero npm vulnerabilities and zero HIGH/CRITICAL findings in each of four images. See [the RBAC verification record](docs/development-security.md#sprint-1-wbs-14--local-rbac-november-10-2026) for scope and remaining workflow/release work. These are local verification results. RBAC PR #23 (private project evidence) tracks hosted checks separately for its current head.

RBAC review is stacked on authentication PR #22 (private project evidence), which depends on PR #17 (private project evidence). Retarget to develop after prerequisites merge. The WBS 1.5 follow-up implements the remaining WBS 1.4.2.2 permission refresh criterion: active sessions reload changed local permissions without a provider login, recheck the version after resolution, and renew only their server-side ticket. Inactive users/revoked identity links and changes racing the refresh fail closed. Unchanged requests do not repeat the permission join. This working-tree follow-up is separate from the earlier PR/CI evidence.

## API foundation (Sprint 1, WBS 1.5)

The API foundation adds consistent ProblemDetails, correlation IDs, safe structured JSON request logs to console and rolling files, SQL/AD/Entra health reporting, user-secret/environment configuration and HTTPS/HSTS/TLS controls. See [API foundation setup and deployment checklist](docs/api-foundation.md) for configuration, exact environment variable names, protected secret sources and verification boundaries. `Logging__Directory` selects a restricted log directory; the local API uses an ephemeral `/logs` mount. `/health` and `/api/health` aggregate dependency health; local directory checks are explicitly mocked. Production must configure Live probes and persistent operational logging.

Production configuration binds HTTP 80 for 301 redirects and HTTPS 443 with a separately provisioned certificate. No production host, IIS binding, real directory, Okta tenant or persistent database is configured by this implementation. Actual Windows/IIS deployment, live provider outcomes and native Safari remain release acceptance work. New local verification is recorded separately in [development security](docs/development-security.md).

The final local functional gate passed 100 credential-free backend tests, 12 frontend tests and 6 browser tests; the disposable SQL test and SQL-security checks passed separately. The full security gate passed with no source/history secrets or NuGet advisories, zero npm vulnerabilities and zero HIGH/CRITICAL findings in all four images. Notion WBS 1.4 is Completed; 1.5 retains the actual host/certificate/80→443 deployment acceptance under 1.5.5.1. These local results were collected on `feature/1.5-api-foundation` before publication. The requested PR review base is `feature/1.4-rbac`; hosted results are separate evidence. Merge prerequisites #17, #22 and #23 in order before retargeting this follow-up to develop. No merge is authorized by this handoff.

Draft PR #24 (private project evidence) publishes implementation commit `155c784343297b9c5e901b7e4a716cb844e14c48` against `feature/1.4-rbac`. Both local gates were repeated successfully before publication. Inspect the current PR head's hosted checks (private project evidence) for build/test/format and security results; documentation follow-ups can advance the head. The review base is unprotected, so passing checks do not establish enforced merge blocking or execution on main.

The PR #24 security review adds exact application-level issuer/subject comparisons to session refresh and privileged authorization checks: SQL Server equality can ignore trailing spaces even under binary collation. Disposable SQL regressions cover both padded claims and changed stored identity keys. Apply the additional `InvalidatePermissionKeyChanges` migration before deploying the API: it invalidates authorization when a permission key changes only by trailing spaces. Non-400 `BadHttpRequestException` responses now use the correct HTTP reason phrase in ProblemDetails. See the [review verification record](docs/development-security.md#pr-24-security-and-acceptance-review--november-10-2026). At review, PRs #17, #22, #23 and #24 were closed and unmerged; the user authorized reopening #24 as a draft. Its prerequisite changes still need integration before release. Deployment/live-provider acceptance remains open.

Both full local gates passed after the review fixes: 8 unit, 93 credential-free integration/protocol, 12 frontend and 6 Chromium/Firefox/WebKit tests; the disposable SQL test and SQL security checks passed separately. Secret/dependency gates passed and all four images reported zero HIGH/CRITICAL findings. Current-head hosted evidence is recorded separately in PR #24 and the related Notion tasks.

## Picomatch remediation candidate — November 17, 2026

The follow-up explicitly authorizes a safe remediation and PR #51's develop-only merge after all checks pass. A scoped override replaces affected picomatch 4.x with the actual patched MIT fork `@ryotsu/picomatch` 4.0.7-a, pinned by lockfile SHA-512. Published runtime files match source commit `3ab5a7627d480259e3d9ca67fbc14c125d374f0e`; npm verified its registry signature and GitHub Actions provenance. The reviewed parser patch removes the redundant repeated-wildcard quantifier and fails closed on unsupported complex forms. This replaces vulnerable code; no advisory or gate is suppressed. See [the source review and verification scope](docs/development-security.md#picomatch-remediation-candidate--november-17-2026).

The vulnerable baseline timed out in the new isolated regression, while the patched distribution passed the same bounded attack checks and normal glob/API tests. The fork's full lint/test suite passed 2,004 cases in a non-root, read-only, network-disabled container. A fresh candidate npm audit reports zero vulnerabilities. `npm run security:picomatch` now runs during both full gates and before browser tests, covering Node 26 and the Node 24 browser runtime. Full aggregate gates and fresh final-head PR/develop CI remain required before completion. This is a candidate preparation record; final commit/merge/run evidence is kept in Notion. The earlier blocker below retains its dated scope. Main promotion remains prohibited and PR #25 stays separate.

The fork is a temporary, independently reviewed dependency boundary: keep its exact pin and integrity, and re-evaluate an official patched release when available. Complex wildcard extglobs can be treated literally and captured substrings may differ; the default safeguard must remain enabled. Adoption requires both the security regressions and the real Angular/lint/build/test/browser checks, not a clean audit alone. Existing license, image pins, isolation, coverage/severity gates and release limitations remain unchanged.

Fresh full local functional/security gates and separate real SQL/SQL-security checks passed on the remediation candidate: 28 unit, 173 credential-free integration, 19 frontend, 9 Chromium/Firefox/WebKit cases, and the five patched-code regressions on Node 26/24. The complete security gate passed with no source/history leaks or NuGet advisories, zero npm vulnerabilities and zero HIGH/CRITICAL findings in all four images; lower-severity findings are not claimed absent. Final exact-head PR and separate develop CI remain pending publication and are recorded in Notion afterward. The earlier blocker is superseded locally by this verified code remediation.

## Final dependency integration candidate — November 17, 2026

Preparation snapshot for final PR #51: ten dependency PRs (#48/#41/#45/#47/#50/#49/#42/#43/#44/#46) are merged into develop, and every distinct develop push run passed before the next integration. Verified base `b5f5fcdff66745fafa0096f46db10e532c89945e` passed [its separate CI run](https://github.com/yojana-hawaii/ai-user-onboarding/actions/runs/44133903717). PR #51 retains OIDC 10.0.15 alongside OpenAPI 10.0.15 and reconciles the final package/toolchain handoff. The complete candidate still requires fresh local gates, new exact-head PR checks and its distinct develop run; final commit/merge/CI evidence is recorded separately in Notion after publication, without another documentation commit loop.

The candidate retains .NET 10 LTS/Noble and EF Core 10.0.14, with dotnet-ef 11.0.0, MVC Testing 10.0.15, Coverlet 10.1.1, Angular CLI/build 22.2.7, ESLint 10.15.0, Prettier 3.10.1 and aligned Playwright package/browser image 1.65.1. See [the current toolchain](docs/development-toolchain.md) and [verification scope](docs/development-security.md#final-dependency-integration-candidate--november-17-2026). Image digests, security fixes, isolation and verification thresholds remain intact. Main promotion is prohibited, PR #25 stays separate and existing operational/live-provider/native Safari/multi-instance/workflow/deployment/release acceptance remains open. The Dependabot routing proposal is present in develop; it needs authorized promotion of the default-branch configuration before taking effect for future version updates.

Earlier blocked attempt: the complete candidate passed functional, real SQL and SQL security checks, but its fresh full security gate stopped at 22 npm HIGH findings rooted in picomatch [GHSA-fc5q-39xc-vjpf](https://github.com/advisories/GHSA-fc5q-39xc-vjpf), updated November 17. The registry's latest 4.0.7 is affected and the advisory lists no patched release. PR #51 remains open; this local candidate/handoff is unpublished. No gate was weakened, advisory suppressed or force/downgrade applied. Final image scans were not reached in this failed gate; earlier passing runs retain their exact dates and revisions. Completion requires a verified remediation that passes the unchanged gates.

## Angular CLI integration checkpoint — November 17, 2026

Eight dependency PRs are merged and have passed their distinct develop runs. PR #44's overlapping Angular manifest/lockfile changes are resolved with CLI and build both at 22.2.7, retaining ESLint 10.15.0, Playwright 1.65.1 and existing security overrides. The lockfile was regenerated without force or legacy-peer-deps and deduplicates matching DevKit packages. Fresh candidate verification and exact-head hosted results are recorded separately in [the security handoff](docs/development-security.md#angular-cli-integration-checkpoint--november-17-2026) and Notion. PR #44 is still unmerged at this preparation snapshot; main promotion remains prohibited and PR #25 stays separate.

The resolved CLI candidate passed fresh full local functional/security gates, including all nine browser cases and all four image scans. These are local candidate results; final commit/current-head PR and develop CI evidence is recorded separately in Notion. No separate SQL runtime test was rerun for this frontend-only resolution.

## Sequential dependency integration — November 16, 2026

The user authorized integrating dependency PRs #41–#51 into develop one at a time, requiring fresh combined-base PR checks and a separate successful develop push run before advancing. The first four (#48, #41, #45, #47) are merged and passed their distinct develop runs. PR #50's synchronization conflict is resolved by retaining both Coverlet 10.1.1 and MVC Testing 10.0.15; verification of that combined candidate is recorded in [the integration security handoff](docs/development-security.md#sequential-dependency-integration--november-16-2026). No PR #50 merge or new hosted result is claimed by this preparation note. Main promotion is not authorized, PR #25 stays separate and existing live-provider/native Safari/deployment/release criteria remain open.

The resolved PR #50 candidate passed fresh full local functional/security gates, the separate real SQL test and SQL security checks. These results precede publication and do not claim new hosted acceptance; final head/merge/develop CI evidence is recorded separately in Notion.

## Dependabot integration — November 14, 2026

Eight existing Dependabot branches are integrated on `feature/dependabot-to-develop` for develop: Angular 22.2.5 / CLI/build 22.2.6, jsdom 31.0.0, Vitest/coverage-v8 5.1.0, Node 26 and Trivy 0.76.2. The SDK proposal is resolved to patched SDK 10.0.404 / ASP.NET Core 10.0.15 rather than the vulnerable runtime in the older proposed digest. Browser testing retains its pinned Node 24/Playwright image; package engines support both Node lines. Existing dependency fixes, container restrictions and verification gates remain intact. See the [dependency integration record](docs/development-security.md#dependabot-integration--november-14-2026) for verification and merge evidence. UI-shell PR #25 remains separate; no deployment or production change is included.

The full functional and security gates passed locally: 8 unit, 93 credential-free integration/protocol, 12 frontend and 6 Chromium/Firefox/WebKit tests; no source/history secrets, no NuGet advisories, zero npm vulnerabilities and zero HIGH/CRITICAL findings in all four images. Coverage thresholds and formatting passed. Real SQL probes were not rerun locally; hosted PR/develop evidence is recorded separately.

### Core notification delivery — WBS 1.8

The API can persist notification intent through `INotificationService`; the worker runs the initial delivery plus up to three retries, honoring later provider Retry-After values. Teams Workflows Adaptive Cards, SMTP STARTTLS relay and Slack incoming webhooks are opt-in transports. SMS uses a provider-neutral, non-delivering mock pending provider selection. All channels default to Mock and never claim delivery in that mode. Each attempt retains safe NotificationLog evidence; uncertain sends stop for reconciliation. Apply `NotificationChannels` after `WorkerAttempts` before deployment. See [notification configuration, evidence and remaining acceptance](docs/notifications.md). Live recipients/endpoints, actual delivery acceptance, lifecycle event wiring and worker IT-alert routing remain open. No live sends or persistent database migration were performed.

The requested November 14 security rerun passed the complete `scripts/verify-security.ps1` gate: no source/history secrets (25 scanned commits), no transitive NuGet advisories, zero npm vulnerabilities and zero HIGH/CRITICAL findings in each backend/frontend/SQL/E2E image. The earlier functional gate and final backend rerun passed 8 unit, 145 credential-free integration/protocol, 12 frontend and 6 Chromium/Firefox/WebKit tests; disposable SQL and SQL security checks passed separately. These functional/SQL results were not rerun during this documentation follow-up. See the [WBS 1.8 verification record](docs/development-security.md#wbs-18-notifications--november-14-2026). Implementation is committed locally as `a27702b7a7b993f9d701963e6ac9c62eaec01adb` on `feature/1.8-notifications`, based on worker commit `224282aa4c56bf5812fe066408614703e550f695`; no WBS 1.8 PR or hosted CI result is claimed.

Pre-PR preparation on November 14 rechecked local implementation commit a27702b7a7b993f9d701963e6ac9c62eaec01adb: the complete security gate exited 0, with no source/history leaks (26 commits), no NuGet advisories, zero npm vulnerabilities and zero HIGH/CRITICAL findings in all four images. Docker startup failures from the commit handoff are resolved. Earlier functional/browser/disposable-SQL results remain applicable and were not rerun. No open PRs were found through the GitHub API; remote develop remains 3b103026b10281da3832d8a855cf2548f47ba3c3 and does not contain worker prerequisite 224282aa4c56bf5812fe066408614703e550f695. A future review must account for that prerequisite. No push, PR, merge or live action occurred. This documentation follow-up remains uncommitted.

PR #28 follow-up corrects a SQL-test timing assumption by using SQL Server time to control fixture eligibility and explicitly checking future/due claims. The backend gate (8 unit, 145 credential-free integration tests), separate disposable SQL test and SQL security checks passed. Production notification behavior is unchanged; original hosted scans passed, while the job failed in the SQL probe. Current corrected-head hosted evidence is tracked in Notion and the PR.

The correction also passed a fresh complete security gate: no source/history leaks (27 commits), no NuGet advisories, zero npm vulnerabilities and zero HIGH/CRITICAL findings in all four images. Hosted acceptance belongs to the corrected PR head.

The Notion WBS 1.8 record (private project evidence) and related tasks now contain the same statuses, evidence and remaining criteria, including the added Slack/SMS leaves and provisional parent effort. [AGENTS.md](AGENTS.md#8-before-each-pull-request) requires security checks and README/AGENTS/Notion reconciliation before commits and PR publication.

### Core background worker — WBS 1.7

The separate opt-in worker provides atomic SQL claims, an eight-type dispatcher, durable attempt history and a four-attempt retry policy (1/4/16-minute delays). `WorkerAttempts` must be applied before starting it. Workflow handlers remain deferred; interrupted or uncertain external actions stop for reconciliation. See [worker setup and execution contract](docs/background-worker.md). IT-attention events and retained failures require operational monitoring; IT-alert recipient routing and actual delivery validation remain open; core transports are described above. Verification evidence and deployment limits are recorded in [development security](docs/development-security.md).

Local verification on November 14, 2026 passed both complete gates, the separate disposable SQL probe and SQL security checks. The PR review base is develop, starting from commit 3b103026b10281da3832d8a855cf2548f47ba3c3; current-head hosted checks are tracked separately from these local results. No persistent migration or deployment was performed. Per-step workflow wiring and IT notification delivery remain open acceptance items.

### Main promotion verification — November 14, 2026

Both main and develop now require PRs, up-to-date GitHub Actions build/test/format and security checks, resolved conversations and linear history, including for administrators. Force pushes/deletion are disabled; independent reviewer approvals remain zero. Promotion of develop `ed5c0cfbd1f6c94bc871ae61aebd6b792946bb1c` to main is being prepared under the user's request to establish current-implementation checks on main. Existing develop CI is passing; main execution is pending and must be verified separately. Local preparation does not establish deployment, live-provider acceptance or Sprint 1 completion. Final PR, main commit and hosted results belong in the related Notion WBS records.


Fresh promotion preparation passed both scripts/verify.ps1 and scripts/verify-security.ps1: 8 unit, 145 credential-free integration/protocol, 12 frontend and 6 Chromium/Firefox/WebKit tests; backend lines API/Infrastructure/Core 95.11%/89.6%/100%; frontend statements/branches/functions/lines 96.03%/92.95%/96.29%/95.58%. Secret scans found no leaks (28 history commits), NuGet no advisories, npm zero vulnerabilities, and all four images zero HIGH/CRITICAL findings. The SQL-only test was skipped by local coverage; separate disposable SQL/security validation will be checked in hosted PR/main jobs. Earlier pending-local-gate wording above is superseded. PR/main checks are still pending. No persistent database, deployment or live-provider action occurred.

Main promotion acceptance verified — November 14, 2026 (Pacific/Honolulu). PR #29 ([private repository reference]) was reopened and squash-merged under explicit user confirmation after the reopened PR run 43685417604 passed both required jobs and separate SQL validation. Current main is 1d2f953fc6bfd38e5fca5d6191996aa532a6b6cc; its tree exactly matches reviewed develop ed5c0cfbd1f6c94bc871ae61aebd6b792946bb1c. The distinct push-triggered main run [private repository reference] completed successfully: Build, test and format (job 132725932468) and Security checks (job 132725932253), including Verify SQL Server encryption and restrictions. Final GitHub readback confirmed the run's branch/event/SHA and current main head; main/develop required checks and main admin enforcement remain active. This closes the CI-on-main criterion and supersedes earlier pending-main statements. It does not close unrelated Sprint 1, deployment, Windows/IIS, native Safari or live-provider acceptance. No deployment, persistent migration, tenant change or live provider action occurred. These local handoff documentation changes remain uncommitted; the pre-existing AGENTS.md changes are preserved.

### Organization-seed publication — November 15, 2026

The user authorized committing the scoped organization-neutral cleanup/importer, pushing feature/1.2.3.4-organization-seeds and opening a draft PR against develop. Fresh scripts/verify-security.ps1 exited 0 after starting Docker Desktop: source/history scans found no leaks (31 local commits), transitive NuGet auditing found no advisories, npm reported zero vulnerabilities, and backend/frontend/SQL/E2E images each had zero HIGH/CRITICAL findings. The initial attempt failed before scanning because the Docker engine was stopped; the complete retry passed without weakening gates. Earlier applicable full functional and separate SQL/SQL-security evidence is reused for the unchanged implementation, not claimed as a fresh run.

Only the scoped source, tests, synthetic example and organization-seed documentation are included. Pre-existing main-promotion/Sprint reconciliation notes remain local and unstaged. Ignored requirements, organization packages, secrets and helper files remain unpublished. Final implementation commit, draft PR and actual-head hosted status will be recorded in Notion after publication; no earlier hosted result validates this pending commit. No merge, history rewrite, repository visibility change, persistent migration, deployment, tenant mutation or live notification is authorized.

### Develop-to-main promotion preparation — November 15, 2026

The user authorized creating a PR from develop to main, monitoring/fixing CI, merging after required checks pass and monitoring the distinct main push run. Source develop is d7b7ee78914cd96b4deb576130de299a8954a657; current main is 1d2f953fc6bfd38e5fca5d6191996aa532a6b6cc. Their direct file delta is 32 files containing verified-worker profile validation and organization-neutral catalog upload. Histories diverge (41 ahead / 1 behind) because the prior promotion was squash-merged; do not interpret that as 41 new features. Main/develop are protected, with current build/security checks and administrator enforcement preserved.

Fresh scripts/verify-security.ps1 exited 0: no source/history leaks (33 local commits), no transitive NuGet advisories, zero npm vulnerabilities and zero HIGH/CRITICAL findings in backend/frontend/SQL/E2E images. Existing successful develop push run 43743700807 validates the exact source tree, including full functional and separate SQL/security checks; it is reused evidence, not this promotion PR's or main's execution. Earlier applicable local functional/SQL results remain distinct. No code or dependency changes were made for promotion preparation.

This local handoff note and pre-existing documentation edits are not committed into the promotion. Ignored requirements, operational packages and secrets remain unpublished. Related Notion Progress Notes retain exact PR/merge/current-head CI evidence after each authorized phase; final main results must be verified separately. No deployment, persistent migration, tenant mutation, live send, history rewrite or visibility change is authorized. Operational approval/bootstrap/CSV/export/native Safari/live-provider/deployment acceptance and all prior task estimates/dependencies/statuses remain unchanged.

### Approved exact-tree promotion replacement — November 15, 2026

Direct develop-to-main PR #32 has 17 merge conflicts from prior squash-promotion ancestry and cannot start PR CI. Both protected branches require linear history. The user explicitly approved replacing it with a main-based promotion PR, then monitoring CI, merging after checks pass and monitoring main. Candidate 3314e8371cdc889c2e44ea773b7f5d2ed1b95d31 on feature/promote-develop-main has current main 1d2f953fc6bfd38e5fca5d6191996aa532a6b6cc as parent and tree 8e54f249026b1d83d40ee9051f3b4d393ff022ae, exactly matching develop d7b7ee78914cd96b4deb576130de299a8954a657. No source, protected history or branch protection was changed to construct the candidate; only a promotion commit with main ancestry was created and pushed.

The replacement's fresh complete verify-security.ps1 gate exited 0: source/history scanning, transitive NuGet/npm auditing and all four image gates passed, with zero HIGH/CRITICAL image findings. Earlier functional/real-SQL evidence applies to the identical source tree; replacement PR/main hosted execution is still pending. The direct conflicted PR will be closed once the replacement exists. Keep these local notes and prior edits uncommitted; only the verified source snapshot is promoted. All operational acceptance, private/ignored files and production/live-provider boundaries remain unchanged.

### Main promotion completion — November 15, 2026

Conflicted develop-to-main PR #32 was replaced, under explicit user approval, by the main-based exact-tree PR #33. PR #32 is closed unmerged. Replacement head 3314e8371cdc889c2e44ea773b7f5d2ed1b95d31 has exactly develop d7b7ee78914cd96b4deb576130de299a8954a657 tree 8e54f249026b1d83d40ee9051f3b4d393ff022ae and prior main as parent. The replacement PR run 43746788352 passed build/test/format and security, including separate SQL validation, before the authorized squash merge. Main is now 2dfa68d7115d57ee7e2deecfea257b2b38e60e1f with that same tree. Both branches remain protected with linear history and main administrator enforcement/current build/security requirements intact.

The separate main push run 43747199317 completed successfully on that exact main head. Build, test and format (job 132913012623) and Security checks (job 132913012457) both passed; the separate SQL encryption/restriction/disposable-database step also passed. API readback confirmed event=push, branch=main, current head and all job/step conclusions. No CI code fixes or reruns were needed. Existing local numerical coverage/test results remain separate from hosted job conclusions. No deployment, persistent user/production migration, live provider action, repository visibility change or protected-history rewrite occurred. Ten related Notion Progress Notes now contain final PR/merge/main CI evidence; statuses, estimates, dependencies and operational/live-provider/deployment criteria remain unchanged. Local handoff notes and prior edits remain uncommitted; private requirements/packages remain unpublished.

### Documentation handoff publication — November 15, 2026

The user authorized committing the remaining handoff notes, pushing a documentation branch and opening a draft PR against develop. The scoped branch is feature/1.1.4-handoff-notes, based on develop d7b7ee78914cd96b4deb576130de299a8954a657. Only README.md, AGENTS.md and docs/development-security.md change. The dated Sprint 1/protection/promotion evidence is preserved, and the README's old uncommitted seed snapshot is explicitly historical. This authorization supersedes the earlier phase-specific instructions to keep these handoff notes uncommitted; it does not authorize merging the documentation PR.

Fresh scripts/verify-security.ps1 exited 0: source/history scans found no leaks (35 scanned commits), transitive NuGet auditing found no advisories, npm reported zero vulnerabilities, and backend/frontend/SQL/E2E images each reported zero HIGH/CRITICAL findings. Lower-severity image findings are not claimed absent. Documentation scope, recorded commits, guide sections, exact main CI evidence, private-file exclusion and git diff --check passed. Application tests and SQL probes were not rerun for this documentation-only change; earlier local and hosted results retain their recorded revisions and scope. A final redacted source scan follows these handoff edits.

No application, schema, workflow, dependency, image, secret or operational configuration changed. Ignored requirements, organization packages and helper files remain unpublished. Related Notion records retain acceptance, status, dependencies and provisional effort; final documentation commit/PR/current-head CI references will be recorded there after publication without an extra documentation-only commit loop. No merge, deployment, persistent migration, tenant mutation or live provider action is authorized.

## WBS 1.3 local enrollment and audit follow-up — November 15, 2026

The remaining local enrollment work is implemented on feature/1.3-enrollment-acceptance. Current users.manage authority and CSRF protect explicit Okta identity enrollment/revocation and independently verified employee links. New users receive no roles; existing users are selected only by stable ID. Serializable changes and stable-actor audits commit together. The deployment-only scripts/bootstrap-admin.sql batch creates the first local Admin only in an empty registry and permanently retires through an audit latch. Remove populated setup invocations and temporary deployment access after use. See docs/authentication.md for verification inputs, parameter binding and recovery boundaries.

Failed sign-ins now append anonymous LoginFailed audits containing only fixed failure categories. Regression coverage includes contact changes/repeat login without account merging, multiple links preserving local roles/history, collisions, stale authority, audit-failure rollback, concurrent enrollment and bootstrap retirement. The synthetic expired-session/reauthentication/return flow is distinct from live browser/Okta acceptance. Enrollment UI and broader role administration remain WBS 5.6 work.

WBS 1.3 remains open for actual tenant/app/MFA and secret provisioning, live logout/provider-outage behavior and the full browser reauthentication journey. No tenant mutation, real enrollment/bootstrap, persistent migration, deployment, commit, push or PR was performed. Earlier hosted results do not validate this uncommitted change. Existing operational catalog, native Safari, multi-instance and release gates remain separate.

Final local verification passed: scripts/verify.ps1 (28 unit, 173 credential-free integration/protocol, 19 frontend and 9 Chromium/Firefox/WebKit tests, builds/analyzers/lint/formatting). Backend line coverage API/Infrastructure/Core: 88.52%/90.38%/100%; frontend statements/branches/functions/lines: 93.54%/94.84%/85.36%/96.24%. The SQL-only test was skipped in coverage and passed separately through scripts/test-dev-database.ps1, including restricted-runtime enrollment, explicit employee links, case/multi-link/future-provider schema checks, concurrency, audit rollback, stale authority and revocation. scripts/test-dev-sql-security.ps1 passed. Native Safari and actual Okta remain unverified. WBS 1.3 parent links and all 15 leaf estimates were checked; the total remains 5.5 provisional effort days. No schema, migration, dependency, pin or threshold changed.

The complete scripts/verify-security.ps1 gate passed (exit 0): no working-tree/history leaks (53 locally available commits), no transitive NuGet advisories, zero npm vulnerabilities and zero HIGH/CRITICAL findings in each backend/frontend/SQL/E2E image. Lower-severity image findings are not claimed absent. Existing scanner isolation, source/read-only restrictions and dependency/image pins remain unchanged. These are local checks on the uncommitted working tree at base 5a94546172361e6c4070f81cd953bdbc80f4e151, not new hosted/production evidence.

### WBS 1.3 pre-PR preparation — November 15, 2026

The dahub-pre-pr skill completed preparation only on feature/1.3-enrollment-acceptance, based on develop 5a94546172361e6c4070f81cd953bdbc80f4e151. Read-only GitHub API inspection found no open PRs. Remote develop still matches that base; no stacked prerequisite is required. The reviewable scope is the local enrollment/revocation/employee-link API, deployment-only first-Admin batch, failed-login audits, regression tests, read-only test-batch mount and related handoffs. No commit, push, PR, merge, persistent migration, deployment or tenant/provider action is authorized or performed by this preparation.

Reused the same-session successful verify.ps1, verify-security.ps1, test-dev-database.ps1 and test-dev-sql-security.ps1 evidence recorded above. These are reused runs, not fresh full-gate executions; implementation/configuration files are unchanged since the completed functional gate. Preparation changes documentation/tracker notes only. A final diff/requirements/WBS consistency check and redacted source scan follow these edits. Uncommitted changes have no new commit, PR-head CI or hosted acceptance evidence.

WBS 1.3.4 remains Completed on its verified implementation criteria; 1.3 remains In progress with 11/15 leaves Completed and four live-environment leaves open. Parent effort is unchanged: 1.3.4 = 2.25, 1.3 = 5.5 and Sprint 1 = 40.25 provisional development-effort days, counted from 5/15/107 leaves respectively. Keep actual Okta/MFA/secret provisioning, live logout/outage and browser reauthentication, native Safari, multi-instance and broader deployment/release acceptance open. Ignored requirements, populated bootstrap invocations, organization packages, secrets and audit/tool output remain unpublished. Final commit/PR/current-head CI references belong in Notion after separately authorized publication.

### WBS 1.3 publication authorization — November 15, 2026

The user authorized committing the scoped WBS 1.3 changes, pushing feature/1.3-enrollment-acceptance, opening a draft PR against develop and monitoring/fixing actual-head CI. Read-only inspection found no open PRs and remote develop still at base 5a94546172361e6c4070f81cd953bdbc80f4e151; no stacked prerequisite is required. This supersedes preparation-only publication limits for this change. No merge, live Okta configuration, persistent migration, real bootstrap/enrollment, deployment or release-history action is authorized.

Publication reuses the unchanged implementation's successful same-session functional, coverage, three-engine browser, disposable SQL and SQL-security results recorded above. The fresh complete publication security gate passed (exit 0): no source/history leaks across 53 locally available commits, no transitive NuGet advisories, zero npm vulnerabilities and zero HIGH/CRITICAL findings in each backend/frontend/SQL/E2E image. This is a new full security run; the functional and separate SQL results above remain reused evidence. Keep ignored requirements, operational packages, populated setup invocations, secrets and tool/audit output unpublished. Add final commit, draft PR and actual-current-head hosted results to Notion after creation without a documentation-only commit loop. WBS 1.3 live-provider acceptance remains open.

### WBS 1.3 main promotion preparation — November 16, 2026

The user authorized promoting verified develop through a main-based PR, monitoring CI, merging after required checks pass and monitoring the separate main push run. Source develop is 668409d7942c434534e98b4c513c159102868d4b; current main is 71948dd88683d701dc3c3620ddef96a8ebad6c56. The direct delta is the 17-file WBS 1.3 enrollment/bootstrap/audit implementation and handoff. Historical squash ancestry makes the histories differ; it is not additional feature scope. The candidate must have current main as parent and exactly develop tree 3ad5058c41e526a89ca824096d820be1660f0d91. Required build/security checks, administrator enforcement and linear history remain intact; no bypass or history rewrite is authorized.

The successful develop push run 43831130900 verified that exact source, including functional/coverage/three-engine browser, full security and separate disposable SQL/security checks. This is reused evidence, not promotion PR or main execution. A fresh full local security gate is required before the promotion PR. No implementation, dependency or configuration change is needed for the promotion.

These preparation notes remain local and are excluded from the exact-tree promotion. Ignored requirements, organization/setup data, secrets and local audit/tool output remain unpublished. Record candidate/PR/merge/current-head hosted evidence in related Notion notes as each phase completes. Existing WBS acceptance/status/dependencies/effort remain unchanged; live Okta/MFA/secrets/logout/browser reauthentication, native Safari, multi-instance and deployment/release gates stay open. No persistent migration, real bootstrap/enrollment, deployment, tenant/provider action, visibility change or protected-history rewrite is authorized.

The fresh November 16 promotion security gate completed with exit 0: source/history scans found no leaks across 55 locally available commits, transitive NuGet auditing found no advisories, npm reported zero vulnerabilities and backend/frontend/SQL/E2E images each reported zero HIGH/CRITICAL findings. Lower-severity image findings are not claimed absent. Existing functional and separate SQL results are reused for the identical source tree, not newly rerun locally. A final redacted source scan follows these local notes; promotion PR and main push checks remain to be verified independently.

### WBS 1.3 promotion completion and notes publication — November 16, 2026

The preparation snapshot above is historical. PR #38 merged the WBS 1.3 implementation into develop as 668409d7942c434534e98b4c513c159102868d4b, whose distinct push run 43831130900 passed both jobs and separate SQL checks. The main-based candidate d3c31bfeb81ee665c231de22f8df3ffe76cd8628 used prior main 71948dd88683d701dc3c3620ddef96a8ebad6c56 as its sole parent and exactly develop tree 3ad5058c41e526a89ca824096d820be1660f0d91. Promotion PR #39 passed exact-head run 43924361643, then was squash-merged under explicit authorization as d5d451c0046e02e28cde9bd0e2f43c1faa09502f. The distinct main push run 43925824353 also passed build/test/format and security, including separate SQL encryption/restriction/disposable-database verification. API readbacks confirmed event, branch, exact head and all job/step conclusions. No CI fix or rerun was needed.

Main's final tree matches the reviewed candidate and verified develop source. Strict required build/security checks, administrator enforcement and linear history remained intact; no bypass, protection change or branch deletion occurred. No deployment, persistent user/production migration, real bootstrap/enrollment, tenant/provider action, visibility change or protected-history rewrite occurred. Passing source/CI gates does not close the existing live-provider, native Safari, multi-instance, workflow/deployment or open-source release acceptance.

The user now authorized committing the remaining notes, pushing a documentation branch, opening a draft PR against develop and monitoring/fixing CI. This supersedes the earlier phase-specific instructions to keep these notes uncommitted. The scoped branch is feature/1.3-promotion-notes, based on develop 668409d7942c434534e98b4c513c159102868d4b; read-only inspection found no open PRs. Only README.md, AGENTS.md and docs/development-security.md change. No merge or further main promotion is authorized by this notes-publication request. Ignored requirements, organization/setup data, secrets and local audit/tool output remain unpublished. Record the final documentation commit/PR/current-head CI in Notion after publication without another documentation-only commit loop.

Documentation-publication verification: fresh scripts/verify-security.ps1 exited 0, with no source/history leaks across 57 locally available commits, no transitive NuGet advisories, zero npm vulnerabilities and zero HIGH/CRITICAL findings in each backend/frontend/SQL/E2E image. Lower-severity image findings are not claimed absent. The three-file diff, recorded commit/tree/run references, private-file exclusions and WBS rollups are consistent; git diff --check passed. A final redacted source scan follows these edits. Application and SQL suites were not rerun locally for this documentation-only change; earlier results retain their exact revisions and scope. No application/schema/dependency/image/workflow/operational configuration changed. New documentation PR-head hosted results are recorded in Notion after publication.
