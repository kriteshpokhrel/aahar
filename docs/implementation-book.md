# Aahar implementation book and branding kit

**Status:** scope and stock rule approved; local implementation in progress.
The release inventory describes targets, not claims that they already exist.
**Product:** Aahar, a reusable restaurant operations application.
**Deployment:** one restaurant per local installation on one computer.
**Reference reviewed:** [RestroX Nepal features](https://www.restrox.com/np/features),
25 November 2026. Its descriptions inform the scope below; Aahar is an
independent implementation and visual identity, not a copy.

## 1. Product contract

Build a portfolio-quality .NET and React application that demonstrates a real,
coherent service cycle: configure the menu and tables, open an order, send its
items to the kitchen, complete the order, record a **simulated** payment, update
stock and inspect sales. A restaurant can use the same product without a
restaurant-specific redesign. Mantra Thakali Kitchen may be used as an optional,
clearly fictional example dataset, not as the product brand.

The first release runs on a single computer, binds only to localhost by default
and uses a dedicated PostgreSQL database. It is
not a hosted SaaS, a multi-tenant service, a native mobile app or a certified
fiscal product. “SaaS-like” describes a reusable product and polished
experience, not a promise of cloud hosting. English is the first language;
organize UI strings so later translation is feasible.

### Decisions already made

- Order-first, three-pane POS: categories, menu grid and persistent order.
- Refined operational identity: warm off-white, graphite and restrained brick.
- Local-only guest menu demo; a localhost QR cannot be used from a phone.
- Simulated payments only; use a dedicated PostgreSQL database.
- Install the .NET 10 SDK in the session tool directory rather than replacing
  the machine's system SDK. Extend this repository with React.

### Baseline and constraints

The original repository contained .NET 9 models and an EF Core SQL Server
context, generic repository and unit of work. A .NET 10/PostgreSQL backend
foundation has since been added; its precise scope and unverified behavior are
listed in [backend setup and limitations](../Aahar-API/BACKEND.md). It is not
the finished product. The machine initially exposed .NET 6 and 7 SDKs; .NET 10
was installed into this session's tool directory. The old SQL Server scaffold
is excluded from compilation, and its data is not migrated.

### Current implementation

The backend and React frontend now contain initial slices for authentication,
menu and tables, dine-in and takeaway orders, kitchen tickets, simulated
payments, stock and purchasing, guest requests, and business-date reports.
These are not release gates: real PostgreSQL migrations and selected workflow,
authorization and concurrency checks have been exercised, but complete
end-to-end, deployment and recovery verification remains outstanding.
The [backend](../Aahar-API/BACKEND.md) and [frontend](../web/README.md)
documentation describe available operations and remaining limitations.
Separate local `aahar_local` and `aahar_test` databases were created with the
user's permission. Fictional menu, table and stock data was seeded only into
the new application database.

## 2. Scope and measurable reference coverage

The inventory below decomposes the five feature-page descriptions into 25
observable capabilities. “Release” means intended for the planned first
working application, not already implemented. Its 20 of 25 capabilities
(80%) exceed the requested 50% by this explicit, unweighted count. This is not
a claim of 80% parity with RestroX: each capability has different complexity.
All five areas have deliverable coverage.

**Implementation status:** initial code exists for O1–O3, T1–T3 and T5,
I1–I5, D1–D5 and S1–S3, including localhost-only QR and locally stored optional
menu images. The inventory is a planned feature count, not a verified parity
score: real deployment, full accessibility, security, migration-upgrade and
recovery gates remain open. Category names are attached to items rather than
independently managed category records. Guest token recovery and retry
idempotency are not implemented. See the backend and frontend guides for the
precise supported operations.

| ID | Reference capability | First-release scope and observable outcome | Status |
|---|---|---|---|
| O1 | Take orders | Staff creates dine-in or takeaway order with quantities and notes | Release |
| O2 | Kitchen order tokens | Send an immutable snapshot of ordered lines to kitchen queue | Release |
| O3 | Kitchen display | Kitchen advances queued tickets through preparing and ready | Release |
| O4 | Bar/customer tokens | Separate BOT and COT ticket formats and destinations | Later |
| O5 | Staff orders on phones | Remote phone access and responsive staff operations | Later |
| T1 | Menu item editing | Authorized staff manage categories, items, prices and availability | Release |
| T2 | Assign tables | Staff assign an available table to a dine-in order | Release |
| T3 | Table availability | Floor view displays available and occupied states from active orders | Release |
| T4 | Reservations | Time-based reservations and conflict handling | Later |
| T5 | Order/table/bill synchronization | Settling or cancelling an order releases its table consistently | Release |
| I1 | Stock tracking | Ingredient quantities and units shown with movement history | Release |
| I2 | Supplier data | Authorized staff maintain supplier contact records | Release |
| I3 | Purchase invoices | Record supplier purchase with line quantities and costs | Release |
| I4 | Ingredient consumption | Configured recipes deduct stock once when an order is sent | Release |
| I5 | Waste and low-stock insight | Record waste with reason; flag low stock and report movements | Release |
| D1 | Interactive digital menu | Guest-facing searchable, categorized menu in browser | Release |
| D2 | QR generation | Generate a QR for a local menu URL with an explicit same-computer limitation | Release |
| D3 | Guest ordering | Guest submits a cart in the local demo; staff accepts or rejects it | Release |
| D4 | Live menu updates | Guest reads current prices and availability; server revalidates submission | Release |
| D5 | High-quality item visuals | Optional local item image handling and polished fallback | Release |
| S1 | Revenue and order volume | Date-filtered paid sales and order count | Release |
| S2 | Best-selling dishes | Ranked item quantities and sales from settled orders | Release |
| S3 | Current sales dashboard | Refreshable same-computer sales metrics | Release |
| S4 | Peak hours and trend analysis | Hourly and longer-term analytical visualizations | Later |
| S5 | Advanced profitability | Full costing, financial statements and margin analysis | Later |

Billing and access are foundational cross-cutting features, although not
separate sections on the referenced feature page: simulated cash/card/wallet
selection, a clearly non-fiscal receipt, users, roles and audit history. No
external payment provider, IRD approval or tax-compliance assertion is in scope.
Also excluded: delivery integrations, multiple locations, online sync, native
apps, automatic purchasing, printer integrations and customer accounts.

## 3. Users and end-to-end workflows

**Owner/manager** configures the restaurant, staff, menu, stock and reports.
**Cashier** manages orders, tables and simulated checkout. **Kitchen** views
and progresses tickets. A **guest** browses and submits a local demo order
without a staff login. Role policies are enforced by the API, not merely by
hiding buttons. Guest actions are limited to menu access and submitting a
validated pending request; they cannot modify an existing staff order.

### Target core service cycle

This is the intended release workflow, not a claim that every step is
implemented. In particular, order send is currently a single transition and
idempotency keys and post-send order editing are not implemented.

1. Manager creates categories, available menu items and tables; optionally
   defines ingredients and recipe quantities.
2. Cashier selects dine-in or takeaway, selects an available table for dine-in,
   adds items and adjusts quantities and notes. Prices are snapshotted at order
   line creation; unavailable items cannot be newly added.
3. Cashier sends new lines to the kitchen. The transaction creates ticket
   snapshots and stock movements for configured recipes exactly once. A retry
   with the same idempotency key cannot deduct stock twice. Insufficient stock
   is explicitly reported and requires a manager stock correction; no silent
   negative stock.
4. Kitchen progresses each ticket `queued -> preparing -> ready`. The cashier
   can see its status. The server validates transitions.
5. Cashier reviews totals, records a simulated payment and closes the order.
   The receipt explicitly says **SIMULATED / NOT A TAX INVOICE**. Settlement
   releases the table and makes the order eligible for sales reporting.
6. Cancellation requires a reason. Before kitchen submission it releases the
   table; after submission, an authorized manager must choose whether recorded
   deductions are returned as reusable stock or remain consumed as waste.
   Record the choice and compensating movements exactly once. Settled orders
   cannot be silently edited or cancelled.

**Guest demo:** staff opens the local menu or QR; guest selects available items
and submits a request with an optional table. Staff accepts it into a new order
or rejects it with a reason. Revalidation prevents stale prices or unavailable
items from being silently accepted. QR uses a localhost URL and is labeled
“same computer only”; scanning it on another device does not work. No LAN
exposure is enabled by default.

**Inventory:** manager records a purchase receipt, producing positive stock
movements and recording cost; waste produces negative movements with a reason.
Corrections are audited movements rather than edits to history. Thresholds are
per ingredient. Recipe updates affect future sends, not earlier tickets.

**Reporting:** a manager chooses a local date range and sees settled-order
revenue, count and top items. Currency is NPR; quantities and prices use decimal
arithmetic. Use an explicit local business timezone, stored timestamps in UTC,
and consistent date boundaries. A dashboard refresh retrieves current data;
do not imply push updates if polling or manual refresh is used.

## 4. Original branding kit

### Position and voice

**Aahar** is the calm control surface for a busy restaurant: capable, direct
and dependable. Brand voice is concise and human: “Send to kitchen”, “Payment
recorded” and “Low stock”, not inflated promises or decorative jargon. Keep
operational messages explicit: “Could not send order. No stock was deducted.”

### Visual system

The direction is **refined operational**: paper-like warmth, graphite structure,
and a sparing deep-brick accent. Avoid gradients, oversized hero cards, neon,
glassmorphism, gratuitous shadows and random stock food imagery. The brand is
independent of any specific restaurant; restaurant identity is contextual text.

| Token | Initial value | Use |
|---|---|---|
| `--canvas` | `#F7F5F0` | Main warm off-white canvas |
| `--surface` | `#FFFFFF` | Work surfaces and forms |
| `--ink` | `#252825` | Primary text and navigation |
| `--muted` | `#5D655F` | Secondary labels |
| `--border` | `#D8DCD5` | Quiet structure and separators |
| `--accent` | `#973F32` | Primary actions, selectively |
| `--accent-hover` | `#773126` | Interactive accent hover |
| `--success` | `#315D48` | Positive state with text/icon |
| `--warning` | `#795400` | Warning text/icon on a light background |
| `--danger` | `#9C302C` | Destructive state with text/icon |

Tokens are starting values; verify actual foreground/background pairs against
WCAG AA (4.5:1 for normal text, 3:1 for large text and meaningful controls)
and adjust before release. Never communicate state by color alone. Use a
readable sans-serif stack with tabular figures for prices and counts; a
distinctive wordmark may use a restrained geometric typographic treatment.
Prefer system fonts or locally bundled, appropriately licensed fonts so local
use does not depend on third-party font services.

### Mark, type, spacing and components

Use an original typographic **aahar** wordmark with a simple circular plate
motif if implemented; no borrowed restaurant logos, ornamental cultural motifs
or unlicensed imagery. Use 4/8/12/16/24/32px spacing, 4–8px control radii,
one-pixel borders and restrained elevation. Body text is at least 14–16px;
money, totals and primary actions are easy to scan. Controls are keyboard
reachable, visibly focused and sized for touch when practical.

Components: compact app shell and section navigation; semantic buttons and
fields; status chips with labels; empty, loading and error states; confirmation
dialogs for destructive actions; tables with headers; searchable item cards;
ticket cards; and a persistent order summary. Icons are simple, consistent and
never the sole accessible label. Respect reduced-motion preferences.

### Screen-level UX

- **Login:** clear product name, minimal form, explicit errors and no demo
  credentials displayed in production.
- **POS:** left category rail, center searchable menu grid, right persistent
  order. The order shows table/type, editable quantities, notes, subtotal,
  configurable charges, total and a prominent send action. At narrower widths
  panels stack with an accessible order summary; never hide the total.
- **Tables:** compact status grid, table number, active order and action to
  open it. Labels and icons distinguish states without relying on color.
- **Kitchen:** large ticket number, elapsed time, items and notes; obvious
  next-state action. Avoid showing monetary or customer details unnecessarily.
- **Inventory:** table of ingredient, unit, on-hand, threshold and status;
  purchase and waste forms show a confirmation before creating movements.
- **Reports:** restrained KPI strip, date filter and a legible ranked list;
  label currency and the definition of each number.
- **Guest menu:** light, inviting layout distinct from staff controls; optional
  licensed/local images, categories, price and availability; cart review and
  an unmistakable pending/accepted/rejected status.

### Running bills and counter checkout

The next service slice targets a normal restaurant visit: open one bill for
the table, add food or drinks throughout service, and pay the full balance at
the billing counter. "Clear the bill" means settle that bill and release the
table, not delete its items or erase its payment history. Split bills,
cross-table merging, discounts, refunds and partial payments are separate work.

Reference research used the public [Square restaurant POS overview](https://squareup.com/us/en/point-of-sale/restaurants),
[Square check management guide](https://squareup.com/help/us/en/article/8166-comp-void-and-reassign-checks-with-square-for-restaurants)
and [RestroX feature page](https://www.restrox.com/np/features). These describe
open checks, floor/table management, kitchen tickets and connected billing;
they do not establish that Aahar implements those products' full capabilities.
Adopt the workflow principles, not their branding, screenshots or assets.

| Observed pattern | Aahar improvement and acceptance gate |
|---|---|
| Open checks persist throughout a visit | Occupied table opens its running bill, rather than blocking the operator with "select another table" |
| Table, order and billing stay connected | Table workspace shows bill balance and service state, with an explicit counter-checkout action |
| New kitchen work is distinct from prepared items | Each added round creates a new immutable ticket; earlier tickets and price snapshots remain unchanged |
| Counter settlement closes a check | One confirmation pays the entire bill only when all kitchen rounds are ready; one payment releases the table |
| Receipts are practical operational documents | 80 mm receipt shows order/payment reference, date, table or takeaway, quantities, unit/line prices, total and simulated method |
| Staff screens favor speed and clarity | Compact navigation, labeled states, aligned money, explicit next actions, accessible focus and honest loading/error states |

An added round must atomically append price-snapshotted lines, create its
kitchen ticket and deduct only that round's stock. A retained request ID and
identical payload permit a safe retry after an uncertain response. An altered
payload must not reuse the ID. Cancellation must account for every round's
consumption without returning the same stock twice. Paid/cancelled bills
cannot receive additional rounds.

Receipt printing uses the browser's **Print / Save as PDF** dialog; direct
thermal-printer integration is not included. The print stylesheet must isolate
the receipt, wrap long item names and exclude navigation and controls. Do not
invent restaurant contact details, tax IDs or tax rates. Every receipt remains
marked **SIMULATED / NOT A TAX INVOICE**.

Navigation should group service actions separately from management. Password
change, sign-out and workspace preferences belong under the account/profile
area, not beside the primary service screens. Available tables use a green
surface and occupied tables a red surface, both with explicit text and a
distinct focus/selected state; color is not the sole status cue. The guest
screen is menu-first, without a large decorative hero above the search,
categories and items.

Browser-specific preferences include compact/comfortable density, the default
simulated checkout method, opening the receipt view after settlement, and
optional automatic kitchen sending for newly saved bills. They are device
preferences, not restaurant-wide policies, and must not contain credentials.
Auto-send is not auto-prepare: the kitchen still confirms preparation and
readiness. On the pre-rounds API, saving a draft and sending it are separate
server operations. If sending fails after saving, show the existing bill ID
and its unsent state so staff retry that bill rather than create a duplicate.
Added rounds use their own atomic append-and-send operation.

**Rollout constraint:** multi-round persistence requires an additive schema
migration. That migration has not been approved for the existing local
databases. Keep the live pre-rounds API running, keep the polished frontend
compatible with its contract, and disable the new-round action with an explicit
upgrade-required message. Do not infer migration permission from a feature
request, or present the source implementation as a live capability.

## 5. Technical architecture

### Structure and dependencies

Use a .NET 10 solution with `Domain`, `Application`, `Infrastructure` and
`Api` projects plus unit and integration test projects; React + TypeScript lives
in a `web/` application. Domain contains entities, value objects and business
invariants. Application owns use cases, request/response contracts, validators
and interfaces for persistence, clock, identity and other genuine boundaries.
Infrastructure implements EF Core/Npgsql persistence, authentication support
and adapters. API owns endpoints, composition, authorization policies,
middleware and HTTP contracts. Dependencies point inward; no EF or web types
in domain. Use Mapster explicitly at API/application DTO boundaries; money,
state transitions and security-sensitive values are handled deliberately.

Use dependency injection with scoped database contexts and use cases. Prefer
use-case-specific queries over a generic repository for every entity; EF Core
already supplies unit-of-work behavior. The existing scaffold can be replaced
incrementally, but no SQL Server database should be migrated or deleted
without a separately approved data migration plan.

Frontend: React + TypeScript, router, a small typed API client, server-state
management and a token-based CSS design system. Choose minimal maintained
libraries for forms, schema validation and QR generation when implementing;
commit lockfiles. Centralize request errors and session handling. No secrets
in browser bundles. UI may optimize presentation but never enforce business
rules in place of server validation.

### Domain and persistence

Core aggregates: `User`, `MenuCategory`, `MenuItem`, `DiningTable`, `Order`
and price-snapshotted `OrderLine`, `KitchenTicket` and ticket lines,
`Payment`, `Ingredient`, `RecipeLine`, `Supplier`, `Purchase` and lines,
`StockMovement`, `GuestOrderRequest` and `AuditEvent`. Use stable IDs,
foreign keys, unique constraints, precise numeric columns and concurrency
tokens for conflicting updates. Index common status, date and relationship
queries. EF migrations are reviewed and applied explicitly; no automatic
destructive schema reset or data seeding on startup.

An order moves through draft, in-service and settled/cancelled states. Tickets
have their own lifecycle. A transaction atomically creates a kitchen ticket
and associated stock movements. Settling writes a single simulated payment and
releases the table atomically. Concurrency and idempotency protections prevent
double send, double settlement and multiple active orders for a table.
Auditable corrections use compensating records instead of rewriting history.
Avoid publishing events outside a committed transaction; the first release
can use local refresh rather than distributed messaging.

Totals: snapshot line unit price, multiply by quantity, round currency to two
decimal places with an explicitly documented rounding rule. Charges or taxes,
if enabled, are configurable demo settings and clearly shown; defaults make no
claim about statutory rates. Changes never rewrite already settled totals.

### API and security

- REST endpoints grouped by auth, users, menu, tables, orders, kitchen,
  inventory, guest requests and reports. Publish OpenAPI in development; use
  stable DTOs, paging for growing lists and `ProblemDetails` errors.
- Central exception handling middleware logs a correlation ID and returns safe
  errors without stack traces or credentials. Validate requests and return
  specific field errors; reject invalid state transitions and conflicts.
- Use a supported ASP.NET Core Identity password hasher and local accounts.
  Set up the first administrator through an explicit local bootstrap step,
  never a hard-coded password or public registration. Use secure, HttpOnly,
  SameSite cookies; serve the production local UI and API under one origin,
  use anti-forgery protection for cookie-authenticated mutations, and configure
  HTTPS for secure cookies. Document local certificate trust and development
  proxy setup. Do not store authentication tokens in localStorage.
- Enforce policies for manager, cashier and kitchen roles on every endpoint,
  including inventory and reports. Scope anonymous guest endpoints narrowly.
  Validate IDs, lengths, quantities, decimal ranges and server-side prices;
  constrain image size/type and store local uploads outside executable paths.
- Bind to loopback by default. No blanket CORS, broad network listening or
  public guest endpoints reachable over the network. Rate-limit login and
  anonymous submissions, protect against common injection/XSS/CSRF paths and
  log security-relevant changes without secrets or unnecessary personal data.
- Configuration comes from environment variables or .NET user secrets, never
  committed credentials. Use an explicitly approved, dedicated database
  and least-privilege credentials. Development and integration-test databases
  must be separate; never run destructive tests against the user's database.

## 6. Phased delivery and gates

Each phase ends in working software, tests, updated documentation and a
demonstrable acceptance gate. Do not mark a feature “done” solely because an
endpoint, screen or mock exists.

### Phase 0 — foundation and design

Inventory the scaffold; agree on product decisions and this book. Establish
versioned documentation, architecture decision records, threat assumptions and
an explicit feature-status ledger. Create the solution layout, formatting,
linting, CI-ready commands, local setup checklist and a minimal vertical health
slice. Preserve the existing repository history and unrelated changes.

**Gate:** clean build and health endpoint; README explains prerequisites and
safe setup; no credentials committed; design tokens and scope approved.

### Phase 1 — access and product shell

Add dedicated PostgreSQL configuration and reviewed initial migration; explicit
bootstrap for the first admin; login/logout, role policies, problem handling,
request validation and audit basics. Build the React shell, navigation, login,
shared UI primitives and responsive behavior.

**Gate:** admin can sign in and out; an unauthenticated user is denied protected
endpoints; a less-privileged user cannot call manager APIs directly; secrets
remain local and a migration runs only against the configured dedicated DB.

### Phase 2 — menu, tables and order-first POS

Implement category/item management, optional local images, tables, order
creation, line editing, notes and state transitions. Build three-pane POS and
table overview with loading, empty, error and conflict states.

**Gate:** create a table and menu item, open an order, add/change/remove a line,
and recover after reload; an unavailable item is rejected; two active orders
cannot own the same table; historical line prices do not change with menu edits.

### Phase 3 — kitchen, billing and stock

Implement queued ticket snapshots and kitchen status workflow; ingredient,
recipe and supplier management; purchase receipts, stock ledger, low-stock and
waste. Add transaction-safe send, cancellation compensation and simulated
checkout with a visibly non-fiscal receipt.

**Gate:** one order flows from POS to ready ticket to simulated settlement;
stock deducts once on send even on retry; cancellation records the manager's
return-or-waste choice and restores reusable stock only once;
settlement and table release are atomic; duplicate payment is rejected;
receipts explicitly disclaim fiscal/IRD compliance.

### Phase 4 — local guest experience and reporting

Implement guest menu, local QR and pending guest request queue; authorized
staff accept or reject requests. Add date-filtered dashboard and best-sellers,
using settled order snapshots; provide clear local-demo limitations.

**Gate:** a guest on the same computer browses, submits and sees request status;
staff accepts or rejects; stale menu items/prices produce an explicit conflict;
QR destination is documented as localhost-only; reports reconcile exactly to
settled test transactions, including date boundaries and empty periods.

### Phase 5 — hardening, delivery and portfolio

Finish accessibility, responsive layouts, structured audit coverage, backup
and restore guidance, operations/runbook, troubleshooting, architecture
diagrams/decisions and portfolio walkthrough. Validate representative end-to-end
flows and documented fresh-start steps on the target machine.

**Gate:** all release inventory rows have evidence; targeted unit, integration
and browser tests pass; no high-severity known security defects; keyboard
navigation and color contrast are checked; a fresh documented installation
works with a user-provided dedicated DB, without real payments.

## 7. Verification strategy

- **Domain unit tests:** valid/invalid transitions, price snapshot/rounding,
  stock rules, cancellation compensation and duplicate operations.
- **API integration tests:** real isolated PostgreSQL test database; migrations,
  authorization by role, input validation, concurrency, transaction rollback,
  idempotency and reports. Database creation requires an explicit test setup;
  tests must refuse to target the configured application database.
- **Frontend tests:** component interactions, accessible labels, validation,
  loading/error/empty states, and role-appropriate navigation.
- **Browser end-to-end:** login; create menu/table; create order; send to
  kitchen; settle; verify stock and report; guest request acceptance; denied
  unauthorized action. Use deterministic test data, not a real restaurant.
- **Quality/security:** type-check, lint, production builds, dependency review,
  keyboard and contrast checks, localhost binding verification, CSRF/auth
  checks, no secrets in logs or source, and verified backup/restore on an
  isolated disposable database. Record exact commands once implemented.

## 8. Local setup, operations and recovery

Use a **dedicated** PostgreSQL database and user. Do not inspect, reuse,
create, change or delete existing databases without permission. Provide the
exact privileges and connection configuration; enter credentials locally
using .NET user secrets or environment variables rather than chat. A second,
explicitly named test database is required for integration tests.

Install the approved .NET 10 SDK only in this session's tool directory; Node
and npm are already available. Document exact versions, commands, ports,
HTTPS certificate setup, migration and admin bootstrap as each is verified.
Production local mode should serve built React and API from one localhost
origin; development may use a same-origin proxy. Do not describe the app as
working until it has actually been started and exercised against an approved
database.

Operations documentation must cover configuration, migration/rollback
limitations, process startup/shutdown, logs, backup and restore, upgrades,
credential rotation and common failures. Backups and restores are explicit
operator actions; test restoration only on a separate disposable database.
Local-only deployment is not protection against other users with access to
the same computer, and is not a substitute for OS account and disk security.

## 9. Documentation and agent handoff

Maintain a concise README as a working entry point and `AGENTS.md` as
repository-specific agent instructions. As the corresponding work lands, add:

- Architecture overview, dependency diagram and numbered decision records.
- Feature inventory/status with evidence and supported workflows.
- Local developer setup, API contract and database migration guide.
- User guide for POS, kitchen, inventory, guest demo and reports.
- Security/threat model, roles, privacy, limitations and incident guidance.
- Operations runbook, backup/restore, troubleshooting and upgrade notes.
- Test strategy, exact runnable commands, release checklist and contribution
  conventions.

Documentation must distinguish implemented behavior from planned work, link to
the authoritative source, avoid stale duplicate instructions and be updated in
the same change as behavior. Never include real credentials, production data or
unverified claims.

## 10. Decision log and unresolved details

| Decision | Outcome |
|---|---|
| Product model | Reusable product, one restaurant per local install |
| Branding | Aahar; refined operational, not restaurant-customized |
| POS | Order-first, three-pane layout |
| Guest access | Same-computer local demo; no remote phone access |
| Language | English initially; structure ready for localization |
| Database | PostgreSQL, explicitly approved dedicated database |
| Payments | Simulated only; no IRD compliance claim |
| Submitted-order cancellation | Manager chooses stock return or waste, with a reason |
| SDK | .NET 10 LTS in session tool directory |

Before implementing affected workflows, confirm the restaurant's actual tax,
service charge and receipt requirements if the demo defaults are insufficient.
Networked deployment, real fiscal billing and multi-tenancy require separate
design, authorization and security review rather than silently expanding this
local release.
