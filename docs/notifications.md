# Core notification delivery — WBS 1.8

`INotificationService.EnqueueAsync` persists a `NotificationRetry` job before any
external send. The separate worker handles the initial attempt and up to three
retries, with 1/4/16-minute backoff. A later provider `Retry-After` takes precedence.
Each actual attempt creates a separate NotificationLog row with its ordinal
AttemptCount. A successful retry never overwrites earlier failure evidence.

The API registers only the queue service; the worker registers delivery transports.
Callers must authorize the originating action and enqueue within the workflow
transaction when intent and workflow changes must commit together. No public send
endpoint is exposed. Lifecycle event routing, current-revision checks and
duplicate-event suppression remain later integration work. Notices carry
information, never approval authority. Delivery failure cannot
block access disabling or resolve pending approval/EHR work.

## Channels and configuration

All four channels default to `Mock`. Mock sends fail visibly without network I/O
or a delivery timestamp; they never fabricate successful delivery. SMS is currently
provider-neutral and mock-only, as explicitly selected by the user. Live SMS,
consent/opt-out rules, approved recipients and receipt reconciliation await a provider
decision. SMS content must exclude sensitive employee details and PHI.

Live configuration is opt-in on the **worker**, using `Notifications:<channel>:Mode=Live`.
Do not enable it in the default Compose file. Supply only the secrets/network access
that this worker needs through a separately reviewed deployment overlay. Existing
worker mounts contain only database credentials; this implementation adds no live
secret mounts or tenant configuration.

- Teams: `Notifications:Teams:Destinations:IT:UrlFile` and/or `...:HR:UrlFile`
  identify protected files containing approved Teams Workflows webhook URLs.
  Jobs and logs contain destination aliases (`IT`, `HR`), never webhook URLs.
  HTTPS on port 443 is mandatory; only `*.logic.azure.com` and
  `*.environment.api.powerplatform.com` hosts are accepted. Review the exact tenant
  endpoint and workflow owners/co-owners before deployment. Adaptive Cards are
  posted using the Workflows attachment envelope. Legacy connector webhooks are
  retired; the user selected Workflows for this implementation.
- Slack: `Notifications:Slack:Destinations:IT:UrlFile` (or another approved alias)
  identifies a protected incoming-webhook file. Only `hooks.slack.com` over HTTPS
  on port 443 is accepted. The webhook fixes the destination; payloads cannot
  override the channel. Plain text payloads disable Markdown and link unfurling.
- Email: configure `Notifications:Email:Host`, `Port` (default 25), `From`, and
  `Recipients:0`, `Recipients:1`, etc. Only exact approved recipient addresses can
  be sent. The transport uses mandatory STARTTLS, standard certificate validation,
  and an anonymous Exchange connector relay; it does not implement password-based
  SMTP submission. Network/connector authorization must be approved separately.
  Notification bodies are plain text rendered as HTML with encoding.

Webhook HTTP clients disable redirects and cookies and bypass URI-logging handlers.
Never enable raw SMTP, HttpClient, EF or provider diagnostics on this worker. Secret
files require restricted ACLs and approved deployment storage. Persist no credentials,
password links/tokens, raw provider errors or PHI in payloads, logs or messages.

## Worker failure alerts

Set `Notifications:WorkerFailure:Teams` or `Notifications:WorkerFailure:Slack` to
an approved IT alias, and/or set `Notifications:WorkerFailure:Email` to an approved
IT mailbox or distribution address. Configure one destination per selected channel.
With Live delivery, the configured destination must also appear in that transport's
approved destinations or recipients. An empty section leaves alert routing disabled.
Restart the worker to apply configuration changes. Queued alerts for destinations
that have been removed or replaced fail without sending. Each alert body contains
only the job ID, attempt number and a prompt to review stored evidence. Notification
job failures emit critical events and do not enqueue further alerts.

## Evidence and failure semantics

NotificationLog is written as Failed/ReconciliationRequired **before** network I/O.
If storage is unavailable, nothing is sent. Confirmed upstream acceptance sets Sent
and DeliveredAt: SMTP acceptance, Slack HTTP 200 with exact `ok`, or Teams Workflows
2xx acceptance. This is not proof of inbox receipt, channel rendering or a user's
reading the notice. Workflows can accept a request before downstream posting fails;
monitor the workflow's own run history and verify actual IT/HR channel delivery.

HTTP 429/503 and explicit SMTP 4xx failures are retryable; terminal rejections stop.
HTTP 5xx other than 503, network exceptions, timeouts, cancellations and ambiguous
acknowledgements require reconciliation. Their raw responses are never stored.
Final retry exhaustion remains Failed. Interrupted jobs retain their pre-send log
and existing worker attempt evidence. A crash after remote acceptance but before
local persistence is uncertain; reconcile before any manual requeue. No external
exactly-once guarantee is claimed. Historical attempt rows remain unchanged by later
retries. Separate channel jobs ensure one failed channel cannot suppress another.

Apply `NotificationChannels` after `WorkerAttempts` before deploying this code. It
only expands the channel check constraint to Email/Teams/Slack/SMS. Its rollback
intentionally fails if Slack/SMS rows exist, preserving delivery history rather
than deleting evidence. No production/persistent migration or live send is
authorized or performed by implementation/testing.

Protocol references: [Teams Workflows webhooks](https://learn.microsoft.com/en-us/microsoftteams/platform/webhooks-and-connectors/how-to/add-incoming-webhook),
[Slack incoming webhooks](https://docs.slack.dev/messaging/sending-messages-using-incoming-webhooks/),
[Slack rate limits](https://docs.slack.dev/apis/web-api/rate-limits/),
[Exchange Online relay](https://learn.microsoft.com/en-us/exchange/mail-flow-best-practices/how-to-set-up-a-multifunction-device-or-application-to-send-email-using-microsoft-365-or-office-365).

Live Teams/Slack/SMTP acceptance remains open until approved configuration and
synthetic recipient/channel checks are available. Mock/protocol tests do not satisfy
those criteria. SMS provider selection and live delivery are explicitly deferred.
