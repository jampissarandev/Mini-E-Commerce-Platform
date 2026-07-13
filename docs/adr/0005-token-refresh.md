# Authentication uses short-lived access tokens + silent refresh

Access tokens are short-lived JWTs (60-minute default). Refresh tokens are long-lived, single-use-per-rotation, server-side state, delivered as an `httpOnly`, `Secure`, `SameSite=Lax` cookie scoped to `/api/auth`. The frontend axios interceptor calls `POST /api/auth/refresh` on any 401 from a request that *was* authenticated; the response is a new access token (and a new refresh token via cookie rotation), and the original request is retried once.

**Why:** the storefront is a single-page app with no human in the loop during a 60-minute shopping session. Asking the customer to re-enter email + password because the JWT aged out is the single most common auth-related complaint in SPA storefronts. Silent refresh keeps the session alive for as long as the browser is open, and the `httpOnly` cookie path keeps the refresh token out of JS reach (XSS-resistant) without forcing the access token to follow it.

**Considered alternatives:**
- **60-min access token, re-login on expiry** (the current state) — rejected because it forces the customer back to `/login` mid-checkout, which is a real conversion killer.
- **Long-lived access token, no refresh** — rejected because it gives an XSS-stolen token weeks of validity, and there is no clean way to revoke a JWT.
- **Silent refresh in JS-readable storage** (e.g. `localStorage`) — rejected because it loses the XSS-resistance benefit of `httpOnly`; a stolen `localStorage` is a stolen token.

**Consequences:**
- New table `RefreshTokens { Id, CustomerId, TokenHash, ExpiresAt, RevokedAt?, CreatedAt, ReplacedById? }` in a Task 5a revision migration. Storing the **hash** of the token, not the token itself.
- New endpoints: `POST /api/auth/refresh` (cookie in, access token out) and `POST /api/auth/logout` (revokes the active refresh token, clears the cookie).
- On any successful refresh, the old refresh token is marked `RevokedAt` and `ReplacedById` points to the new one — this is the rotation that limits a stolen token to a single use.
- The `/api/auth/me` 401 interceptor in the frontend is the trigger; the retry policy is **once per request** (no infinite refresh loops).
- Refresh-token expiry is `appsettings.json` (`Jwt:RefreshExpiresInDays`, default 30). Access-token expiry stays 60 minutes.
- Logout must clear the cookie even if the access token is still valid, and revoke the active refresh token in the same request.
- Image storage, payment mock failure modes, and the other deferred items in `CONTEXT.md` are independent of this ADR.
