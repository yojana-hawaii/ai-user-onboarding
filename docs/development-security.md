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