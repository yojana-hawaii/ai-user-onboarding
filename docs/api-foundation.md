# API foundation — WBS 1.5

The API uses .NET 10 and its existing server-side Okta session boundary. This foundation adds no employee workflow, role administration, directory mutation or production deployment.

## Errors and input validation

Unhandled exceptions, malformed minimal-API parameters, FluentValidation `ValidationException`, and empty 4xx/5xx responses return `application/problem+json` with `type`, `title`, `status`, a safe `detail`, and `correlationId`. Authentication/authorization keep their 401/403 semantics. Antiforgery failures return 400. Validation exceptions map to 400 without reflecting attempted input, validator messages or exception details. Future form endpoints must explicitly invoke their validators before side effects; this foundation does not claim form validation for unimplemented workflows.

Every response returns `X-Correlation-ID`. A single supplied value of 1–64 ASCII letters, digits, hyphens or underscores is accepted; other values are replaced. This identifier supports diagnostics only and never grants trust or access.

## Operational logging

Serilog writes JSON to console and daily/size-rolled `api-*.jsonl` files. Each request completion records `CorrelationId`, stable local `ActorUserId`, local `ActorUPN` snapshot, HTTP method, route template (`RequestPath`) and status. Anonymous requests use `anonymous`. Unknown paths are `(unmatched)`; route templates avoid putting employee IDs or arbitrary URL text into logs.

Only events explicitly marked `DaHubSafeEvent` are emitted. Framework, SQL and OIDC diagnostic events are excluded because they can contain SQL, credentials, callback queries or raw provider errors. The exception handler suppresses its raw exception diagnostics. Request bodies, query strings, authorization headers, cookies, tokens and exception text are not recorded. New operational events must deliberately supply safe fields; do not mark raw framework events safe. Operational request logs are separate from the append-only business audit log.

`Logging__Directory` selects a service-writable directory. Startup checks write access and fails if unavailable. Local Compose uses `/logs`, a 160 MiB private tmpfs, with 10 MiB rolls and at most 14 files; it is lost when the container is replaced. Other development/test hosts default to the temporary `dahub-logs` directory. These limits are operational defaults, not the business audit retention policy. Production must override the path to restricted, persistent storage and monitor disk space, sink failures and centralized collection; the file sink cannot guarantee durable audit delivery.

## Dependency health

- `/api/health/live`: anonymous process liveness, independent of external services.
- `/api/health/ready`: anonymous SQL connectivity **and migration readiness**; 503 ProblemDetails when unavailable.
- `/health` and `/api/health`: aggregate SQL, AD and Entra status. All dependencies must pass for 200. Unavailable/unconfigured dependencies return 503 ProblemDetails with only the named check statuses. Responses are `no-store`; hostnames, connection strings and exception details are excluded.

`DirectoryHealth__Mode` is `Unconfigured` by default (unhealthy), `Mock` for isolated Development (reported as `Mock`, never proof of connectivity), or `Live`. Mock mode is rejected outside Development. Local Compose explicitly selects Mock. Live mode requires `DirectoryHealth__AdHost`, optional `DirectoryHealth__AdPort` (636), and a GUID `DirectoryHealth__EntraTenantId`. Each probe has a five-second deadline and honors shutdown/request cancellation. The AD probe validates an LDAPS TLS handshake, hostname, certificate trust and revocation; it does not bind, query employees or prove provisioning rights. The Entra probe sends a credential-free HTTPS GET to the tenant's OIDC metadata endpoint, without redirects or cookies. It proves metadata reachability, not Microsoft Graph authorization or M365 provisioning. Worker-specific authenticated health remains part of the future integrations.

No real directory endpoints are configured by this implementation. Validate live connectivity, certificate trust, firewall access and failures in the authorized environment before release.

## Secrets and deployment checklist

Standard ASP.NET configuration loads Development user secrets for ID `dahub-api-development` and environment variables. Never run `user-secrets list` on real credentials in captured output. In Docker development prefer the existing ignored, ACL-protected secret files and per-service read-only mounts; user-secrets are a local development convenience, not encrypted storage. Never mount a full developer profile into the API container.

| Configuration/environment name | Purpose |
| --- | --- |
| `ASPNETCORE_ENVIRONMENT` | `Development` locally; `Production` for deployment |
| `AllowedHosts` | Exact deployment host allowlist |
| `Database__ConnectionStringFile` | Preferred protected SQL connection file |
| `ConnectionStrings__DaHub` | SQL connection alternative from injected environment/user secrets; no tracked credentials |
| `Okta__Enabled`, `Okta__Issuer`, `Okta__ClientId`, `Okta__PublicOrigin` | Trusted tenant and HTTPS callback origin |
| `Okta__ClientSecretFile` **or** `Okta__ClientSecret` | Exactly one protected file or injected configuration secret; ambiguous sources fail startup |
| `Logging__Directory` | Restricted persistent production operational log directory |
| `DirectoryHealth__Mode`, `DirectoryHealth__AdHost`, `DirectoryHealth__AdPort`, `DirectoryHealth__EntraTenantId` | Read-only directory probes described above |
| `Kestrel__Certificates__Default__Path`, `Kestrel__Certificates__Default__KeyPath` | Provisioned TLS certificate and private-key paths |
| `Https__Port` | Redirect destination port; 443 by default, 5081 for local API Compose |

1. Provision the dedicated runtime DB identity, verified SQL TLS, required migrations, secret-file ACLs and trusted Okta enrollment through the existing controlled processes. This task does not apply a persistent migration or configure a tenant. See [authentication](authentication.md) and [database foundation](database-foundation.md).
2. For native Windows hosting, the checked-in Production configuration binds HTTP 80 for a 301 redirect and HTTPS 443 for application traffic. Provision a valid certificate through the environment variables above and restrict private-key access to the service identity. When using IIS, configure its HTTPS binding, HTTP redirect and Windows TLS policy too; Kestrel settings do not establish IIS TLS policy. No IIS/site/binding is created by this change.
3. Keep same-origin HTTPS frontend/API hosting, exact `AllowedHosts`, trusted callback origins and a production frontend host. Kestrel permits only TLS 1.2/1.3. HTTPS responses, including errors, include `Strict-Transport-Security: max-age=31536000; includeSubDomains`. Verify subdomains are ready before deployment. Do not trust arbitrary forwarded headers; configure any reverse proxy and its trust boundary explicitly before use.
4. `Https__AllowDevelopmentHttp=true` bypasses redirection only in Development for the unpublished isolated E2E API. It has no effect outside Development. Normal local development remains HTTPS. Never deploy the E2E overlay or Angular development server.
5. Verify actual HTTP→HTTPS behavior, certificates, rejection of TLS 1.0/1.1, HSTS, no mixed content, live MFA/logout/session expiry, and directory outage recovery on the deployment host. Synthetic and in-process tests do not establish Windows/IIS, native Safari or production acceptance.

Package references: [Serilog ASP.NET integration](https://www.nuget.org/packages/Serilog.AspNetCore/10.0.0), [rolling file sink](https://www.nuget.org/packages/Serilog.Sinks.File/7.0.0), [FluentValidation](https://www.nuget.org/packages/FluentValidation/12.2.0).
