# Okta sign-in and application sessions

DaHub uses the ASP.NET Core OpenID Connect handler with authorization code flow and PKCE. Only the configured Okta issuer is trusted. The handler validates signed ID tokens, audience, lifetime, state, correlation and nonce. Application access requires an existing active `AppExternalIdentities` row with `ProviderKey = 'Okta'`, an exact issuer/subject pair, no revocation timestamp, and an active linked `AppUser`. Email, UPN, provider groups and provider roles cannot enroll users or grant privileges.

After complete protocol validation, login updates `LastLoginAt` and appends a `Login` audit event with stable `ActorUserId`. The session principal contains local user ID, local identity-link ID, display name, authorization version and locally resolved permission keys. Each authenticated request rechecks account/link activity and `AuthorizationVersion` in SQL. A mismatch invalidates the session immediately. Database failures deny access through the exception handler; no cached authorization fallback is used. Local permission and resource policies are implemented for WBS 1.4; enrollment administration remains subsequent work; an authenticated session alone must never authorize employee provisioning.

## Configure a development tenant

1. An Okta administrator creates an **OIDC Web Application** using authorization code flow and a client secret. Assign only intended users. Configure its application sign-in policy to **require MFA**; do not create a password-only policy exception. MFA enforcement is an Okta policy requirement and must be verified in that tenant before release.
2. Register exactly `https://localhost:4200/api/auth/callback` as sign-in redirect URI and `https://localhost:4200/api/auth/signed-out` as sign-out redirect URI. These are frontend-origin URLs: Angular proxies `/api/**` to the backend, including callbacks. Use the deployment's HTTPS origin in other environments.
3. In the ignored root `.env`, set `DAHUB_OKTA_ISSUER` to the exact issuer in the administrator's trusted metadata, `DAHUB_OKTA_CLIENT_ID` to the client ID, and optionally `DAHUB_PUBLIC_ORIGIN` (default `https://localhost:4200`). Do not put the client secret in this file. Place it in `.secrets/okta-client-secret` with ACLs restricted to the current user and SYSTEM, following the database-secret storage convention. Never commit or print its contents.
4. Pre-enroll a verified issuer/subject link to an existing local user through an authorized, audited deployment/enrollment process. Do not infer a subject from email or automatically bootstrap an administrator at login. This change supplies no enrollment endpoint or bootstrap backdoor.
5. After HTTPS and database setup, start with the opt-in overlay:

```powershell
docker compose -f compose.yaml -f compose.database.yaml -f compose.okta.yaml up -d --build api web
```

Use the same overlays for subsequent restarts. `start-dev.ps1` does not enable Okta automatically. Without the overlay/configuration, login returns 503 and existing health/scaffold tools continue to work. When enabled, missing secrets or malformed settings fail startup. Production must explicitly enable/configure Okta, configure `AllowedHosts` for its hostname, and serve the frontend and API under the same HTTPS origin. `PublicOrigin` fixes callback and return origins independently of request Host headers. Do not enable forwarded-header trust for arbitrary proxies.

## Session and CSRF boundaries

- `__Host-DaHub.Session` is Secure, HttpOnly, SameSite=Lax, path `/`, without a Domain or persistent expiry. It holds a protected random session reference. ID tokens used for Okta logout remain in the server-side ticket; access and refresh tokens are discarded. Angular receives no OAuth tokens and uses no browser token storage.
- The bounded session store is **process-local**, supports **one API instance**, and deliberately invalidates all sessions on process restart or eviction. This deployment does not support multiple replicas or sessions surviving restarts. A shared store with atomic renewal/revocation and shared protected Data Protection keys must replace it before introducing multiple API instances. Do not add sticky routing as a substitute for shared revocation.
- Expiry slides on each authenticated request, with rejection at 30 minutes of inactivity. Closing the browser does not provide a revocation guarantee; use logout. Logout removes the server ticket before attempting Okta logout, so local revocation survives provider failure and a copied old cookie cannot restore it. Concurrent renewal cannot recreate a removed session.
- `GET /api/auth/session` returns local ID, display name and permission keys, or 401. API challenges return 401/403 without redirecting XHR to Okta. The Angular interceptor sends expired protected requests to `/api/auth/login` with the current local route. Login return URLs must be rooted local paths, excluding network paths, backslashes and control characters.
- `GET /api/auth/antiforgery` requires a valid session and returns a request proof paired with a Secure, HttpOnly, SameSite=Strict antiforgery cookie. Mutating `/api/**` requests require `X-XSRF-TOKEN` or the standard hidden form field. The OIDC middleware consumes its callbacks before application CSRF middleware and validates its own state/nonce/correlation. Logout is a POST form with this proof; GET cannot sign out. All auth responses use `no-store`.
- Provider logout returns only through the registered signed-out callback and a protected state containing the fixed application origin. Unsolicited provider front-channel logout is disabled; local account/link revocation is checked on the next request.

Never log HTTP bodies/cookies, authorization headers, callback query parameters or raw provider responses. Protocol-handler logging is disabled because provider error descriptions and protocol exceptions may contain sensitive response data. Preserve this filter, the configured ASP.NET request logging level and default IdentityModel PII suppression. Production TLS termination, protected secret/key storage and log retention need deployment validation.

## Verification and release checks

`scripts/verify.ps1` runs signed synthetic OIDC protocol tests (including issuer, audience, signature, expiry, nonce and state rejection), local identity/revocation tests, cookie/CSRF/logout replay tests, session-store time tests, UI/interceptor tests and three-engine browser smoke tests. These use an isolated in-memory test database and a simulated provider; they do not contact Okta or establish live MFA enforcement. Existing SQL schema tests separately validate the binary issuer/subject collation.

Before release, an administrator must validate a permitted MFA sign-in and denied policy outcome in the configured Okta tenant, unknown and revoked identity rejection, actual redirect/logout registration, logout after provider outage, expiry/return routing and production TLS/secret controls. No tenant registration, real credentials, production enrollment or live MFA validation is performed by this implementation.

Framework reference: [ASP.NET Core OIDC web authentication](https://learn.microsoft.com/aspnet/core/security/authentication/configure-oidc-web-authentication). The implementation retains the framework's state, nonce and token validation rather than implementing a custom OAuth protocol.

## Local RBAC — WBS 1.4

Login resolves a DISTINCT union through active `AppUsers`, `AppUserRoles`, active `AppRoles`, `AppRolePermissions` and active `AppPermissions`, keyed only by `AppUsers.Id`. The permission claims live in the existing server-side ticket. Requests use that cached union after validating the current account, identity link and authorization version; they do not repeat the role-table join. The session response exposes permission names for presentation, never provider credentials or tokens.

Apply `[RequirePermission("requests.submit")]` to an API action, or `.RequireAuthorization("Permission:requests.submit")` to a minimal endpoint. Permission keys are exact and additive; Admin has no implicit wildcard. Authorization middleware returns ProblemDetails with 401 for missing/currently invalid sessions and 403 for missing permission. Existing anonymous health and Development documentation endpoints remain anonymous.

Resource-aware policies are invoked through `IAuthorizationService.AuthorizeAsync(user, resource, policyName)`:

- `Urgent` takes `UrgentAction(requestId, revision, justification)`. It requires both `provisioning.execute` and `provisioning.urgent`, a nonblank reason and an existing immutable current revision of a pending urgent onboarding/offboarding request awaiting execution. Ordinary IT/Admin and UrgentOperator alone fail. Authorization does not approve or execute the request.
- `Delegation` takes `DelegateAssignment(managerEmployeeId)`. `approvals.delegate.manage` permits backup assignment; `approvals.delegate.own` requires the caller's verified local employee link to match the manager. No UPN/email match grants scope.
- `Retention` requires `retention.manage`, assigned to IT in the default matrix; Admin alone does not receive it.

Resource policies recheck local session authority. Future workflow handlers/workers must call the appropriate policy immediately before a privileged side effect and bind their transactional execution to the same immutable revision/employment period. Urgent execution must additionally persist actor/time/reason and audit evidence and send notifications without changing approval to Approved. This RBAC work adds no workflow mutation, provider action, role-administration/enrollment API or bootstrap mechanism. Their audit and recovery requirements remain open.

Migration `InvalidateAuthorization` adds transactional SQL triggers. Assignment insert/update/delete, role-grant insert/update/delete, role activity, permission activity/name, account activity/employee link and identity update/delete advance affected users' authorization versions. The update uses `AuthorizationVersion + 1` under SQL locking, so concurrent updates cannot overwrite increments. A rollback also rolls back invalidation. Direct SQL maintenance receives the same invalidation; authorized maintenance must still record stable-actor audit evidence. EF disables its direct SQL OUTPUT clause on the six trigger-bearing tables. Versions held by already tracked EF entities can be stale after trigger-bearing writes: reload before establishing a new session or returning a version.

Apply the checked-in migration through the established database migration process before deploying this API. The implementation task does not migrate the developer's persistent database or configure a tenant. Tests migrate only a disposable synthetic database. Existing sessions cannot acquire new claims until a new sign-in; affected sessions are denied on their next protected request rather than silently refreshed.

Angular fetches the current session when entering a permission route, directs anonymous visitors to sign-in with their local target, sends denied users to `/forbidden`, and filters home navigation by returned permission keys. Requests, Provisioning and Audit pages are explicitly workflow placeholders. These UI checks never replace API authorization.
