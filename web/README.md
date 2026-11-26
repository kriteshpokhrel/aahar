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

The POS, tables, kitchen, menu catalogue, inventory, sales overview and read-only guest menu use the API. Table availability comes from the server; order conflicts are reported rather than silently retried. Staff can cancel draft orders; managers and administrators can also cancel submitted orders and must choose whether to return stock or mark it as waste. Menu and table creation, menu edits, inventory adjustments and sales are available to managers and administrators.

Cash and card payments are simulations. The paid order view provides a simulated receipt from the order and payment data, not an IRD-compliant invoice. Recipe management, staff administration, stock ledger browsing and QR generation are not implemented in the frontend. Guest menu links on localhost work only on the same computer.
