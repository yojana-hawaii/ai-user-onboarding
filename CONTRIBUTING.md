# Contributing

Use feature branches named `feature/<task-id>-<description>` and reviewed pull requests into develop. Protect develop and main on the selected Git host. Branch protections are not configured locally.

For application behavior: write a failing test, implement the smallest correct change, refactor, and run the relevant checks. Use `*Tests.cs` for backend tests and `*.spec.ts` for Angular component tests. Keep domain unit tests in DaHub.Tests.Unit; API host and later isolated SQL tests belong in DaHub.Tests.Integration. Keep Playwright browser tests in src/dahub-web/e2e; the same smoke suite runs against Chromium, Firefox and WebKit through scripts/test-e2e.ps1 and CI.

Use xUnit assertions and Angular TestBed. Add mocking libraries only when an implemented boundary requires one. Mock external directories, M365, ticketing, and notifications by default; use synthetic employee identities and never patient PHI. SQL integration tests must use an isolated test database, never production.

Security work needs tests for unauthorized access, identity collisions, revoked permissions, stale revisions, concurrent execution, partial failures and recovery as applicable. Do not substitute UI visibility tests for server-side authorization tests.

Run `./scripts/verify.ps1` before submitting changes once dependencies are installed. Verification enforces at least 80% line coverage per backend application assembly and 80% frontend statements, branches, functions and lines. See README for the exact scope and generated-code exclusions. Tests that need SQL run separately through scripts/test-dev-database.ps1 against a disposable database.
