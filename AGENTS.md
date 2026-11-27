# Agent guide

## Project intent

Aahar is a reusable restaurant operations product, deployed for one restaurant
per local installation. It is not a custom design for a particular restaurant.
Read [the implementation book](docs/implementation-book.md) before changing
product behavior; distinguish planned capabilities from implemented ones.

## Working agreements

- Implement the agreed phase in small, verifiable vertical slices. Keep the
  feature inventory, phase status and user-facing documentation current.
- Respect clean architecture dependency direction: domain knows no framework;
  application depends on domain; infrastructure and API depend inward.
  Keep HTTP, EF Core and mapping details out of domain logic.
- Use interfaces at meaningful boundaries and dependency injection. Apply SOLID
  without adding generic abstractions that obscure behavior. Use Mapster for
  DTO mapping, not for business decisions.
- Money uses decimal arithmetic and explicit rounding; preserve historical
  price snapshots. Treat order, stock and payment changes as transactional
  business operations, not independent CRUD updates.
- Enforce authorization on the server. Validate input, use parameterized
  database access, return actionable errors and avoid logging secrets.
- No real payment processing or claims of Nepal IRD compliance. Clearly label
  simulated payments and receipts.
- Localhost is not reachable from a guest's phone. Do not claim that a QR code
  pointing at localhost supports phone ordering.
- Never store credentials or personal data in documentation, source control,
  seed files or test fixtures. Never create, migrate, reset or delete an
  existing database without explicit permission. Use a dedicated test database.
- Preserve existing user changes. Prefer focused tests, including integration
  tests for persistence and authorization, and verify affected behavior.
- Document architecture decisions, operations, security limitations and changes
  to setup alongside the code. Keep examples executable and truthful; do not
  invent commands or mark work done until verified.

## Current baseline

The .NET 10 backend foundation uses PostgreSQL, cookie authentication, EF Core
and Mapster. The older SQL Server scaffold is retained but excluded from
compilation. Consult [backend setup and limitations](Aahar-API/BACKEND.md):
new-database migrations and selected PostgreSQL workflow and race tests have
been verified; full end-to-end, upgrade and recovery coverage remains incomplete.
