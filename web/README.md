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

Vite proxies `/api` to `https://localhost:7041` by default; set `AAHAR_API_URL` to change the backend origin. The proxy accepts the development backend's self-signed certificate. Real authentication requires HTTPS: set `AAHAR_DEV_CERT` and `AAHAR_DEV_KEY` to paths for a locally trusted certificate and key before starting Vite, and open the frontend at `https://localhost:5173`. HTTP sample mode cannot authenticate to the API.

For example, after installing .NET 10 and approving trust of its local development certificate, export it into a private directory outside the repository:

```sh
dotnet dev-certs https --trust
mkdir -p "$HOME/.local/share/aahar/tls"
chmod 700 "$HOME/.local/share/aahar/tls"
umask 077
dotnet dev-certs https --export-path "$HOME/.local/share/aahar/tls/localhost.pem" --format PEM --no-password
export AAHAR_DEV_CERT="$HOME/.local/share/aahar/tls/localhost.pem"
export AAHAR_DEV_KEY="$HOME/.local/share/aahar/tls/localhost.key"
npm run dev -- --host localhost
```

Keep the private key private and never commit it. The trust step changes the
machine's certificate trust and can prompt for approval. Do not expose the
development server publicly. The frontend and API are same-origin from the
browser's perspective; the backend needs no CORS configuration for this setup.

The client expects cookie authentication and `GET /api/auth/csrf` to return a request token. It sends `X-CSRF-TOKEN` on mutations and refreshes the token after login and logout. API routes and DTOs are isolated in `src/api.ts`; the backend remains responsible for authorization and validation.

Signed-in users can change their own password. A successful change signs out the current browser and requires a fresh sign-in; other existing sessions remain active until they expire. If the result cannot be confirmed, sign out and try the new password before retrying.

The POS, tables, kitchen, menu catalogue, inventory, sales overview and guest menu use the API. Table availability comes from the server; order conflicts are reported rather than silently retried. Staff can cancel draft orders; managers and administrators can also cancel submitted orders and must choose whether to return stock or mark it as waste. Menu and table creation, menu edits, inventory adjustments and sales are available to managers and administrators.

The administrator and manager POS overview highlights queued kitchen tickets, bills ready for simulated payment, pending guest requests, low stock and available tables. It links to the relevant workflows. Counts are snapshots, not live notifications; use refresh for changes from other staff. A full guest request page is shown as 20+ rather than an exact total. Unavailable data is marked instead of shown as zero. Sample mode displays example service counts without connecting to inventory or guest requests.

Orders can be dine-in or takeaway. Draft items and kitchen notes can be edited before sending; sent orders produce kitchen tickets that progress through queued, preparing and ready before order completion. Reconstructed tickets are flagged: verify the actual kitchen state before acting, as their timestamps reflect order creation. Refresh the kitchen view to see other operators' changes.

Select an occupied table to open its running bill. On a server that exposes `roundCount`, staff can send additional items as separate kitchen rounds and settle the full bill once all rounds are ready. Older servers require completion before payment and cannot add rounds until upgraded. Pending round and payment recovery is kept in tab-scoped session storage, which may be lost when the tab closes; check the bill and kitchen before retrying uncertain operations.

Workspace settings are specific to this browser: auto-send new drafts (off by default), display density, default simulated payment method and automatic receipt preview. Auto-send does not mark food ready; if sending fails after saving, use the existing draft instead of creating another. Receipts are printable at 80 mm through the browser and are explicitly simulated, not tax invoices.

Administrators have **Management → Restaurant settings** for shared business
name, tagline, address, phone, email, website and receipt footer. These are
public business details displayed on the guest menu, staff breadcrumb and
receipt, not private account information or browser preferences. Guest contact
information stays in a compact expandable section. Aahar remains the product
brand. Save conflicts retain edits and require reloading/reapplying them; a
failed reload retains unsaved edits. Menus and workspace load the profile when
the page opens; receipt previews refresh it when opened. Reprints use those
current details, not historical identity snapshots. Profile failures are shown
explicitly, and the receipt print action stays disabled while details are unavailable. Sample
mode uses an independent fictional profile and cannot save shared settings.
Payment/receipt warnings are concise; the printed receipt retains one clear
simulated/not-tax label.

Service and management navigation are grouped separately; password change,
workspace settings and sign-out are in the account area. Green/red table
surfaces retain explicit Available/Occupied labels. The guest menu prioritizes
search, categories and items, with a sticky selection summary on desktop and a
cart shortcut on small screens.

For an older server, progress the ticket through Prepare and Ready in Kitchen,
then have an authorized manager or kitchen user mark the order complete in
Billing. Open its bill, review the full balance and record one simulated
payment. Paid orders retain **View receipt**, with **Print / Save as PDF**.
Receipt details scroll independently so Close and Print remain accessible.
The print document excludes the workspace; printer paper settings and actual
thermal hardware still need device-specific verification.

Guests can submit quoted requests for staff review using a takeaway or table-specific guest link. Staff acceptance creates a draft order, not a kitchen ticket; prices, availability and table occupancy are revalidated. Guest status lookup uses an access token held only in page memory, so closing or reloading the page loses lookup access. If submission has an uncertain network outcome, ask staff before trying again: the API has no idempotency or token recovery. Table links expose a table reference rather than a verified table number; guests should confirm it with staff.

The manager reports use an inclusive business-date range of at most 366 days. The API supplies the business timezone and UTC payment window; best-selling rows use the item name and price snapshots recorded at sale.

Managers can view low stock, change thresholds, record stock and waste, browse movement and threshold histories, manage suppliers and their change history, and record and browse purchase receipts. Quantities use the ingredient's exact unit; receipt totals and snapshots come from the server. Supplier references are case-sensitive and unique per supplier. If recording a receipt has an uncertain outcome, check receipt history and with staff before trying again; the API does not provide idempotent retry. The warning is kept only in the current page session and is lost on reload.

Managers can edit the ingredient quantities used per menu item in Manage recipes. Saving replaces the whole recipe; reload before saving when other managers may be editing. Stock is deducted using the recipe at order send time; an empty recipe causes no deduction.

Managers can upload or remove a menu item's PNG or JPEG image (up to 2 MiB) after creating the item. Item images appear in the POS and guest menu; a local placeholder is shown if an image cannot be loaded.

The guest menu QR is generated locally and encodes the same selected takeaway or table link; it does not contact a QR service. A localhost link or QR works only on the same computer, not on another device. Do not expose the development proxy to remote devices.

Cash and card payments are simulations. The paid order view provides a simulated receipt from the order and payment data, not an IRD-compliant invoice. Administrators can create staff accounts with a role and an administrator-supplied password; account listing and editing are not implemented in the frontend.
