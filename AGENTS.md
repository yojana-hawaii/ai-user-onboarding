# AGENTS.md

Behavioral guidelines for the DaHub project. Apply these alongside the current project requirements and the user's instructions.

Tradeoff: These guidelines bias toward caution over speed. For trivial tasks, use judgment.

## 1. Think Before Coding

Don't assume. Don't hide confusion. Surface tradeoffs.

Before implementing:

Inspect the repository, current requirements, and relevant implementation before asking questions that those sources can answer.
State assumptions that materially affect the result. Proceed with routine, reversible implementation choices within the authorized scope.
If multiple interpretations materially change security, scope, data, or external actions, present the tradeoffs and ask before doing dependent work.
If a simpler approach exists, say so. Push back when warranted.
When clarification is required, continue independent work that does not depend on the answer. Do not repeat permission requests for actions already authorized.

## 2. Simplicity First

Minimum code that solves the problem. Nothing speculative.

No features beyond what was asked.
Avoid speculative abstractions. Use an abstraction when required by an agreed integration boundary, security boundary, or meaningful duplication.
No "flexibility" or "configurability" that wasn't requested.
Handle realistic failures, including unavailable directories, expired credentials, API throttling, partial provisioning, and worker recovery. Avoid branches for scenarios ruled out by enforced invariants.
Prefer readable, maintainable code over a line-count target. Do not remove validation, authorization, audit evidence, retries, or idempotency to shorten an implementation.
The provider-neutral ticketing interface is an agreed requirement; the choice between SysAid and ServiceDesk Plus On-Premises is still deferred.

Ask yourself: "Would a senior engineer say this is overcomplicated?" If yes, simplify.

## 3. Surgical Changes

Touch only what you must. Clean up only your own mess.

When editing existing code:

Don't "improve" adjacent code, comments, or formatting.
Don't refactor things that aren't broken.
Match existing style, even if you'd do it differently.
If you notice unrelated dead code, mention it - don't delete it.

When your changes create orphans:

Remove imports/variables/functions that YOUR changes made unused.
Don't remove pre-existing dead code unless asked.

The test: Every changed line should trace directly to the user's request.

## 4. Goal-Driven Execution

Define success criteria. Loop until verified.

Transform tasks into verifiable goals:

"Add validation" → "Write tests for invalid inputs, then make them pass"
"Fix the bug" → "Write a test that reproduces it, then make it pass"
"Refactor X" → "Ensure tests pass before and after"

For multi-step tasks, state a brief plan:

1. [Step] → verify: [check]
2. [Step] → verify: [check]
3. [Step] → verify: [check]

Strong success criteria let you loop independently. Weak criteria ("make it work") require constant clarification.

Use tests appropriate to the change. Prioritize authorization failures, identity collisions, stale approvals, concurrent execution, partial failures, and recovery for security-sensitive behavior. Documentation-only changes require consistency checks rather than unrelated application tests.
Report what changed, what was verified, and any remaining limitation. Do not describe an unrun check, mocked integration, or illustrative schema as production validation.

## 5. Project Baseline

- Internal employee lifecycle application for a nonprofit community health center. Keep patient PHI out of application records, tickets, fixtures, and logs.
- Angular frontend and ASP.NET Core backend; on-premises Windows Server 2022 and SQL Server 2022. Confirm supported framework/runtime versions before scaffolding; do not assume an old version from historical WBS text.
- Phase 1 sign-in is direct Okta OIDC with MFA. Direct Entra ID sign-in is future work. AD, Entra ID, and Microsoft 365 provisioning/revocation remain in Phase 1.
- Application roles and permissions are local, provider-independent data. The confirmed requirements govern detailed approval, urgent execution, delegation, scheduling, retention, and completion behavior.
- Helpdesk choice remains SysAid or ManageEngine ServiceDesk Plus On-Premises. Use a mock provider until a provider and its integration configuration are confirmed. Equipment assignment and return tracking are deferred.

## 6. Security and Integration Boundaries

- Enforce permissions server-side for every protected operation. UI visibility and provider group claims are not authorization controls. Unknown identities receive no application access or automatic roles.
- Identify local users by stable AppUsers.Id. Link external identities using validated, exact OIDC issuer/subject pairs under authorized enrollment. Never automatically merge accounts or link employees by email or UPN alone.
- Preserve the documented server-side OIDC/session design: validate tokens and callbacks, protect cookies and state-changing requests against CSRF, and keep credentials and downstream tokens out of Angular and browser storage.
- Check local account and external identity activity and AuthorizationVersion on each protected request. Role revocation and account deactivation must not wait for another provider login. Recheck authorization before privileged side effects.
- Keep privileged directory/M365 actions behind a dedicated worker boundary and narrowly scoped service identities. Do not grant broad directory administration merely to make an integration work.
- Never commit, print, or place passwords, client secrets, private keys, access/refresh tokens, session cookies, or raw authentication responses in logs, fixtures, tickets, or documentation. Use synthetic identities and approved secret storage.
- Audit identity enrollment/linking, role changes, approvals, urgent execution, provisioning, manual resolution, and exports with stable actor attribution. Preserve append-only audit and immutable revision/urgent-authorization requirements.
- Bind approval and execution to the correct request revision and employment period. Required retries, stale-job checks, external-result reconciliation, and idempotency must prevent duplicate or obsolete actions.
- Urgent execution requires designated authority and mandatory justification; it does not grant approval. Execution completion cannot conceal pending approval, manual resolution, or unverified EHR work.
- Use mocks and isolated test environments by default. Do not mutate production accounts, groups, licenses, sessions, mailboxes, retention settings, or helpdesk tickets without explicit authorization for that environment and scope. Existing authorization remains valid within its stated scope.
- Do not claim that framework choice, on-premises hosting, encryption, or a retention period alone establishes HIPAA compliance. Record implementation evidence and unresolved security requirements accurately.

## 7. Requirements and Documentation Consistency

Read the current document contents and version metadata; filenames ending in v1.0 do not identify the current specification version.

- `product-requirements-document_v1_0.md`: product scope, release boundaries, and business outcomes.
- `software-requirements-specification v1.0.md`: functional/security behavior and acceptance criteria.
- `data-model v1.0.md`: entities, constraints, identity links, workflow state, and illustrative DDL.
- `entity-relation-diagram v1.0.html`: visual representation of the current data model.
- `all_sprints_wbs_tdd.csv`: implementation tasks, requirement references, dependencies, and provisional estimates.

Follow the user's latest explicit decisions. Investigate disagreements between documents; ask only when a material decision cannot be resolved from the recorded requirements. Do not silently choose a conflicting document or let historical tasks override an agreed requirement.

When a change affects multiple artifacts, reconcile only the affected sections across those artifacts. Update version metadata and cross-references when the specification changes. Preserve deferred scope rather than implementing it opportunistically.

Verify the affected requirement IDs and task coverage, schema columns/constraints and ERD relationships, and WBS parent/dependency links. Recalculate affected parent and sprint estimates from leaf tasks without double-counting. Label new estimates as provisional and distinguish effort from calendar duration. For documentation or schema design work, state clearly that no migration or external action was executed.


## 8. Before Each Pull Request

Use the branch and review conventions in CONTRIBUTING.md. Inspect the target branch and existing PRs so prerequisite commits and the final review scope are clear.

1. Run the security gate before every PR: ./scripts/verify-security.ps1. For implementation changes, run ./scripts/verify.ps1 as well. Documentation-only work still requires the consistency checks described in section 4; report the checks actually run.
2. Include Chromium, Firefox, and WebKit in browser integration/E2E testing through ./scripts/test-e2e.ps1. WebKit provides Safari engine coverage. Claim native Safari validation only after testing Safari on macOS.
3. Enforce at least 80% line coverage in every backend application assembly and 80% frontend lines, statements, branches, and functions. Backend results merge fresh unit/integration hits; keep generated-code exclusions explicit and documented. Include untested application files in frontend coverage.
4. Security checks cover working-tree/Git-history secrets, transitive NuGet dependencies, the frontend lockfile, and the actual backend/frontend/SQL/E2E images. NuGet advisories, npm HIGH/CRITICAL findings, image HIGH/CRITICAL findings (including unfixed ones), and scanner errors fail the configured gates. Preserve these gates when resolving findings.
5. Run ./scripts/test-dev-sql-security.ps1 and ./scripts/test-dev-database.ps1 for SQL security, persistence, or database environment changes. Real SQL tests use a disposable database and synthetic data. The credential-free coverage run skips the SQL-only test, so report the separate SQL result explicitly.
6. Keep application and scanner containers non-root, source/root filesystems read-only, capabilities dropped, and secrets limited to services that need them. Browser tests use their dedicated network and temporary profiles. Scanners receive exported public tool images rather than a Docker socket or credentials.
7. Record security hardening actions, exact verification scope, and remaining findings in README.md and the relevant development/security record. Keep the Playwright package and browser-image versions aligned, preserve pinned image digests and lockfiles, and revisit temporary package patches when refreshing upstream images.
8. Update related Notion task statuses and Progress Notes as part of the authorized PR workflow. Link the PR, verified commit, and CI evidence. Preserve acceptance criteria, estimates, and deferred work. Mark a task Completed only when its own criteria are met.
9. Distinguish local checks, hosted checks on the current PR head, execution on main, and enforced branch protection. A workflow definition or successful local run does not prove hosted execution. Passing PR checks do not establish merge blocking on an unprotected target branch.

For connected writes, inspect every result. Stop the affected write batch on the first approval, access, or quota failure; preserve successful updates and report what remains unsaved. Resolve the cause or obtain user guidance before retrying. Continue independent authorized work, and do not switch tools to bypass an approval check.

## 9. Development Commands and Verified Handoff

Development uses Docker Desktop Linux containers and PowerShell 7. Workstation .NET/Node/npm installations are not required. Consult the checked-in manifests, lockfile, Dockerfiles, and README for current versions and setup.

- ./scripts/verify.ps1: Release builds, .NET analyzers, backend coverage, dotnet format, Angular ESLint, frontend build/coverage, Prettier, and three-engine browser tests.
- ./scripts/verify-security.ps1: secret/dependency gates and all four image scans. Archives are copied into Docker-local scanner storage before analysis and cleaned up afterward; the operational scan timeout is 20 minutes.
- ./scripts/test-e2e.ps1: browser smoke tests against the isolated Angular/API test environment.
- ./scripts/test-dev-sql-security.ps1: SQL encryption, feature, and container restriction checks.
- ./scripts/test-dev-database.ps1: isolated migrations, constraints, rollback, readiness, and runtime-permission tests.
- Development UI: https://localhost:4200. API Swagger: https://localhost:5081/swagger. OpenAPI: https://localhost:5081/openapi/v1.json. Documentation is exposed only in Development.

Historical hosted baseline verified November 8, 2026 (Pacific/Honolulu). The newer local implementation/security handoff follows in section 10; recheck current repository and hosted state before relying on either record:

- [PR #17](https://github.com/yojana-hawaii/ai-user-onboarding/pull/17), verified commit ee99fb4667aa0d91bb1b1eb9dfb55c4c31b615e1. Both hosted build/test/format and security jobs passed.
- Backend line coverage: Core 100%, API 88%, Infrastructure 95.42% (94.91% overall). Frontend lines/statements/branches/functions: 100%.
- Eight unit tests, five credential-free API integration tests, three component/routing tests, and all six Chromium/Firefox/WebKit smoke tests passed. The real SQL migration/runtime-permission test passed separately.
- Backend, frontend, SQL, and final E2E images each reported zero HIGH/CRITICAL findings. At that baseline, fifteen moderate npm lint-tool dependency findings remained; the November 9 audit/remediation results below supersede that dependency status.
- The E2E image uses pinned Playwright 1.65.0 on Ubuntu 26.04 with Node 24 retained, plus the APT/libapt-pkg7.0 fix at 3.2.1ubuntu0.1. See docs/development-security.md for the findings, rejected candidate, and remediation evidence.
- main was protected; develop was unprotected. PR CI passed, while execution of this change on main and enforced merge blocking on develop remained outstanding.
- Native Safari on macOS remains unverified. E2E uses src/dahub-web/e2e rather than a .NET DaHub.Tests.E2E project, and Angular Testing Library is not declared in package.json. Keep these documented Notion acceptance differences visible rather than silently changing the criteria.
- [Notion home](https://app.notion.com/p/412ee4cf5eb48039b603de84b9eb08ad) and the linked [Repo & solution structure record](https://app.notion.com/p/412ee4cf5eb4813c8105da5e93e9e33e) contain the final results and remaining WBS criteria. The API build/Swagger and TDD/coverage tasks are Completed; broader CI/testing records retain their open criteria.

The requirements/ directory remains intentionally Git-ignored. This guide was moved to the repository root as AGENTS.md for publication at the user's request; do not publish other requirements files without explicit authorization.

## 10. Okta Implementation and Current Local Handoff

Guide updated November 10, 2026 (Pacific/Honolulu). The results below were verified November 9, 2026; this documentation update does not constitute a new test run or hosted validation.

### Implemented authentication boundaries

- Direct Okta OIDC authorization code flow with PKCE is implemented in `src/DaHub.API/Authentication/`. Preserve standard signature, issuer, audience, lifetime, state, nonce and correlation validation, plus the exact configured-issuer check. No Entra sign-in or local-password path is enabled.
- Only an existing active `AppExternalIdentities` issuer/subject link with `ProviderKey = 'Okta'`, no revocation timestamp and an active linked `AppUser` may sign in. Never enroll or grant roles by matching email/UPN or importing provider groups. Login updates `LastLoginAt` and appends a `Login` audit event with stable `ActorUserId`.
- `__Host-DaHub.Session` is Secure, HttpOnly, SameSite=Lax and contains an opaque reference to a bounded server-side ticket. The store supports **one API instance** and invalidates sessions on restart/eviction. Preserve 30-minute sliding idle expiry, logout replay protection and per-request local account/link/`AuthorizationVersion` checks. A shared store with atomic revocation/renewal and protected shared Data Protection keys is required before multiple instances.
- Application mutations and POST logout require antiforgery proof; OIDC callbacks retain their middleware state/nonce/correlation protection. API requests receive 401/403 rather than provider redirects. The Angular interceptor preserves the local route when redirecting an expired protected request to sign-in. Keep OAuth tokens out of Angular/browser storage; only the ID token required for provider logout is retained in the server-side ticket.
- Preserve fixed trusted callback/logout origins, local return-URL validation, auth response `no-store` headers and disabled raw OIDC protocol logging. Never enable token/PII or callback-query logging to diagnose failures.

### Configuration and deployment prerequisites

- Follow `docs/authentication.md`. Okta is opt-in through `compose.okta.yaml`; `start-dev.ps1` does not enable it automatically. Unconfigured login returns 503, and enabled configurations with malformed settings/missing secrets fail startup.
- Use the same HTTPS frontend/API origin. For local development, register `https://localhost:4200/api/auth/callback` and `https://localhost:4200/api/auth/signed-out`. The `/api/**` proxy carries both callbacks to the API.
- Store the client secret in the ignored, ACL-protected `.secrets/okta-client-secret` file. Configure only the issuer, client ID and public origin in the ignored `.env`; never place the secret there or in this guide.
- After HTTPS/database setup, use `docker compose -f compose.yaml -f compose.database.yaml -f compose.okta.yaml up -d --build api web` for an explicitly configured tenant. Use the same overlays for subsequent operations. Do not create tenant applications, enroll real identities or alter MFA policy without authorization for that environment.

### Verified local results and dependency remediation

- The full `scripts/verify.ps1` gate passed: 8 backend unit tests, 34 credential-free integration/protocol tests, 8 frontend tests and all 6 Chromium/Firefox/WebKit smoke tests, with builds, lint, formatting and coverage thresholds satisfied. Backend line coverage: API 94.21%, Infrastructure 95.87%, Core 100%.
- The complete `scripts/verify-security.ps1` suite exited 0. Working-tree and fetched-history secret scans found no leaks (11 commits), and NuGet transitive auditing found no advisories. Backend, frontend, SQL and E2E images each reported **zero HIGH/CRITICAL findings** using the refreshed vulnerability database. This does not claim zero lower-severity image findings.
- The initial npm audit reported 10 moderate lint-tool findings from `minimatch` 10.2.6 (CVE-2026-118764 / GHSA-g3hv-75wg-m249). A scoped override of affected 10.x versions to patched **11.0.1**, with the regenerated lockfile, resolved them. A fresh full npm audit reported **zero vulnerabilities**; no advisory suppression, gate reduction or ESLint downgrade was used. Revisit/remove the override when upstream lint dependencies adopt a corrected release.
- After the dependency fix, a clean install, frontend lint/build/formatting, all 8 frontend tests and all 6 browser smoke tests passed again. Frontend coverage: statements 92.85%, branches 89.74%, functions 92.85%, lines 92.68%. A final redacted working-tree secret scan also passed after the documentation update.
- The real SQL migration/runtime-security probes were not rerun in this follow-up; the credential-free gate skips that SQL test. Image scanning does not replace runtime/database-permission validation. No schema, SQL environment or production configuration was changed.

### Remaining work and evidence

- Actual Okta application/redirect/logout registration, assigned users and MFA policy still need tenant configuration and permitted/denied outcome validation. Synthetic-provider tests do not establish live MFA enforcement.
- Validate actual Okta logout, provider-outage local logout, and the complete expired-session -> live reauthentication -> original browser route journey before release. Native Safari on macOS remains unverified.
- Enrollment/admin tooling, controlled audited bootstrap, employee-link verification, local permission policies, broader audit events and production secret/TLS controls remain unfinished. An authenticated session alone must not authorize employee workflows or privileged integrations.
- At the November 9 review, authentication/remediation changes were uncommitted in the local working tree based on `ee99fb4667aa0d91bb1b1eb9dfb55c4c31b615e1`. Prior PR #17 CI results do not validate these new changes. Inspect the current branch, diff, commit and hosted checks before publishing or updating PR/CI claims.
- `README.md`, `docs/authentication.md` and the November 9 section of `docs/development-security.md` contain setup, implementation and verification evidence. Related Notion task statuses and Progress Notes were updated, with live-provider acceptance kept open: [Okta sign-in and local identity links](https://app.notion.com/p/412ee4cf5eb481d293a2e5109ed3230b) and [CI pipeline](https://app.notion.com/p/412ee4cf5eb4817da8c1f9fea9941ab5).

### PR preparation — November 10, 2026

The complete functional and security gates were repeated after remediation and passed with the same test/coverage counts above. Gitleaks scanned 15 fetched commits with no leaks; npm reported zero vulnerabilities and all four images had zero HIGH/CRITICAL findings. This root guide is included in working-tree secret scans. The authentication branch is feature/1.3-okta-server-sessions, stacked on the still-open prerequisite PR #17; merge that prerequisite first and retarget the authentication PR to develop. Record new hosted results separately from these local checks. See the November 10 section of docs/development-security.md for scope and remaining acceptance work.

## 11. RBAC Implementation and PR Handoff — November 10, 2026

- WBS 1.4 resolves the DISTINCT union of active local roles/permissions by AppUsers.Id and caches permission claims in the server-side ticket. Provider groups remain excluded; Admin has no wildcard. Use RequirePermission or Permission:<key> policies for protected operations. Authorization failures return 401/403 ProblemDetails.
- Urgent, Delegation and Retention resource policies enforce both execution/urgency grants plus current immutable request revision/reason, own-manager employee scope or backup-delegation authority, and the separate retention permission. These policies authorize only; workflow writes, audited urgent records/notifications and worker recovery remain future workflow implementation. UI destinations are placeholders.
- Apply migration InvalidateAuthorization before deploying this API. Its six SQL triggers increment affected users' AuthorizationVersion transactionally for assignment/grant changes and role, permission, user or identity changes. EF uses a trigger-compatible write path. Reload tracked versions after authorization mutations. No persistent development/production database or external tenant was changed; SQL verification used a disposable database.
- Stale sessions fail closed on the next protected request and require sign-in. This meets the SRS refresh/deny boundary. WBS 1.4.2.2 additionally asks for changed-permission refresh without re-login; preserve that leaf as open pending implementation or an explicit acceptance reconciliation. Do not silently mark that narrower criterion Completed. Local role/enrollment administration and stable-actor mutation auditing remain separate work.
- Local verification passed: scripts/verify.ps1 (8 unit, 52 credential-free integration/protocol, 12 frontend and 6 Chromium/Firefox/WebKit smoke tests); backend line coverage API 94.56%, Infrastructure 96.76%, Core 100%; frontend statements/branches/functions/lines 96.03%/92.95%/96.29%/95.58%. The SQL-only test passed separately, including actual permission resolution/login persistence, one role join at login, subsequent version checks without repeating that join, invalidation and rollback. scripts/test-dev-sql-security.ps1 passed.
- The complete scripts/verify-security.ps1 suite passed: no source/history secrets (16 locally available commits), no transitive NuGet advisories, zero npm vulnerabilities, and zero HIGH/CRITICAL findings in each backend/frontend/SQL/E2E image. A final redacted source scan passed after the implementation verification documentation. Existing gates, locks and scanner isolation were preserved. Native Safari, live Okta acceptance and multi-instance session support remain unverified/unfinished as documented above.
- RBAC branch: feature/1.4-rbac, based on authentication commit bd6fd8e. PR review base is feature/1.3-okta-server-sessions while prerequisite PR #22 remains open; #22 itself depends on #17. Merge prerequisites in order and retarget RBAC to develop when they land. No merge is authorized by this PR preparation. Record hosted results for the actual RBAC PR head separately from local checks and branch protection. See README.md, docs/authentication.md and the WBS 1.4 section of docs/development-security.md.

- RBAC implementation commit: 53b5d3bbf10c5216fb1311596136fa49a6c13ffe; [draft PR #23](https://github.com/yojana-hawaii/ai-user-onboarding/pull/23) is open against the authentication branch. Initial hosted build/test/format and security jobs started on that commit; this statement does not claim they passed. Documentation follow-ups may advance the head: inspect current-head checks before updating hosted acceptance. Notion WBS 1.4 retains the no-re-login-refresh acceptance difference as open.
