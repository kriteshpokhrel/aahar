# Backend security assumptions

## Trust boundary

The intended deployment is one restaurant on one computer, with a localhost-bound HTTPS frontend and a same-origin `/api` proxy to the localhost HTTPS API. The API rejects non-loopback connections and non-HTTPS requests. The configured host allowlist is in `appsettings.json`. A network-exposed proxy defeats the loopback check because the API sees the proxy as the client; do not expose the frontend or proxy to other devices. Forwarded client headers are not a substitute for this boundary.

Protect the operating-system account, PostgreSQL access and backups, HTTPS private key, and ASP.NET Core data-protection keys. Someone who controls the host, application, browser session, database, or data-protection keys is outside the protections provided here. Default development key storage may not encrypt keys at rest. This design does not provide production hardening, isolation between local OS users, or protection against a compromised frontend or cross-site scripting.

Menu images are stored outside application paths, with names generated from numeric menu IDs. Only Admin/Manager can replace or remove them; anonymous retrieval is limited to available items. Input is size- and dimension-limited, decoded and normalized to PNG rather than serving uploaded bytes. The local image directory and its parent paths must remain protected from untrusted changes, including symlinks. Use a separate image directory per database.

## Authentication and requests

Staff passwords use ASP.NET Core Identity's password hasher; the bootstrap administrator requires a 12–256 character password, and bootstrap refuses to run when any user exists. The `__Host-aahar-auth` cookie is HttpOnly, Secure, SameSite=Strict, path `/`, with an eight-hour expiration and no sliding renewal. A cookie contains the authenticated name and role: there is no server-side session store, password reset, account disablement, role refresh, or individual session revocation. Signing out clears the browser cookie but does not invalidate a previously copied cookie. Treat a stolen cookie as valid until expiry unless its protecting keys are made unavailable using a separately planned operational response.

The separate `__Host-aahar-csrf` cookie has the same security attributes. Clients fetch `/api/auth/csrf`, send its request token in `X-CSRF-TOKEN` for POST, PUT, PATCH, and DELETE, and refresh after login or logout. This includes guest submission. CSRF protection does not defend against script executing in the same origin; never put tokens in URLs or logs. Role restrictions and response shapes are listed in [the API contract](BACKEND.md#http-contract). Authentication, CSRF, and role enforcement are separate checks.

Guest status uses a one-time-returned random 256-bit token, delivered in `X-Guest-Request-Token`; the database stores a SHA-256 hash, and comparison is fixed-time. Status output is limited, and bad tokens or unknown IDs return 404. Keep the token in memory and never log it; losing it prevents guest status lookup. The server marks responses `no-store`.

Login and guest submission have separate fixed-window limits of five attempts per minute per server process; guest status has 60 per minute. Guest submissions are bounded to 64 KiB, including streamed bodies. These are shared limits, not per-user or durable limits: they reset on restart and do not coordinate across processes. A local attacker can exhaust them and deny service to others. They are not a substitute for a network perimeter or multi-user abuse controls.

## Data integrity and remaining risks

Serializable transactions, uniqueness constraints, explicit transitions, and stock checks are intended to prevent double payment, conflicting decisions, and negative stock. They have not been verified against live PostgreSQL concurrency. Actor/reason history exists for selected workflows but is not a complete tamper-evident audit log. Users with database write access can bypass application rules. Guest requests lack expiration and submission idempotency; manual stock receipts also lack idempotency.

No sensitive credentials, request tokens, guest tokens, full connection strings, or database dumps belong in source control, chat, logs, or issue reports. Inspect proxy and application logging configuration before deployment. Review security, backup, and recovery procedures for the actual host before using real data. Payments are simulated and are not IRD certified.
