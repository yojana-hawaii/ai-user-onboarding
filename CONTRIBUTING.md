# Contributing

Use feature branches named `feature/<task-id>-<description>` and reviewed pull requests into develop. Protect develop and main on the selected Git host. Branch protections are not configured locally.

For application behavior: write a failing test, implement the smallest correct change, refactor, and run the relevant checks. Use `*Tests.cs` for backend tests and `*.spec.ts` for Angular component tests. Keep domain unit tests in DaHub.Tests.Unit; API host and later isolated SQL tests belong in DaHub.Tests.Integration. E2E tests will be added with executable workflows.

Use xUnit assertions and Angular TestBed. Add mocking libraries only when an implemented boundary requires one. Mock external directories, M365, ticketing, and notifications by default; use synthetic employee identities and never patient PHI. SQL integration tests must use an isolated test database, never production.

Security work needs tests for unauthorized access, identity collisions, revoked permissions, stale revisions, concurrent execution, partial failures and recovery as applicable. Do not substitute UI visibility tests for server-side authorization tests.

Run `./scripts/verify.ps1` before submitting changes once dependencies are installed. The 80% Core/API coverage gate required by WBS 1.1.5.4 remains pending until coverage tooling and CI are configured. Passing this scaffold's checks does not satisfy that unfinished requirement.
