# Backend architecture

This is a single-restaurant, same-computer portfolio application, not a hosted multi-tenant service. The [backend guide](BACKEND.md) describes the HTTP contract and setup; [security](SECURITY.md) and [operations](OPERATIONS.md) describe the operating boundary and recovery precautions.

## Project boundaries

| Project | Responsibility | Dependencies |
| --- | --- | --- |
| `Aahar.Domain` | Entities and business rules for orders, kitchen, guest requests, inventory, and reporting date boundaries | No project dependencies |
| `Aahar.Application` | Service and typed persistence interfaces, unit-of-work contract, request/response records | Domain |
| `Aahar.Infrastructure` | EF Core PostgreSQL repositories and unit of work, model and migrations, service implementations, guest token generation, reporting queries, and Mapster DTO mapping | Application and Domain |
| `Aahar-API` | ASP.NET Core endpoints, authentication, authorization, antiforgery, middleware, and dependency-injection composition | Application and Infrastructure |
| `Aahar.Tests` | Domain, mapping, HTTP host, and offline EF model/query tests | All backend projects |

The API references Infrastructure to compose implementations, while endpoints call Application interfaces. All database services depend on typed Application repositories and `IUnitOfWork`, not `RestaurantDbContext`. Domain rules are called by services, and Mapster maps compatible entities to response records. Some composite responses are assembled explicitly. The unused old SQL Server scaffold was removed; its data is not migrated.

## Persistence boundaries

Contracts live in `Aahar.Application/Persistence`; EF implementations and SQL query builders live in `Aahar.Infrastructure/Persistence`. Orchestration lives in `Aahar.Infrastructure/Services`, where the existing Mapster and Identity dependencies remain. No EF types or `IQueryable` cross the Application boundary. Repositories cover menu and recipes, tables/orders/payments/kitchen, ingredients/stock/suppliers/purchases, guest requests, users/passwords, reports, and installation guards.

Every repository and `EfUnitOfWork` is scoped and receives the same scoped `RestaurantDbContext`. Repository adds/removes stage tracked changes; `SaveChangesAsync` flushes them. Use cases explicitly open serializable transactions, save and commit. Recipe replacement still saves deletions before inserts inside the same transaction. Disposing an uncommitted transaction rolls back all database writes; discard the failed request scope rather than reusing its tracked entities.

Password changes are the deliberate exception to staged writes: a repository executes a parameterized conditional update matching the original user/hash, inside the service's explicit unit-of-work transaction. A zero-row update returns a conflict, not success. Menu/supplier shared row locks and installation advisory locks also execute immediately and remain inside their callers' transactions. There are no automatic retries for uncertain operations.

Menu image files and restaurant-profile JSON are separate storage adapters, not participants in the PostgreSQL unit of work. This refactor changes neither database schema nor installation setup and does not migrate existing databases.

## Important workflows and invariants

- An order begins as a Draft, with an optional table for takeaway. A partial unique index protects one active Draft, Sent, or Completed order per table. Item names and prices are snapshots; editing retained Draft lines preserves their snapshots.
- Sending aggregates the recipe quantities configured at send time, validates all required stock, then writes stock deductions, movement records, order state, and an immutable queued kitchen ticket together. No recipe means no deduction. Payment is simulated and recorded once.
- An unpaid submitted order may receive additional idempotent kitchen rounds. Each round has immutable snapshots and separate stock deductions; a Completed order reopens. All tickets must be ready before completing or paying a Sent order. Cancellation aggregates recorded deductions by ingredient.
- A cancellation records actor, reason, and disposition. Staff can cancel Draft orders; only Admin/Manager can cancel submitted unpaid orders. A Return uses recorded consumption to restore stock, whereas Waste does not restore it. Pre-ledger submitted orders cannot be automatically cancelled because their deductions are unknown.
- Guest requests are not orders. Submission and acceptance validate quoted prices and availability under menu-row locks; acceptance creates a Draft order, while rejection records a reason. The status token is separate from staff authentication.
- A purchase stores supplier, ingredient, unit, quantity, and cost snapshots, updates stock, and links each line to a ledger movement in one transaction. Explicit waste checks available stock and records a negative movement. Thresholds, supplier changes, and inventory movements retain actor/reason history where applicable.
- Menu images are decoded with SkiaSharp and stored as normalized local PNG files outside application directories, keyed by menu ID. Authorization and item availability are checked before serving; the image store and database have separate backup and transaction boundaries.
- Restaurant identity has an Application contract and validation rules, a singleton Infrastructure JSON store, public reads, and Admin/CSRF-protected saves. Atomic replacement and revision checks serialize edits within one API process without adding a database schema change. Run one writer per installation file; reprints use the current identity rather than historical business-detail snapshots.
- Sales reports use paid order/item snapshots and payment time. A configured business time zone translates inclusive business dates into a UTC half-open range; changing that configuration can change historical day grouping.

These workflows use serializable transactions for coordinated changes; order/request revisions and database uniqueness and nonnegative checks provide additional protections. Conflicts can return 409: refresh state before deciding whether a retry is safe. For example, purchase references prevent duplicate supplier/reference pairs, but guest submissions and manual stock receipts are not idempotent.

## Decisions and limits

The implementation uses .NET 10, EF Core with the Npgsql PostgreSQL provider, ASP.NET Core Identity's password hasher, cookie authentication, antiforgery, and Mapster; project files are authoritative for package versions. The API has no background migration or database creation. Payments do not contact a provider, and the application is **not IRD certified** or suitable for real fiscal invoicing.

The automated suite exercises domain rules, mapping, HTTP authorization/CSRF, service transaction boundaries, and offline PostgreSQL SQL translation. CI also provisions fresh PostgreSQL for workflow/race tests and repository commit/rollback checks. These selected checks do not establish complete migration-upgrade, end-to-end, or recovery coverage. Local database tests require explicit permission and a separately provisioned test database.
