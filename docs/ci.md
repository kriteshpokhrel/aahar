# Continuous integration and merge protection

The [CI workflow](../.github/workflows/ci.yml) runs on pushes, pull requests
targeting `main`, and merge-queue groups. Both jobs run on every change, including
documentation changes: path filters must not leave required checks pending.

| Required check | What it covers |
| --- | --- |
| `Backend build and tests` | .NET 10 restore, Release build with warnings treated as errors, EF model/migration consistency, fresh database migration, and the full backend test suite |
| `Frontend build and tests` | Locked npm installation, lint with warnings rejected, TypeScript/Vite production build, and Node tests |

## Database isolation

The backend job creates its own PostgreSQL 18 service and an empty `aahar_test`
database. It applies migrations there and enables the existing PostgreSQL
workflow and round/concurrency tests. It never connects to an operator's local
database or changes the unapproved local schema.

Passwordless `trust` authentication is limited to this disposable CI service,
published on the runner's loopback interface. It is not a deployment
configuration and must not be copied to a real restaurant database. No database
password or application credential is stored in the workflow.

Backend TRX results are uploaded for seven days, including when tests fail.
These contain test-generated data only. A missing artifact after an earlier
build failure does not turn that failed job into a success.

## Workflow safety and maintenance

Official GitHub actions are pinned to immutable commits. The workflow token has
read-only repository content access, checkout does not retain Git credentials,
and untrusted pull requests do not run through `pull_request_target`. New pushes
cancel superseded runs for the same event/ref.

Keep job names stable: branch requirements depend on their exact check names.
If a job is renamed, update the GitHub ruleset with the new verified check name.
Do not use `continue-on-error`, skip database tests in CI, or replace failures
with successful fallback checks.

The local EF tool is pinned in [the tool manifest](../.config/dotnet-tools.json).
Use `dotnet tool restore` rather than relying on an arbitrary global EF version.
Ordinary local `dotnet test` does not opt into PostgreSQL tests unless their
environment variables are explicitly set for an approved, dedicated test
database. See [backend setup](../Aahar-API/BACKEND.md) for the local safety rules.

## GitHub merge requirements

Repository settings are separate from workflow YAML. The intended `main`
policy requires a pull request, both successful GitHub Actions checks, and
up-to-date checks against the target branch. Non-fast-forward updates (force
pushes) are blocked. No bypass actors are configured for the CI requirement.
Existing default-branch review and update restrictions remain in place.

Do not describe this policy as active until the hosted workflow has passed and
the live GitHub ruleset has been configured and read back successfully.
