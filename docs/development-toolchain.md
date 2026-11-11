# Development toolchain decision

Recorded November 2, 2026. Scope: Sprint 1 development setup. These choices implement the agreed Angular / ASP.NET Core architecture without changing business requirements.

Development runs in Docker Desktop Linux containers on this Windows workstation. No workstation .NET SDK or npm installation was performed or is required. Production remains Windows Server 2022 and SQL Server 2022; Windows-specific integrations need separate validation when implemented.

| Area | Selection | Observed validation |
| --- | --- | --- |
| Backend | .NET / ASP.NET Core 10 LTS, `mcr.microsoft.com/dotnet/sdk:10.0` | SDK 10.0.403; Release solution build passes with zero warnings/errors |
| SDK selection | `global.json`: minimum 10.0.100, latest installed stable 10.0 feature band | Resolves to 10.0.403 inside container |
| Frontend | Angular 22, Node 24 LTS image `node:24-alpine` | Angular 22.2.4; CLI/build 22.2.5; production build passes |
| JavaScript runtime | Node 24.x and npm 11.x inside Docker | Node 24.22.2, npm 11.21.1 (bundled Undici patched to 6.29.1) |
| TypeScript | 6.0.x | 6.0.3 resolves and compiles |
| Backend tests | xUnit 2.9.3, VS adapter 3.1.5, Microsoft.NET.Test.Sdk 18.0.0 | API integration test passes; unit project is intentionally empty until domain implementation |
| API integration host | Microsoft.AspNetCore.Mvc.Testing 10.0.0 | Liveness test passes; no SQL or external-service validation |
| Frontend tests | Angular unit-test builder, Vitest, jsdom | Vitest 4.1.12, jsdom 27.4.0; one component test passes |
| Formatting | .editorconfig, dotnet format, Prettier | Backend and frontend checks pass; Prettier 3.9.10 |
| Persistence | SQL Server 2022; EF Core SQL Server provider and dotnet-ef 10.0.14 | Complete 31-table first-release schema, catalog seeds, append-only grants and SQL readiness implemented; see [database foundation](database-foundation.md) for current scope and validation |
| CI | GitHub Actions on Ubuntu, invoking the same Docker verification script | Workflow prepared; local script passes; hosted execution and branch protections pending |

## Verification evidence

- Docker Compose configuration validates; API/web containers run with loopback-only host ports.
- `scripts/verify.ps1` completed successfully using one-off development containers, including `npm ci` against the generated lockfile.
- Backend Release build: zero warnings/errors. Integration tests: 1 passed. Unit project: no tests yet.
- Angular production build: passes. Component tests: 1 passed. Backend/frontend formatting: passes.
- HTTPS checks for direct/proxied liveness and the Angular root are recorded after local certificate setup below.
- The .NET polling watcher failed on Windows bind mounts during rebuilds. The API uses ordinary dotnet run; explicitly restart the api service after backend changes. Frontend checks use separate dependency/cache volumes from the live web service. Static assets are disabled for this API-only project.
- `package-lock.json` records resolved frontend packages. Image tags follow servicing releases; record new runtime versions when pulling updated images.
- GitHub origin is `https://github.com/yojana-hawaii/ai-user-onboarding.git`. Local main/develop preserve existing commit `2235304`; existing license and ignore rules are retained. Develop has no upstream until its first publication. No commit or push was made.
- Git trusts only this workspace through a specific safe.directory entry, allowing the Windows user to use repository metadata initialized by the sandbox.

## Sprint coverage and remaining work

Initial artifacts cover parts of WBS 1.1.1 (Git and branch convention), 1.1.2 (solution), 1.1.3 (Angular and API proxy), 1.1.4 (CI definition), 1.1.5 (test structure/conventions), and 1.1.6.2 (runtime/testing selection). These work packages are not all complete: hosted check execution still needs confirmation. Migration tooling, OpenAPI UI, semantic linting, browser E2E setup and an 80% coverage gate are now configured; see the README quality-check section for commands and coverage scope. The frontend currently uses a single relative `/api` base in both environments, documented in README; separate environment files are not present. Prettier checks formatting; Angular ESLint checks TypeScript and templates, and .NET SDK analyzers run with build warnings treated as errors.

This is a manually prepared, build-verified scaffold. The branding revision updated requirement metadata and naming; business behavior and provisional effort estimates were not changed. No database migration, directory action, ticket creation, authentication setup or production deployment was executed. No production credentials are configured.

## Official references checked before scaffolding

- [Microsoft .NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core): .NET 10 LTS supported through November 2028.
- [.NET 10 supported operating systems](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md).
- [Angular version compatibility](https://angular.dev/reference/versions): Angular 22 supports Node ^24.15.0 and TypeScript >=6.0.0 <6.1.0.
- [Angular releases](https://angular.dev/reference/releases).
- Official [checkout](https://github.com/actions/checkout), [setup-node](https://github.com/actions/setup-node), and [setup-dotnet](https://github.com/actions/setup-dotnet) action documentation was consulted; the final CI uses Docker instead of host runtime setup actions.

## HTTPS development update

Both development servers use TLS: Angular at https://localhost:4200 and the API at https://localhost:5081. Standard fullchain.pem/privkey.pem files are mounted read-only; the frontend proxy retains certificate/hostname verification. A Windows certificate setup script generates and optionally trusts a local certificate without host SDK/npm installation. Builds/tests use separate check services that do not require certificates, so hosted CI can run without access to local private material.

There is no owned public domain. Public ACME issuance and renewal automation are deferred; the PEM input supports future ACME-client output. Production host names, host filtering, DNS validation integration and renewal monitoring remain deployment configuration work. No production deployment, ACME account or DNS change was executed.

Verified after the rename and HTTPS changes: complete Docker verification script passes (backend/frontend builds, one API integration test, one component test and both format checks). Windows HTTPS requests to the frontend, proxied liveness and direct API liveness each returned 200 without bypassing certificate validation. An API request with an incorrect TLS hostname was rejected. Private-key files are ignored by Git; the source mount masks .certs, while only the running services receive its read-only certificate mount.

The security hardening revision and final validation are recorded in development-security.md. Container images now use pinned Dockerfiles, non-root users and read-only source/root filesystems. Check script changes and certificate lifecycle commands are documented in README.md.

Verified locally on November 8, 2026: Release build and .NET formatting passed; 8 unit and 5 API integration tests passed, with the SQL-only migration test skipped in this credential-free run. Merged backend line coverage was 94.91%. Angular lint, production build, formatting and 3 component/routing tests passed with 100% coverage in all four frontend metrics. All 6 browser smoke tests passed across Chromium, Firefox and WebKit. The live development Swagger UI and OpenAPI document returned HTTPS 200 with certificate verification enabled. The hosted build/test/format job passed on the initial implementation commit in PR #17. Results for the final security-remediation commit are tracked in that PR's checks; native Safari validation on macOS remains unverified.

After security validation, the E2E image moved to the supported Ubuntu 26.04 Playwright image with Node 24 retained and the APT security fix applied. The final complete security command passed with zero HIGH/CRITICAL findings in all four development images, and all six browser smoke tests passed again. See [the security verification record](development-security.md#cross-browser-test-image--november-8-2026) and [PR #17](https://github.com/yojana-hawaii/ai-user-onboarding/pull/17).
