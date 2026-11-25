# Aahar backend

Single-restaurant, localhost-only portfolio backend: .NET 10, PostgreSQL, EF Core, Mapster, cookie authentication, and antiforgery. `Aahar.Domain` holds entities and rules; `Aahar.Application` defines contracts; `Aahar.Infrastructure` implements persistence and services; `Aahar-API` is the HTTP host. The original SQL Server scaffold under `Aahar-API/Data`, `Models`, and `Repository` is retained for reference but excluded from compilation.

## Setup

No database is created or migrated automatically. The initial migration is for a **new, dedicated, empty PostgreSQL database**; it is not an upgrade or data migration from the old SQL Server scaffold. Review both migrations in `Aahar.Infrastructure/Migrations` and the generated SQL before applying them. The initial migration's `Down` drops tables; the ledger migration refuses rollback because it would destroy history. Do not apply changes to any database or run rollback commands without explicit approval and a backup.

When upgrading an already-provisioned dedicated Aahar database, stop all old application instances and inspect it first. The follow-up migration refuses to proceed if multiple Draft, Sent, or Completed orders share a table; reconcile those orders manually with an operator before retrying. It records each existing ingredient's current stock as an opening balance, not a reconstruction of past movements. Submitted orders from before the ledger cannot be cancelled automatically, even as waste, because their actual deductions are unknown. Handle them through a documented manual operational process; do not guess a stock adjustment.

With .NET 10 installed and a dedicated database already created by its owner, configure a connection string privately (do not enter passwords in chat or commit them). For example, in a local bash terminal, prompt without echo and avoid putting the value in shell history:

```bash
read -r -s -p 'Dedicated PostgreSQL connection string: ' ConnectionStrings__Aahar; echo
export ConnectionStrings__Aahar
dotnet restore Aahar-API.slnx
dotnet tool install --global dotnet-ef --version 10.0.0
dotnet ef migrations script --project Aahar.Infrastructure --startup-project Aahar.Infrastructure --context RestaurantDbContext --output aahar-migrations.sql
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
| `GET /api/tables` | `{id,number,capacity,isOccupied}[]` | Authenticated |
| `POST /api/tables` | `{number,capacity}`; returns table | Admin, Manager |
| `GET /api/ingredients` | `{id,name,unit,stock}[]` | Admin, Manager |
| `POST /api/ingredients` | `{name,unit,stock}`; returns ingredient | Admin, Manager |
| `POST /api/ingredients/{id}/stock` | `{quantity}` positive receipt; returns ingredient | Admin, Manager |
| `GET /api/ingredients/{id}/movements?beforeId=&limit=` | Newest-first stock movements; limit 1–100, default 50; use the last ID as `beforeId` for the next page | Admin, Manager |
| `GET /api/menu/{id}/recipe` | `{ingredients:[{ingredientId,quantity}]}` | Admin, Manager |
| `PUT /api/menu/{id}/recipe` | `{ingredients:[{ingredientId,quantity}]}` replaces recipe; 204 | Admin, Manager |
| `GET /api/orders` | Orders, newest first | Authenticated |
| `POST /api/orders` | `{tableId,items:[{menuItemId,quantity}]}`; returns order | Admin, Manager, Staff |
| `POST /api/orders/{id}/send` | Draft to Sent; returns order | Admin, Manager, Staff |
| `POST /api/orders/{id}/complete` | Sent to Completed; returns order | Admin, Manager, Kitchen |
| `POST /api/orders/{id}/cancel` | `{reason,stockDisposition}`; returns order | Admin, Manager, Staff for Draft; Admin, Manager for Sent/Completed |
| `POST /api/orders/{id}/payments` | `{method:"Cash"}` or `{method:"Card"}`; Completed to Paid; returns order | Admin, Manager, Staff |
| `GET /api/sales` | `{ordersPaid,total}` over all recorded payments | Admin, Manager |

An order is `{id,tableId,status,createdAt,items:[{menuItemId,name,quantity,unitPrice}],total,payment,cancelledAt,cancelledBy,cancellationReason,cancellationStockDisposition}`; `payment` is null or `{id,method,amount,paidAt}`, and cancellation fields are null until cancellation. Prices and names are captured at order creation. A table is occupied while its order is Draft, Sent, or Completed; a concurrent second active order for the same table is rejected with 409. Payment or cancellation releases the table.

Sending uses the recipe configured at **send time**. No recipe means no stock deduction for that item. Configured ingredients are aggregated and checked before any deduction. Stock, send, cancellation, completion, recipe changes, and payment use serializable transactions; concurrent conflicts must be retried after refreshing. A unique payment per order, unique active order per table, state transitions, movement uniqueness, and nonnegative stock constraints provide additional safeguards.

Cancellation requires a nonblank reason (at most 300 characters). Draft uses `stockDisposition:"None"`; submitted orders require `stockDisposition:"Return"` or `"Waste"`. Return restores the exact recorded deductions, even if recipes have changed, and must only be selected when the ingredients are physically reusable. Waste leaves the deduction in place. A cancelled order cannot be cancelled again, and a paid order cannot be cancelled. Reason, actor, time, and disposition remain on the order. The stock ledger records opening balances, receipts, consumption, and returns as `{id,ingredientId,orderId,kind,quantityChange,balanceAfter,actor,occurredAt}`; orderId is null for opening and receipt. Waste is recorded on the cancelled order, not as a stock change.

Payments are **simulated only**; this is **not IRD certified** and is not suitable for real fiscal invoicing.

## Verification and limitations

`dotnet test Aahar.Tests/Aahar.Tests.csproj` runs isolated domain/mapping and HTTP cookie, CSRF, and role tests without a PostgreSQL connection. Before operational use, run end-to-end migration, transaction/concurrency, and recovery tests against a separately provisioned disposable database with explicit approval. The included tests do not verify PostgreSQL concurrency or migrations against a live server.

This is an intentionally incomplete portfolio foundation: no order editing or refunds, user management beyond creation, password reset/revocation, reporting filters, backups, comprehensive audit trail, production deployment hardening, or real payment integration. The initial migration's rollback is destructive. The old SQL Server data and schema are not migrated.
