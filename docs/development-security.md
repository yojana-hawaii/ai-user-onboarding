# Development security changes

Recorded November 2, 2026. Scope: development environment and repository safeguards; not a production security certification.

## Implemented controls

- API/frontend/check containers use non-root UIDs, read-only root filesystems and application source, cap_drop ALL, no-new-privileges and CPU/memory/PID limits.
- Specific source mounts exclude root .env, Git metadata and certificate storage. Runtime services receive only their read-only certificate/key mounts; checks receive none. Outputs/caches use writable Docker volumes.
- .NET uses a private, user-owned XDG_RUNTIME_DIR for named locks, preserving the runtime's current security requirements. Read-only source builds use an external artifacts path. Old in-tree bin/obj files are excluded from compilation.
- GitHub workflow is no longer ignored, checkout is pinned by commit, and images/scanners are pinned by digest. Scheduled security checks and Dependabot configuration are included.
- Secret scans redact findings and cover mounted source plus all fetched Git history. NuGet audits include transitive dependencies and treat advisories as errors. npm audits the lockfile without deleting the check service's installed modules, so security and functional checks can coexist.
- Image scans inspect the actual built images via temporary archives, with no Docker socket or credentials mounted. HIGH/CRITICAL findings fail the gate, including unfixed findings. Scanner failures also fail closed.
- The upstream SDK scan reported a fixed Ubuntu Brotli advisory; the development image installs libbrotli1 1.1.0-2ubuntu0.1. The Node tool image uses pinned Node 24 Alpine and npm 11.21.1. npm's bundled Undici is explicitly patched to 6.29.1 until an upstream npm release includes it. Revisit these temporary package patches when refreshing upstream images; do not silently suppress their advisories.
- Certificate scripts check trust/key pairing/expiry, refuse unrelated certificates, serialize mutations, stage replacements, roll back failed replacements, retain pending cleanup evidence and remove exact old Root/My entries and private keys. Windows DACL-only updates and the certificate-store API avoid unrelated audit-policy privileges and interactive root-store removal prompts.

## Verification

Final local verification completed November 7, 2026:

- Backend Release build: zero warnings/errors; one API integration test passed. The domain unit-test project remains empty until domain behavior is implemented.
- Angular production build, one component test, and backend/frontend formatting checks passed.
- Source and fetched Git-history secret scans reported no leaks. npm audit reported zero vulnerabilities; the NuGet transitive audit completed without advisories.
- The current image database initially reported CVE-2026-109518 in Ubuntu apt/libapt. The backend image now installs apt and libapt-pkg6.0t64 2.8.4ubuntu0.1 in addition to the Brotli fix. Both final development images reported zero HIGH/CRITICAL findings with no vulnerability suppression.
- Running services use non-root UIDs, read-only root filesystems, no-new-privileges and cap_drop ALL. Actual frontend source writes were rejected; root Git metadata and .env are absent from the application containers.
- Frontend, direct API liveness and HTTPS-proxied liveness returned 200 with certificate validation enabled.
- Certificate renewal, previous trust/key cleanup, unrelated-certificate preservation, replacement-failure rollback and managed removal passed in the earlier November 2 session. The isolated tests use temporary untrusted certificates and remove their test keys.

GitHub settings applied and verified in the November 2 session: secret scanning and push protection enabled, Dependabot security updates enabled, default workflow token read-only, workflow PR approvals disabled. Main requires a PR, current GitHub Actions build/security checks, resolved conversations and linear history; admins are included and force pushes/deletion are disabled. One collaborator has write access, so independent approval is not yet required. These hosted settings were not reconfigured during the November 7 verification. At local verification, application/CI publication and hosted CI execution were still pending. Check the pull request and Actions history for subsequent hosted results.

## Remaining release boundaries

This environment contains no employee data or production credentials. Okta authentication, application authorization, session revocation, CSRF, audit logging, isolated privileged workers, SQL controls, production deployment and monitored public ACME renewal remain planned work. No database migration or directory/M365 action was performed.

The GitHub repository is public. Local confidential requirements and certificate material remain excluded. The application baseline is published through a feature branch and pull request; local checks alone do not demonstrate hosted CI execution. Independent approval requires another eligible reviewer; a single collaborator can enforce PRs/checks without requiring an unavailable second approver.

References: [Docker service hardening](https://docs.docker.com/reference/compose-file/services/), [GitHub secure workflow use](https://docs.github.com/en/actions/reference/security/secure-use), [Dependabot configuration](https://docs.github.com/en/code-security/reference/supply-chain-security/dependabot-options-reference), [.NET artifacts layout](https://learn.microsoft.com/dotnet/core/sdk/artifacts-output), [Trivy .NET coverage](https://trivy.dev/docs/v0.76/guide/coverage/language/dotnet/).

## SQL Server validation — November 7, 2026

**Initial result: failed the project's zero HIGH/CRITICAL image-finding threshold.** The three findings below were subsequently remediated and verified; see the remediation results at the end of this section. This section covers the new local SQL Server setup; the earlier API/frontend results do not cover SQL Server.

Validated SQL Server 2022 Developer 16.0.4305.5, image `mcr.microsoft.com/mssql/server:2022-latest@sha256:cf8f0ab2ebe61feec2eed2629061a07d247f791c2a8ddda74a4ac9f06a291718`, using the repository's pinned Trivy 0.76.1 scanner. The raw local report is `.tools/security/sql-server-report.json` (Git-ignored). The temporary image archive was removed. No database or infrastructure settings were changed during this validation.

### Initial findings

1. **HIGH — Image vulnerabilities:** 76 HIGH package/binary findings, zero CRITICAL findings, representing 26 distinct CVEs. Four package findings affect `apt`, `apt-transport-https`, `libapt-pkg6.0`, and `libbrotli1`. The report lists patched versions `2.4.15ubuntu0.1` for the APT packages and `1.0.9-2ubuntu0.1` for Brotli. The other 72 findings are 24 Go standard-library CVEs repeated across `/opt/mssql-extensibility/bin/launchpad`, `/opt/mssql/bin/launchpadd`, and `/opt/mssql/bin/setnetbr`, built with Go 1.26.1. Prefer a Microsoft-published patched image and rescan its pinned digest; patching OS packages alone will not repair embedded Go code. These are scanner findings, not evidence that each CVE is exploitable through the SQL listener. No findings were suppressed.
2. **Encryption policy gap:** an explicit mandatory-encryption connection reported `encrypt_option=TRUE`, but an optional-encryption connection (`sqlcmd -No`) reported `FALSE`. There is no local `mssql.conf` enforcing encryption. Configure server-required TLS, then repeat both probes. Certificate validation without `-C` rejected the self-signed certificate; the current local instructions bypass identity validation. Use a trusted certificate and hostname validation before a shared or production deployment. See [Microsoft's Linux encryption settings](https://learn.microsoft.com/en-us/sql/linux/security/encrypted-connections?view=sql-server-ver17).
3. **Coverage gap:** `scripts/verify-security.ps1` currently scans only the backend/frontend images. SQL Server was scanned separately in this validation; add it to the automated security gate before treating routine verification as SQL coverage.

### Controls verified

- Only `127.0.0.1:1433` is published on Windows. This does not isolate SQL Server from other containers on the Compose network.
- Runtime UID/GID is `10001:10001`, privileged mode is off, `no-new-privileges` is active, and seccomp filtering is active. All capabilities are dropped except the `NET_BIND_SERVICE` bounding capability required by the vendor executable. PID 1's effective capability set was empty.
- Container limits are 4 GiB memory, 2 CPUs in Compose, and 512 PIDs. The root filesystem remains writable; SQL data is in the named volume. No source directory, host credential file, or Docker socket is mounted into SQL Server.
- `.env.sql` is untracked and ignored. Windows ACLs grant only the current user and SYSTEM access. The password is also present in the container environment, so Docker administrators can retrieve it; this is not a production secret store.
- A nonexistent login with an invalid password was rejected. `sa` has password policy enabled and expiration disabled; it remains a local administrator account, with no application login configured.
- `xp_cmdshell`, OLE automation, ad hoc distributed queries, CLR, cross-database ownership chaining, and contained-database authentication are disabled. Legacy `remote access` is enabled by default; review whether any remote stored-procedure use is intended.
- `DaHub` has TRUSTWORTHY and database chaining off. Transparent data encryption is off; backup protection, at-rest encryption and audit policy were not established by this setup.
- Source and all fetched Git-history secret scans passed; the history scan covered three commits. The intentionally Git-ignored local password was checked through ACL/Git inspection, not secret-scan coverage.

This initial validation did not change production, rotate credentials, apply schema migrations, or certify application authorization. Its raw report records the original upstream image, not the remediated image below.

### Remediation verified — November 7, 2026

**All three reported issues are resolved for this local development setup.**

- `.docker/sql/Dockerfile` builds `dahub-sql-dev:local` from the same pinned Microsoft CU27-GDR1 base. It installs the fixed APT/Brotli versions listed above and removes the three unused extensibility helper binaries. No vulnerability ignore rules were added. This image supports the SQL engine only: external R/Python script execution is unsupported and must stay disabled. Refresh the base and revisit these temporary changes when Microsoft publishes patched helpers.
- `.docker/sql/mssql.conf` is mounted read-only and sets `network.forceencryption=1` and `network.tlsprotocols=1.2`. The previously failing optional-encryption probe now passes: SQL reports the connection encrypted even with `sqlcmd -No`. This follows [Microsoft's server encryption configuration](https://learn.microsoft.com/en-us/sql/linux/sql-server-linux-configure-mssql-conf). The development server still uses its local self-signed certificate with explicit client trust; shared/production use still needs a trusted certificate and hostname validation.
- `scripts/verify-security.ps1` now builds and scans the SQL image together with the backend and frontend images. All three returned **zero HIGH/CRITICAL findings** in the complete local gate. Source/fetched-history secret scans and NuGet/npm dependency audits also passed. The gate continues to reject findings including unfixed vulnerabilities and operational scanner errors.
- `scripts/test-dev-sql-security.ps1` checks actual connection encryption with an optional-encryption client, disabled external/unsafe features, database flags, helper removal, TLS policy, non-root/non-privileged execution and loopback publishing. It failed against the original running server for missing required encryption, then passed against the patched server. CI now runs database setup plus these assertions after the image/security gate. Hosted execution has not been performed in this session.
- The patched SQL container is healthy, `DaHub` is online, and its database GUID was identical before and after image replacement. Existing credentials and the named data volume were preserved.

Reproduce from PowerShell 7 with Docker Desktop running Linux containers:

```powershell
./scripts/setup-dev-sql.ps1
./scripts/test-dev-sql-security.ps1
./scripts/verify-security.ps1
```
## Cross-browser test image — November 8, 2026

The first Ubuntu-based Playwright image failed the HIGH/CRITICAL gate with four HIGH package findings: CVE-2026-109518 in apt/libapt-pkg6.0t64, and CVE-2025-3887 in gstreamer1.0-plugins-bad/libgstreamer-plugins-bad1.0-0. WebKit links the affected codec parser library, so removing the package would break the test engine.

A Debian 13 candidate supplied the corrected GStreamer package (1.26.2-3+deb13u3) and passed all six browser smoke tests, but its image scan reported 291 HIGH/CRITICAL package findings. That candidate was rejected. The final E2E Dockerfile uses the official digest-pinned Playwright Ubuntu 26.04 image while retaining the project's Node 24 runtime. APT and libapt-pkg7.0 are upgraded to the scanner-listed fixed version 3.2.1ubuntu0.1. See the [Debian CVE record](https://security-tracker.debian.org/tracker/CVE-2025-3887) and [Playwright supported systems](https://playwright.dev/docs/intro#system-requirements). Runtime tests retain the non-root user, read-only source/root filesystem, dropped capabilities, writable temporary browser profiles, and dedicated network without published ports or secret mounts.

Security verification includes backend, frontend, SQL and E2E images. Public image archives are copied once into Docker-local scanner storage before analysis to avoid slow Windows bind-mount access, and host/cache copies are removed afterward. The scanner receives exported images rather than a Docker socket or credentials. Its 20-minute operational timeout accommodates the larger browser image; the HIGH/CRITICAL failure threshold remains enforced.

Verified locally on November 8, 2026 (Pacific/Honolulu): the complete updated verify-security.ps1 command passed. Working-tree/history secret scans and NuGet audit passed; npm reported no HIGH/CRITICAL findings and 15 moderate lint-tool dependency findings. Backend, frontend, SQL and the final E2E image each reported zero HIGH/CRITICAL findings. All six Chromium/Firefox/WebKit smoke tests passed again on the final APT-patched image. SQL encryption/container checks and the isolated migration/runtime-permission test also passed. No vulnerability exception was added.

GitHub inspection during this work found main protected and develop unprotected. The new PR targets develop, so failing checks are not yet an enforced merge blocker there. This observation is tracked in the Notion lint/CI records.

## Okta, session and dependency verification — November 9, 2026

Verified locally on November 9, 2026 (Pacific/Honolulu), against the working tree based on `ee99fb4667aa0d91bb1b1eb9dfb55c4c31b615e1`. The authentication and remediation changes are uncommitted; no new PR or hosted CI result is asserted by this record.

### Complete security suite

`pwsh -NoProfile -File scripts/verify-security.ps1` completed with exit code 0. It rebuilt the scanner and all four development images, scanned actual exported image archives with pinned Trivy 0.76.1 and a refreshed vulnerability database, and removed its temporary host/cache archives. Existing HIGH/CRITICAL gates, including unfixed findings and operational scanner failures, were retained.

| Check | Result |
| --- | --- |
| Working-tree Gitleaks scan | No leaks |
| All fetched Git history | No leaks; 11 commits scanned |
| NuGet transitive audit | Passed; no advisories |
| Backend image | 0 HIGH/CRITICAL findings |
| Frontend image | 0 HIGH/CRITICAL findings |
| SQL Server image | 0 HIGH/CRITICAL findings |
| Browser E2E image | 0 HIGH/CRITICAL findings |

The images' lower-severity findings were outside the configured HIGH/CRITICAL report filter; these results do not claim zero vulnerabilities at every severity. No image changes or vulnerability exceptions were required in this run. Application source/NuGet dependencies are mounted/restored by development containers rather than baked into these tool images; source/dependency audits complement the image scans.

### npm finding and remediation

The initial suite's npm audit passed its HIGH/CRITICAL threshold but reported **10 moderate findings**, all stemming from `minimatch` 10.2.6 through ESLint, `@eslint/config-array` and `@typescript-eslint/typescript-estree`. [GHSA-g3hv-75wg-m249 / CVE-2026-118764](https://github.com/advisories/GHSA-g3hv-75wg-m249) identifies an attacker-controlled glob-pattern amplification problem and lists 11.0.1 as fixed.

`src/dahub-web/package.json` now overrides only `minimatch` versions `>=10.0.0 <11.0.1` to **11.0.1**, with the corresponding integrity-pinned lockfile update. Its new nonrecursive glob-matching dependency is included in the lockfile. The fixed release supports the project's Node 24 runtime. The override is explicit because current lint packages request the affected major; remove/revisit it when those upstream dependencies adopt a corrected release. The reported `npm audit fix --force` ESLint downgrade was not applied, and no advisory was suppressed.

Follow-up verification on the remediated lockfile used a clean `npm ci`, Angular ESLint with zero allowed warnings, production build, all 8 component/interceptor/routing tests, Prettier, and an unfiltered `npm audit --package-lock-only`: all passed, with **zero npm vulnerabilities**. Frontend coverage remained above every 80% gate (92.85% statements, 89.74% branches, 92.85% functions, 92.68% lines). All 6 Chromium/Firefox/WebKit smoke tests passed again after the dependency change.

### Authentication evidence and release limits

The full `scripts/verify.ps1` gate passed for the authentication implementation before this dependency-only remediation: 8 backend unit tests, 34 credential-free API/protocol integration tests, 8 frontend tests and 6 browser smoke tests. Backend line coverage: API 94.21%, Infrastructure 95.87%, Core 100%. Backend code did not change during this follow-up; the affected frontend checks and browser suite were repeated after the dependency fix.

Signed synthetic-provider tests cover valid authorization code + PKCE, invalid issuer/audience/signature/lifetime/state/nonce, missing correlation cookie, unknown and exact-subject mismatches, local user/link/version revocation, idle timeout/sliding activity, CSRF rejection and logout replay. Credentials and access/refresh tokens are not exposed to Angular. Raw OIDC protocol logging is disabled, and only a server-side ID token is retained for provider logout. See [authentication configuration](authentication.md).

The process-local store supports one API instance; restart/eviction invalidates sessions. Actual Okta application registration, MFA allowed/denied outcomes, live logout and expired-session return journey, enrollment/bootstrap administration, local permission policies and production secret/TLS controls remain release work. Native Safari was not tested. The real SQL migration/runtime-security probes were not rerun: there were no schema or SQL-environment changes, and the credential-free gate skips that isolated SQL test. Image scans do not establish database runtime security or production readiness.

Related Notion records: [Okta sign-in and local identity links](https://app.notion.com/p/412ee4cf5eb481d293a2e5109ed3230b) and [CI pipeline](https://app.notion.com/p/412ee4cf5eb4817da8c1f9fea9941ab5). Their updates distinguish completed local implementation/tests from outstanding live-provider and hosted acceptance criteria.

## PR preparation verification — November 10, 2026

Both complete local gates were repeated on the final authentication/remediation tree before PR preparation. `scripts/verify.ps1` passed: 8 unit tests, 34 credential-free integration/protocol tests, 8 frontend tests and all 6 Chromium/Firefox/WebKit smoke tests, plus builds, lint, formatting and coverage gates. One real-SQL test was intentionally skipped. Backend lines: API 94.21%, Infrastructure 95.87%, Core 100%; frontend statements/branches/functions/lines: 92.85%/89.74%/92.85%/92.68%.

`scripts/verify-security.ps1` also passed in full after the minimatch remediation: working-tree and fetched-history Gitleaks scans found no leaks (15 fetched commits), NuGet audit found no advisories, npm reported zero vulnerabilities, and backend/frontend/SQL/E2E images each reported zero HIGH/CRITICAL findings. The root `AGENTS.md` guide is now included in the working-tree scanner mounts. No severity threshold, suppression or security exception was changed. Temporary image archives were cleaned up.

The guide was moved from ignored `requirements/agents.md` to root `AGENTS.md` at the user's request. Other requirements files remain ignored. The authentication branch is `feature/1.3-okta-server-sessions`, stacked on the still-open prerequisite PR #17; merge that prerequisite first and retarget the authentication PR to develop. Hosted checks on the new PR head are separate evidence. The live-provider, single-instance, SQL-runtime and native-Safari limitations above still apply.

## Sprint 1 WBS 1.4 — local RBAC, November 10, 2026

Implemented AUTH-03/SEC-07 local permission resolution and server-side session caching, permission attributes/dynamic policies, 401/403 ProblemDetails, Angular guards and filtered navigation. APR-01/APR-04/OFF-08 authority policies separately enforce own/backup delegation, current-revision urgent execution with both grants and a reason, and IT retention permission. All 19 seeded permission keys are exercised for all seven local roles through the real API cookie/authorization pipeline. Provider groups remain excluded; Admin has no wildcard, urgency or retention entitlement by default.

Migration `InvalidateAuthorization` adds six transactional SQL triggers that increment affected users' versions for local authorization changes. EF uses the trigger-compatible SQL write path. Already cached grants are denied on the next protected request after a change; new sign-in is required. The permission join runs at login; subsequent requests retain the local account/link/version check. See [implementation and deployment details](authentication.md#local-rbac--wbs-14).

The complete local `scripts/verify.ps1` gate passed: 8 unit tests, 52 credential-free integration/protocol tests, 12 frontend tests and all 6 Chromium/Firefox/WebKit smoke tests. One SQL-only test was intentionally skipped by that credential-free run and passed separately through `scripts/test-dev-database.ps1`. Backend line coverage: API 94.56%, Infrastructure 96.76%, Core 100%. Frontend statements/branches/functions/lines: 96.03%/92.95%/96.29%/95.58%. Release builds, analyzers, formatting, lint and all existing coverage gates passed. Browser smoke tests exercise the existing home/proxy scaffold; Angular router tests exercise RBAC route denial/allowance and navigation. Native Safari on macOS remains unverified.

`scripts/test-dev-sql-security.ps1` passed SQL encryption, feature, helper-removal, TLS and container restriction checks. The isolated database test verified migration upgrade/down/reapply, schema constraints, runtime permissions and trigger invalidation/rollback using synthetic data. No persistent development or production database was migrated; no tenant, real user, provider or privileged external system was changed.

Authorization HTTP tests use synthetic enrolled identities and EF InMemory. SQL trigger/persistence behavior is separately tested on real SQL Server. This implements authorization infrastructure and resource-policy checks; employee workflows, role/enrollment administration and their stable-actor audit, approval, urgent-execution recording/notifications, worker rechecks and recovery remain separate implementation work. The UI destinations are workflow placeholders. Existing one-API-instance session-store and live Okta/MFA release limitations continue to apply.

The local implementation was committed as `53b5d3bbf10c5216fb1311596136fa49a6c13ffe` on `feature/1.4-rbac`, based on authentication commit `bd6fd8e`. [Draft PR #23](https://github.com/yojana-hawaii/ai-user-onboarding/pull/23) targets the authentication branch. Initial hosted build/test/format and security jobs started; no passing hosted result, merge protection or main execution is claimed here. Follow-up documentation may advance the PR head; inspect exact-head checks. No requirement version, schema column, relationship, estimate, or deferred scope was changed; the ignored planning requirements remain unpublished.

The complete `scripts/verify-security.ps1` suite exited 0: working-tree and Git-history scans found no leaks (16 locally available commits), transitive NuGet auditing found no advisories, npm reported zero vulnerabilities, and backend/frontend/SQL/E2E images each reported zero HIGH/CRITICAL findings. This does not claim zero lower-severity image findings. Existing scanner isolation, pinned images, dependency locks, severity gates and coverage thresholds were preserved; no dependency change, advisory suppression or gate reduction was needed. Temporary exported image archives were cleaned up.

## WBS 1.4 acceptance closure and 1.5 API foundation — November 10, 2026

This follow-up implements the outstanding 1.4.2.2 behavior rather than waiving its acceptance criterion. Active users with changed authorization versions refresh local permission claims without another provider login. The refresh checks account/link activity and the original validated issuer/subject, reloads the permission union, and rechecks the version before renewing the existing server-side ticket. Changed identity bindings, inactive/revoked accounts/links, unstable concurrent refreshes and database failures fail closed. Unchanged requests do not repeat the permission join. Existing idle expiry, logout replay protection, ID-token retention for logout and strict resource-policy rechecks remain intact. Refresh creates no new login audit event.

WBS 1.5 implements safe ProblemDetails for application errors (including FluentValidation 400, binding errors, auth failures, 404, 429 and 500), bounded correlation IDs, JSON console/rolling-file request logs, aggregate SQL/AD/Entra transport health, explicit mock/unconfigured modes, secret-source validation, production listener configuration, HTTPS redirection, HSTS on HTTPS errors and TLS 1.2/1.3. Only allowlisted safe operational events are emitted; query strings, arbitrary paths, bodies, cookies, tokens, SQL and exception text are excluded. Stable local actor ID accompanies the UPN snapshot. Local `/logs` is private ephemeral tmpfs; persistent production logging and monitoring require deployment configuration. See [API foundation setup and deployment checklist](api-foundation.md).

The final complete `scripts/verify.ps1` passed: 8 unit tests, 92 credential-free integration/protocol tests, 12 frontend tests and all 6 Chromium/Firefox/WebKit smoke tests. The SQL-only test was skipped by this credential-free gate and passed separately through `scripts/test-dev-database.ps1`; `scripts/test-dev-sql-security.ps1` also passed. Backend line coverage: API 96%, Infrastructure 96.76%, Core 100%. Frontend statements/branches/functions/lines: 96.03%/92.95%/96.29%/95.58%. Release builds, analyzers, lint, formatting and all coverage thresholds passed.

New HTTP tests verify no-login grant addition/removal, role/permission/grant edits, revocation, identity rebinding, a change during refresh, database failure, safe error bodies even with HTML Accept headers, HSTS/cache headers after exceptions, log context and file redaction. Real SQL verifies transactional invalidation, direct issuer/subject edits, a single additional permission join on refresh, no repeat join for unchanged sessions, and rollback. The Linux Kestrel handshake probe accepts TLS 1.2/1.3 and rejects TLS 1.0/1.1; the real LDAPS transport rejects its untrusted synthetic certificate. Directory success/outage/deadline/cancellation and aggregate 200/503 scenarios use synthetic transports/checks. Entra metadata requests are credential-free; these tests do not establish Graph authorization or live directory availability. Synthetic user-secret provider/CLI-list and environment-provider startup tests inject their values through early test-host settings; they do not validate a deployed secret store. Real user secrets were neither listed nor changed.

The implementation was locally verified before publication on `feature/1.5-api-foundation`, based on `ed58d3631f608657c7b2934dc7d418546c81303c`. Earlier PR #23/CI evidence does not validate this follow-up. Publication and current-head hosted results are recorded separately; no merge or main/branch-protection claim is made. No persistent database, tenant, production identity or deployed host was changed; existing migrations ran only in the disposable SQL test database. Specification/schema/ERD and estimates remain unchanged; affected requirement references and WBS parent/effort rollups were checked. Other ignored requirements remain unpublished.

Actual Windows/IIS host bindings, the deployed certificate, HTTP 80→HTTPS 443 and frontend TLS/no-mixed-content acceptance remain open under 1.5.5.1 and its parents. Live AD/Entra/Graph permission checks, Okta MFA/logout/session-return acceptance, native Safari, role/enrollment administration, worker implementation and multi-instance sessions remain separate release/workflow work. The local protocol probe establishes Kestrel behavior only. No compliance claim follows from these controls.

The complete `scripts/verify-security.ps1` exited 0: no working-tree or Git-history secrets (18 locally available commits), no transitive NuGet advisories, zero npm vulnerabilities, and zero HIGH/CRITICAL findings in each backend/frontend/SQL/E2E image. This does not claim zero lower-severity image findings. New dependencies are Serilog.AspNetCore 10.0.0, Serilog.Sinks.File 7.0.0 and FluentValidation 12.2.0; no existing lock, image pin, scanner isolation, severity gate, coverage threshold or advisory suppression was weakened. Temporary image exports were removed. A separate redacted working-tree secret scan covers the final documentation handoff.

All 22 affected Notion records were updated successfully with current local evidence, preserving previous notes, original acceptance criteria and estimates. WBS 1.4/1.4.2/1.4.2.2 are Completed. WBS 1.5/1.5.5/1.5.5.1 remain In progress for deployed host/certificate acceptance; the other foundation criteria are locally verified. Publication and hosted verification are tracked separately from these local checks.
