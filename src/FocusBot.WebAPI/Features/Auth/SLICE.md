# Auth Slice

## Endpoints

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| GET | `/auth/me` | Required | Returns current user profile |

## `GET /auth/me`

Returns the authenticated user's profile. On first call, auto-provisions the user from JWT claims.

**Response 200:**
```json
{
  "userId": "uuid",
  "email": "user@example.com",
  "subscriptionStatus": "none|trial|active|expired"
}
```

**Response 401:** Missing or invalid JWT.

## User Auto-Provisioning

When an authenticated request arrives, the `AuthService` checks for an existing user by `sub` claim (Supabase user ID). If not found, creates a new `User` row from JWT claims. Handles concurrent first-requests gracefully via catch-and-retry on unique constraint violation.

## JWT Configuration

- Algorithm: HS256
- Issuer: `{Supabase:Url}/auth/v1`
- Audience: `authenticated`
- Signing key: `Supabase:JwtSecret` (symmetric)

## Browser Extension Client

When a user chooses the **FocusBot account** mode in the browser extension, the extension:

- Authenticates the user with Supabase using a magic link sent to their email.
- Stores the Supabase access token locally inside the extension.
- Sends that token as `Authorization: Bearer <access_token>` when calling `GET /auth/me`.

On the first successful call from the extension:

- `AuthService` looks up the user by Supabase `sub` claim.
- If no row exists, a new `User` record is created from the JWT claims.
- The response is returned to the extension, which displays the authenticated email and subscription status and can later use the same identity for multi-device sync.
