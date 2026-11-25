# Aahar backend

Single-restaurant, localhost-only portfolio backend: .NET 10, PostgreSQL, EF Core, Mapster, cookie authentication, and antiforgery. `Aahar.Domain` holds entities and rules; `Aahar.Application` defines contracts; `Aahar.Infrastructure` implements persistence and services; `Aahar-API` is the HTTP host. The original SQL Server scaffold under `Aahar-API/Data`, `Models`, and `Repository` is retained for reference but excluded from compilation.

## Setup

No database is created or migrated automatically. The initial migration is for a **new, dedicated, empty PostgreSQL database**; it is not an upgrade or data migration from the old SQL Server scaffold. Review `Aahar.Infrastructure/Migrations/*_InitialPostgres.cs` and the generated SQL before applying it. `Down` drops tables and is destructive. Do not apply it to an existing database or run rollback commands without an explicit decision and backup.

With .NET 10 installed and a dedicated database already created by its owner, configure a connection string privately (do not enter passwords in chat or commit them). For example, in a local bash terminal, prompt without echo and avoid putting the value in shell history:

```bash
read -r -s -p 'Dedicated PostgreSQL connection string: ' ConnectionStrings__Aahar; echo
export ConnectionStrings__Aahar
dotnet restore Aahar-API.slnx
dotnet tool install --global dotnet-ef --version 10.0.0
dotnet ef migrations script --project Aahar.Infrastructure --startup-project Aahar.Infrastructure --context RestaurantDbContext --output aahar-initial.sql
```

Inspect the SQL and verify the target independently. **Only after explicitly approving the migration of that dedicated database**, apply it:

```bash
dotnet ef database update --project Aahar.Infrastructure --startup-project Aahar.Infrastructure --context RestaurantDbContext
```

Create the first administrator only after migration, using a username and a password of 12–256 characters. This operation refuses to run when any user exists and never creates or migrates a database:

```bash
read -r -p 'Initial administrator username: ' AAHAR_ADMIN_USERNAME
read -r -s -p 'Initial administrator password: ' AAHAR_ADMIN_PASSWORD; echo
export AAHAR_ADMIN_USERNAME AAHAR_ADMIN_PASSWORD
dotnet run --project Aahar-API -- --bootstrap-admin
unset AAHAR_ADMIN_USERNAME AAHAR_ADMIN_PASSWORD
dotnet dev-certs https --trust
dotnet run --project Aahar-API --launch-profile https
```

The API listens at `https://localhost:7041`. Use an HTTPS frontend bound to localhost and a **same-origin** `/api` development proxy to that HTTPS address, preserving a `Host` of `localhost`. Trust the local development certificate; do not disable certificate validation for deployed use. The API rejects non-loopback clients and non-HTTPS requests. Do not expose the frontend proxy to a network, since the API sees the proxy as loopback. Data-protection keys must be protected by the local OS account; default development storage may not encrypt them at rest.

## HTTP contract

JSON is camelCase. Successful creation and updates return 200 unless stated otherwise. Errors are generally Problem Details with 400 for invalid operations or CSRF, 409 for uniqueness/serialization conflicts, 401/403 for authentication/authorization, and 429 for login throttling. Retry a 409 only after refreshing state.

All calls use same-origin credentials. `GET /api/auth/csrf` returns `{requestToken}` and sets an HttpOnly, Secure, SameSite=Strict antiforgery cookie. Send that token as `X-CSRF-TOKEN` on every POST, PUT, PATCH, and DELETE, including login. `POST /api/auth/login` takes `{username,password}` and returns `{username,role}` with an HttpOnly, Secure, SameSite=Strict auth cookie. Get a fresh CSRF token after login. `GET /api/auth/me` returns the same user shape; `POST /api/auth/logout` returns 204 and clears the auth cookie. Fetch a fresh token before another login. Unauthenticated requests do not redirect. Roles are `Admin`, `Manager`, `Staff`, and `Kitchen`; an Admin can `POST /api/users` with `{username,password,role}`.

| Endpoint | Request / response | Access |
| --- | --- | --- |
| `GET /api/guest/menu` | Available menu items | Anonymous |
| `GET /api/menu` | All menu items: `{id,name,description,category,price,isAvailable}` | Authenticated |
| `POST /api/menu`, `PUT /api/menu/{id}` | `{name,description,category,price,isAvailable}`; returns item | Admin, Manager |
| `GET /api/tables` | `{id,number,capacity}[]` | Authenticated |
| `POST /api/tables` | `{number,capacity}`; returns table | Admin, Manager |
| `GET /api/ingredients` | `{id,name,unit,stock}[]` | Admin, Manager |
| `POST /api/ingredients` | `{name,unit,stock}`; returns ingredient | Admin, Manager |
| `POST /api/ingredients/{id}/stock` | `{quantity}` positive receipt; returns ingredient | Admin, Manager |
| `PUT /api/menu/{id}/recipe` | `{ingredients:[{ingredientId,quantity}]}` replaces recipe; 204 | Admin, Manager |
| `GET /api/orders` | Orders, newest first | Authenticated |
| `POST /api/orders` | `{tableId,items:[{menuItemId,quantity}]}`; returns order | Admin, Manager, Staff |
| `POST /api/orders/{id}/send` | Draft to Sent; returns order | Admin, Manager, Staff |
| `POST /api/orders/{id}/complete` | Sent to Completed; returns order | Admin, Manager, Kitchen |
| `POST /api/orders/{id}/payments` | `{method:"Cash"}` or `{method:"Card"}`; Completed to Paid; returns order | Admin, Manager, Staff |
| `GET /api/sales` | `{ordersPaid,total}` over all recorded payments | Admin, Manager |

An order is `{id,tableId,status,createdAt,items:[{menuItemId,name,quantity,unitPrice}],total,payment}`; `payment` is null or `{id,method,amount,paidAt}`. Prices and names are captured at order creation. Sending uses the recipe configured at **send time**. No recipe means no stock deduction for that item. Configured ingredients are aggregated and checked before any deduction. Stock, send, completion, recipe changes, and payment use serializable transactions; concurrent conflicts must be retried after refreshing. A unique payment per order, state transitions, and nonnegative stock constraints provide additional safeguards.

Payments are **simulated only**; this is **not IRD certified** and is not suitable for real fiscal invoicing.

## Verification and limitations

`dotnet test Aahar.Tests/Aahar.Tests.csproj` runs isolated domain/mapping and HTTP cookie, CSRF, and role tests without a PostgreSQL connection. Before operational use, run end-to-end migration, transaction/concurrency, and recovery tests against a separately provisioned disposable database with explicit approval. The included tests do not verify PostgreSQL concurrency or migrations against a live server.

This is an intentionally incomplete portfolio foundation: no order editing/cancellation/refunds, table occupancy, stock ledger, recipe read endpoint, user management beyond creation, password reset/revocation, reporting filters, backups, audit trail, production deployment hardening, or real payment integration. The initial migration's rollback is destructive. The old SQL Server data and schema are not migrated.
