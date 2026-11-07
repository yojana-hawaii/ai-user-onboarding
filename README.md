# DaHub development workspace

Angular frontend, ASP.NET Core API, domain/infrastructure libraries and initial tests. Authentication, permissions, database access and employee workflows remain later Sprint 1 work.

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
| Security gates | Scan source and fetched Git history with redacted findings; audit transitive NuGet dependencies and the frontend lockfile; scan actual built images. Fail on NuGet advisories, npm HIGH/CRITICAL findings, image HIGH/CRITICAL findings, or scanner errors. No blanket vulnerability exceptions are configured. | [verify-security.ps1](scripts/verify-security.ps1), [compose.security.yaml](compose.security.yaml), [.gitleaks.toml](.gitleaks.toml) |
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

GitHub settings were applied and verified on November 2, 2026. At that verification, only one collaborator had write access, so independent approval was not required. Add an eligible reviewer and raise the approval requirement when available. The CI file defines PR/push checks plus a weekly advisory scan. The weekly schedule becomes active after the workflow reaches the default branch. Use the pull request checks and GitHub Actions history to confirm hosted execution; local verification alone does not establish a hosted CI result.

Origin: https://github.com/yojana-hawaii/ai-user-onboarding.git. See [the security change record](docs/development-security.md) for detailed evidence and remaining release controls. Okta authentication, application permissions, session revocation, CSRF protection, audit logging and production secret management remain required before employee workflows or privileged integrations are enabled.

Use main for the reviewed baseline, develop for integration and feature/<task-id>-<description> for work. Keep the local requirements/ documents excluded as currently configured. See CONTRIBUTING.md.

## Project layout and remaining work

- src/DaHub.API: HTTP entry point and anonymous process-liveness endpoint.
- src/DaHub.Core and src/DaHub.Infrastructure: initially empty domain/integration libraries.
- src/dahub-web: Angular application and HTTPS development proxy.
- tests/DaHub.Tests.Unit: unit-test structure; domain tests begin with implementation.
- tests/DaHub.Tests.Integration: in-memory API host and liveness test.
- scripts/: all environment PowerShell entry points.
- .docker/: pinned development tool images and explicit formatting override.

Phase 1 requires server-side Okta OIDC with MFA, locally owned authorization, immediate revocation checks, secure session cookies and CSRF protection before employee workflows. Directory/M365 service identities, append-only audit evidence and SQL migrations are still pending. No employee data, production credentials, database migration or production provisioning action is part of this scaffold.
