# Aahar backend

Single-restaurant, localhost-only portfolio backend: .NET 10, PostgreSQL, EF Core, Mapster, cookie authentication, and antiforgery. `Aahar.Domain` holds entities and rules; `Aahar.Application` defines contracts; `Aahar.Infrastructure` implements persistence and services; `Aahar-API` is the HTTP host. The original SQL Server scaffold under `Aahar-API/Data`, `Models`, and `Repository` is retained for reference but excluded from compilation.

## Setup

No database is created or migrated automatically. The initial migration is for a **new, dedicated, empty PostgreSQL database**; it is not an upgrade or data migration from the old SQL Server scaffold. Review all migrations in `Aahar.Infrastructure/Migrations` and the generated SQL before applying them. The initial migration's `Down` drops tables; later migrations refuse rollback because it would destroy history. Do not apply changes to any database or run rollback commands without explicit approval and a backup.

When upgrading an already-provisioned dedicated Aahar database, stop all old application instances and inspect it first. The follow-up migration refuses to proceed if multiple Draft, Sent, or Completed orders share a table; reconcile those orders manually with an operator before retrying. It records each existing ingredient's current stock as an opening balance, not a reconstruction of past movements. Submitted orders from before the ledger cannot be cancelled automatically, even as waste, because their actual deductions are unknown. Handle them through a documented manual operational process; do not guess a stock adjustment.

The kitchen migration reconstructs a queued ticket from the stored item snapshots for each existing Sent order, without deducting stock again. These tickets have `isReconstructed:true`, no historical notes, and use the order creation time rather than its unknown send time. An operator must check their actual kitchen status before proceeding; treating them as newly queued could cause food to be prepared twice. Migration stops if a Sent order has no items. Existing Completed and Paid orders do not get reconstructed tickets.

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

`Business:TimeZone` in `appsettings.json` explicitly defaults to `Asia/Kathmandu` for a Nepal-local restaurant. Set `Business__TimeZone` to the restaurant's system time-zone ID before starting the API if it operates elsewhere. A missing or invalid ID prevents startup. Reporting uses this configured zone, not the server's implicit local zone; changing it can change which business day includes a historical payment. Keep the operating system's time-zone data current.

## HTTP contract

JSON is camelCase. Successful creation and updates return 200 unless stated otherwise. Errors are generally Problem Details with 400 for invalid operations or CSRF, 409 for uniqueness/serialization conflicts, 401/403 for authentication/authorization, and 429 for login throttling. Retry a 409 only after refreshing state.

All calls use same-origin credentials. `GET /api/auth/csrf` returns `{requestToken}` and sets an HttpOnly, Secure, SameSite=Strict antiforgery cookie. Send that token as `X-CSRF-TOKEN` on every POST, PUT, PATCH, and DELETE, including login. `POST /api/auth/login` takes `{username,password}` and returns `{username,role}` with an HttpOnly, Secure, SameSite=Strict auth cookie. Get a fresh CSRF token after login. `GET /api/auth/me` returns the same user shape; `POST /api/auth/logout` returns 204 and clears the auth cookie. Fetch a fresh token before another login. Unauthenticated requests do not redirect. Roles are `Admin`, `Manager`, `Staff`, and `Kitchen`; an Admin can `POST /api/users` with `{username,password,role}`.

| Endpoint | Request / response | Access |
| --- | --- | --- |
| `GET /api/guest/menu` | Available menu items | Anonymous |
| `POST /api/guest/requests` | `{tableId:null,items:[{menuItemId,quantity,quotedUnitPrice,notes}]}`; returns `{id,status,accessToken}` | Anonymous, CSRF protected |
| `GET /api/guest/requests/{id}` | `X-Guest-Request-Token` header; returns `{id,status,orderId}` | Anonymous, token required |
| `GET /api/guest-requests?status=&beforeId=&limit=` | Newest-first requests; `status` optional: Pending, Accepted, Rejected; `limit` 1–100, default 50; use last ID as `beforeId` | Admin, Manager, Staff |
| `POST /api/guest-requests/{id}/accept` | No body; returns `{request,order}` | Admin, Manager, Staff |
| `POST /api/guest-requests/{id}/reject` | `{reason}`; returns request | Admin, Manager, Staff |
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
| `POST /api/orders` | `{tableId,items:[{menuItemId,quantity,notes}]}`; `tableId` null or omitted for takeaway; returns order | Admin, Manager, Staff |
| `PUT /api/orders/{id}/items` | `{items:[{menuItemId,quantity,notes}]}` replaces all Draft lines; returns order | Admin, Manager, Staff |
| `POST /api/orders/{id}/send` | Draft to Sent; deducts stock and creates a Queued ticket; returns order | Admin, Manager, Staff |
| `GET /api/kitchen/tickets` | All kitchen tickets, newest first | Authenticated |
| `POST /api/kitchen/tickets/{id}/prepare` | Queued to Preparing; returns ticket | Admin, Manager, Kitchen |
| `POST /api/kitchen/tickets/{id}/ready` | Preparing to Ready; returns ticket | Admin, Manager, Kitchen |
| `POST /api/orders/{id}/complete` | Sent to Completed, only after ticket is Ready; returns order | Admin, Manager, Kitchen |
| `POST /api/orders/{id}/cancel` | `{reason,stockDisposition}`; returns order | Admin, Manager, Staff for Draft; Admin, Manager for Sent/Completed |
| `POST /api/orders/{id}/payments` | `{method:"Cash"}` or `{method:"Card"}`; Completed to Paid; returns order | Admin, Manager, Staff |
| `GET /api/sales` | `{ordersPaid,total}` over all recorded payments | Admin, Manager |
| `GET /api/reports/sales?fromDate=YYYY-MM-DD&toDate=YYYY-MM-DD` | `{fromDate,toDate,timeZone,fromUtc,toUtcExclusive,ordersPaid,total}` | Admin, Manager |
| `GET /api/reports/best-selling?fromDate=YYYY-MM-DD&toDate=YYYY-MM-DD&limit=20` | `{fromDate,toDate,timeZone,fromUtc,toUtcExclusive,items:[{menuItemId,name,unitPrice,quantity,revenue}]}` | Admin, Manager |

An order is `{id,tableId,status,createdAt,items:[{menuItemId,name,quantity,unitPrice,notes}],total,payment,cancelledAt,cancelledBy,cancellationReason,cancellationStockDisposition}`; `tableId` is null for takeaway, `payment` is null or `{id,method,amount,paidAt}`, and cancellation fields are null until cancellation. Notes are optional strings of at most 300 characters. Prices and names are captured when a line is first added; editing a retained line preserves its snapshot, and a removed and re-added line uses the current menu values. Unavailable items can be retained or reduced, but cannot be added or increased. Editing is restricted to Draft orders. A table is occupied while its dine-in order is Draft, Sent, or Completed; a concurrent second active order for the same table is rejected with 409. Payment or cancellation releases the table. Takeaway orders do not occupy a table.

A ticket is `{id,orderId,tableId,status,createdAt,isReconstructed,items:[{menuItemId,name,quantity,unitPrice,notes}]}`. Its items are immutable snapshots made when the order is sent; sending, stock deduction and ticket creation are one transaction. Kitchen transitions must be in order: Queued, Preparing, Ready. Completing an order requires Ready. Cancelling an order also marks its ticket Cancelled. Order revisions detect concurrent edits and kitchen transitions; refresh after a 409.

Guest submission is a request for review, not a confirmed order, payment, stock deduction, or kitchen ticket. Supply the numeric `quotedUnitPrice` from the guest menu for each item; omitted/null or invalid prices are rejected, and changed prices or unavailable items return 409. `tableId` may be a valid table ID or null/omitted for takeaway. Prices and availability are checked under menu-row locks both at submission and acceptance. On acceptance a new Draft order is created atomically, after checking the table is free; its prices are those validated at acceptance and its names reflect the current menu. Staff can subsequently edit the draft through the regular order API. If prices, availability, or occupancy changed, acceptance returns 409 and the request stays Pending; reject it and have the guest submit a fresh quote if needed. Acceptance and rejection are mutually exclusive; retries after a decision return 409. Rejection requires a nonblank reason of at most 300 characters.

An authenticated request is `{id,tableId,status,createdAt,decidedAt,decidedBy,rejectionReason,orderId,items:[{menuItemId,name,quantity,quotedUnitPrice,notes}]}`. The guest status response intentionally omits items, notes, actor, and rejection reason. The submission returns a random `accessToken` once: keep it in memory, send it only in `X-Guest-Request-Token` over same-origin HTTPS, and do not put it in a URL or logs. Only its hash is stored; losing the token (including on page reload) prevents guest status lookup. Missing or incorrect tokens and unknown request IDs return 404. There is no anonymous request list.

Guest submissions are limited globally per server process to five attempts per minute, including invalid attempts, with a 64 KiB body limit. Status lookups are limited to 60 per minute. Limits reset on restart and are shared by all local users. All mutations, including guest submission, require the CSRF token as described above; refresh that token after login. Localhost-only is a security boundary: do not expose the frontend proxy to remote devices. Duplicate submissions are not idempotent and can create multiple Pending requests; a lost submission response also loses its status token. There is no automatic expiration or retention policy for requests.

Sending uses the recipe configured at **send time**. No recipe means no stock deduction for that item. Configured ingredients are aggregated and checked before any deduction. Stock, send, cancellation, completion, recipe changes, and payment use serializable transactions; concurrent conflicts must be retried after refreshing. A unique payment per order, unique active order per table, state transitions, movement uniqueness, and nonnegative stock constraints provide additional safeguards.

Cancellation requires a nonblank reason (at most 300 characters). Draft uses `stockDisposition:"None"`; submitted orders require `stockDisposition:"Return"` or `"Waste"`. Return restores the exact recorded deductions, even if recipes have changed, and must only be selected when the ingredients are physically reusable. Waste leaves the deduction in place. A cancelled order cannot be cancelled again, and a paid order cannot be cancelled. Reason, actor, time, and disposition remain on the order. The stock ledger records opening balances, receipts, consumption, and returns as `{id,ingredientId,orderId,kind,quantityChange,balanceAfter,actor,occurredAt}`; orderId is null for opening and receipt. Waste is recorded on the cancelled order, not as a stock change.

Reports require both inclusive ISO dates, ordered, within 1900-01-01 through 9998-12-30, and spanning at most 366 calendar days. They convert the local start midnight and midnight after the end date to the UTC half-open interval `[fromUtc,toUtcExclusive)` and filter on payment time and Paid status. An invalid or ambiguous local midnight is rejected with 400 rather than guessed. Empty sales return zero count and total; empty best-sellers return an empty array. Best-selling `limit` is 1–100, default 20. Rows group by the stored menu item ID, name, and unit price snapshots, so renamed/repriced items appear as separate rows; revenue is the sum of snapshot price times quantity. Results sort by quantity descending, revenue descending, then ID, name, and price. `total` is the sum of simulated payment amounts, with no tax, refund, or IRD accounting.

Payments are **simulated only**; this is **not IRD certified** and is not suitable for real fiscal invoicing.

## Verification and limitations

`dotnet test Aahar.Tests/Aahar.Tests.csproj` runs isolated domain/mapping and HTTP cookie, CSRF, and role tests without a PostgreSQL connection. Before operational use, run end-to-end migration, transaction/concurrency, and recovery tests against a separately provisioned disposable database with explicit approval. The included tests do not verify PostgreSQL concurrency or migrations against a live server.

This is an intentionally incomplete portfolio foundation: no guest request expiration/idempotency or token recovery, post-send order editing, refunds, user management beyond creation, password reset/revocation, report export or tax accounting, backups, comprehensive audit trail, production deployment hardening, or real payment integration. The initial migration's rollback is destructive. The old SQL Server data and schema are not migrated.
