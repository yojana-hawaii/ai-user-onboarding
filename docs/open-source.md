# Open-source preparation and organization seed packages

DaHub ships neutral employee-lifecycle UI text, generic application roles/permissions and synthetic examples. Organization catalogs are loaded explicitly after installation. This is a single-organization deployment; the upload does not add multitenancy or live provider provisioning.

## Private organization data

Copy `examples/organization-seeds.example.json` to the Git-ignored `organization-data/` directory and replace its synthetic values with approved catalog data. Keep the directory ACL-restricted and backed up under your organization's controls. Git ignore prevents ordinary adds; it does not encrypt files or prevent forced adds. Do not attach operational packages to issues, PRs or public build artifacts.

The authorized migration sanitization preserved the original eight locations in `organization-data/organization-seeds.json` locally. This private file is not part of the public distribution. Existing installed databases retain their records; the importer does not remove or replace them.

Never include passwords, tokens, connection strings, patient information, employees, application users, external identity links, role assignments or approval/workflow records in a package. Unknown JSON fields are rejected, but a schema cannot detect a secret or personal information placed in an allowed description; review packages before upload. Credentials remain in protected secret sources described in authentication.md and api-foundation.md.

## Upload and apply

1. Sign in using an existing authorized local identity. Both active `templates.manage` and `users.manage` permissions are required. The default Admin role has both; neither the role name nor provider groups confer authority.
2. Open **Organization seed data** from Home, or `/admin/organization-seeds`.
3. Choose a UTF-8 JSON package of at most 512 KiB and select **Preview package**.
4. Review the addition/unchanged counts. Validation rejects conflicting rows before offering Apply; no catalog writes occur during preview.
5. Select **Apply additions** within ten minutes. The API rechecks the package, user, preview expiry, catalog state and current authority. Concurrent catalog changes require a fresh preview.

An existing inactive row, changed value, case-only key change or ambiguous key is a conflict, not permission to overwrite. Resolve conflicts through controlled catalog maintenance and create a fresh preview. A successful identical reimport adds nothing. Missing package rows never delete or deactivate stored rows. Applying catalogs does not create external accounts, grant memberships/licenses or verify EHR work.

The first-admin bootstrap/enrollment UI remains unfinished. This importer cannot bootstrap an unknown user, grant permissions or bypass that boundary. Existing installations must use their separately authorized enrollment process before this page can be used.

## Package contract (schemaVersion 1)

All arrays are optional and default to empty; null arrays are invalid. At least one and at most 1000 total catalog rows are allowed, excluding template groups. Unknown or duplicate JSON fields, null rows, surrounding whitespace, control characters and duplicate keys ignoring case are rejected. Optional text may be omitted/null; when supplied it must be nonblank. Catalog keys are stable within this import contract: use them unchanged across packages. Renaming keys is a separate administrative action; the importer cannot infer renames.

| Array | Key | Fields |
| --- | --- | --- |
| locations | siteCode (20) | name (150), optional address (300), phone (30) |
| departments | name (100) | ouPath (500) |
| jobTitles | title (150) | ehrAccessDefault (boolean, defaults false) |
| specialties | name (150) | optional description (500) |
| templates | name (150) | m365LicenseSku (100), optional description (500), department/location/jobTitle/specialty reference keys, ehrAccessRequired (boolean, defaults false), groups |

Numbers in parentheses are maximum character lengths. Template `location` references SiteCode; other criteria reference their respective catalog keys. Omitted/null criteria mean any. References must identify an active stored catalog row or a row in this package. Ambiguous legacy catalog names require manual reconciliation, even when not selected by the package. Required department OU paths and template license SKUs follow the existing model; this change does not relax those constraints.

Each template permits at most 100 groups, identified by `(groupType, groupIdentifier)`. Group fields are groupType (`AD_Security`, `AD_Distribution` or `Entra_Group`), groupName (300), groupIdentifier (500), and isRequired (boolean, defaults false). Omitted/null groups mean no groups. Groups compare independently of ordering. Group identifiers, OU paths and license SKUs are stored configuration; this importer does not contact providers to verify their existence or suitability. Confirm them operationally before enabling provisioning.

## API and transaction behavior

`POST /api/admin/organization-seeds/preview` accepts the package as `application/json` and returns `{ summary: { added, unchanged }, previewToken }`. `POST /api/admin/organization-seeds/apply` accepts the same package and the `X-Seed-Preview` header. Both endpoints require a current server-side session, both local permissions and the standard `X-XSRF-TOKEN` antiforgery proof. Responses disable caching. Validation errors are safe indexed messages and never echo uploaded values.

Preview receipts are Data Protection-protected, expire after ten minutes and bind the actor ID, canonical package digest and full catalog-state digest. An API restart/key change can invalidate receipts; preview again. Apply recomputes the plan under SQL Server serializable isolation and rechecks authority before writes. Lookup/template/group writes and the append-only `OrganizationSeedsImported` event commit together. The event contains stable ActorUserId, package SHA-256 and counts, not raw package contents. Identical no-op reimports create no new mutation event. SQL failures/deadlocks roll back the entire transaction; start again with Preview rather than retrying a partial write. The API's existing one-instance session limitation still applies.

## Migration compatibility and release boundary

With explicit authorization, only the organization-location insertion block was removed from `20261108030652_CompleteFirstReleaseSchema`. Its migration ID, schema operations, role/permission seeds and later migrations remain intact. Existing databases that recorded this migration will not rerun it. No cleanup migration or automatic deletion was added. Fresh installs and full disposable rollback/reapply now leave organization lookup catalogs empty; tests supply synthetic locations explicitly. No persistent database migration was executed for this cleanup.

Working-tree cleanup does not sanitize prior Git commits, tags, branches, PR discussions, CI logs, releases or private connected documents. Before making a repository public, choose a clean public source snapshot or separately authorize a coordinated history cleanup and review all other publication surfaces. Keep the ignored requirements private until sanitized publication is explicitly authorized. No history rewrite, remote visibility change, push, PR, merge or deployment is part of this implementation.

The existing GPL v3 license text is preserved. Ownership and intended licensing must be confirmed before release; no legal/compliance certification is claimed. Historical verification notes retain dates, commit IDs and open acceptance but private repository/workspace URLs have been redacted. Those historical checks do not validate this change. Current verification results belong in development-security.md.
