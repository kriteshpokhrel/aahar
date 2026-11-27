# Aahar frontend

React and TypeScript workspace for a single restaurant. The sign-in screen also offers a clearly marked, in-memory sample workspace; sample orders are discarded on refresh and never sent to the API.

## Development

Use Node.js 24 or newer:

```sh
cd web
npm ci
npm run dev
npm run build
npm run lint
npm test
```

Vite proxies `/api` to `https://localhost:7041` by default; set `AAHAR_API_URL` to change the backend origin. The proxy accepts the development backend's self-signed certificate. For an HTTPS frontend, set `AAHAR_DEV_CERT` and `AAHAR_DEV_KEY` to paths for a locally trusted certificate and key before starting Vite. Do not expose the development server publicly. The frontend and API are same-origin from the browser's perspective; the backend needs no CORS configuration for this setup.

The client expects cookie authentication and `GET /api/auth/csrf` to return a request token. It sends `X-CSRF-TOKEN` on mutations and refreshes the token after login and logout. API routes and DTOs are isolated in `src/api.ts`; the backend remains responsible for authorization and validation.

The POS, tables, kitchen, menu catalogue, inventory, sales overview and guest menu use the API. Table availability comes from the server; order conflicts are reported rather than silently retried. Staff can cancel draft orders; managers and administrators can also cancel submitted orders and must choose whether to return stock or mark it as waste. Menu and table creation, menu edits, inventory adjustments and sales are available to managers and administrators.

The administrator and manager POS overview highlights queued kitchen tickets, completed orders awaiting simulated payment, pending guest requests, low stock and available tables. It links to the relevant workflows. Counts are snapshots, not live notifications; use refresh for changes from other staff. A full guest request page is shown as 20+ rather than an exact total. Unavailable data is marked instead of shown as zero. Sample mode displays example service counts without connecting to inventory or guest requests.

Orders can be dine-in or takeaway. Draft items and kitchen notes can be edited before sending; sent orders produce kitchen tickets that progress through queued, preparing and ready before order completion. Reconstructed tickets are flagged: verify the actual kitchen state before acting, as their timestamps reflect order creation. Refresh the kitchen view to see other operators' changes.

Guests can submit quoted requests for staff review using a takeaway or table-specific guest link. Staff acceptance creates a draft order, not a kitchen ticket; prices, availability and table occupancy are revalidated. Guest status lookup uses an access token held only in page memory, so closing or reloading the page loses lookup access. If submission has an uncertain network outcome, ask staff before trying again: the API has no idempotency or token recovery. Table links expose a table reference rather than a verified table number; guests should confirm it with staff.

The manager reports use an inclusive business-date range of at most 366 days. The API supplies the business timezone and UTC payment window; best-selling rows use the item name and price snapshots recorded at sale.

Managers can view low stock, change thresholds, record stock and waste, browse movement and threshold histories, manage suppliers and their change history, and record and browse purchase receipts. Quantities use the ingredient's exact unit; receipt totals and snapshots come from the server. Supplier references are case-sensitive and unique per supplier. If recording a receipt has an uncertain outcome, check receipt history and with staff before trying again; the API does not provide idempotent retry. The warning is kept only in the current page session and is lost on reload.

Managers can edit the ingredient quantities used per menu item in Manage recipes. Saving replaces the whole recipe; reload before saving when other managers may be editing. Stock is deducted using the recipe at order send time; an empty recipe causes no deduction.

The guest menu QR is generated locally and encodes the same selected takeaway or table link; it does not contact a QR service. A localhost link or QR works only on the same computer, not on another device. Do not expose the development proxy to remote devices.

Cash and card payments are simulations. The paid order view provides a simulated receipt from the order and payment data, not an IRD-compliant invoice. Administrators can create staff accounts with a role and an administrator-supplied password; account listing and editing are not implemented in the frontend.
