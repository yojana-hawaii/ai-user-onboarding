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
