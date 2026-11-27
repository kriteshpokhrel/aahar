# Aahar

Aahar is a locally deployed restaurant operations application in development,
using ASP.NET Core, PostgreSQL and React. It supports dine-in and takeaway
orders, kitchen tickets, inventory, guest requests and sales reports.
Payments and receipts are simulated and are not IRD certified.

- [Implementation book and branding kit](docs/implementation-book.md)
- [Guidance for coding agents](AGENTS.md)
- [Backend setup, API contract and limitations](Aahar-API/BACKEND.md)
- [Frontend development and limitations](web/README.md)

## Local development

Use .NET 10, Node.js 24 and PostgreSQL. Create a dedicated empty database and
follow the [backend setup](Aahar-API/BACKEND.md) to configure the connection,
review and apply migrations, create an administrator and optionally seed
fictional menu and inventory data. Databases are never created or migrated
automatically. Credentials are configured locally, not committed.

The backend runs at `https://localhost:7041`. Start the frontend as described in
the [frontend guide](web/README.md); it needs an HTTPS certificate for real
sign-in and proxies `/api` to the backend. A local QR cannot be used from a
phone. The sample workspace is in-memory only and does not use PostgreSQL.

The implementation book distinguishes release targets from completed work.
Live PostgreSQL migration and selected workflow and concurrency checks have
been exercised on dedicated local databases; a full deployment, security and
accessibility audit is still outstanding.
