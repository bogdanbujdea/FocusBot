# Foqus landing page (`foqus-website`)

Vite/React landing page for Foqus, including a MailerLite-backed waitlist signup.

## Development

```bash
cd src/foqus-website
npm install
npm run dev
```

## Production build

```bash
cd src/foqus-website
npm run build
```

## Waitlist (MailerLite)

The landing page submits emails to your backend endpoint:

- `POST /api/waitlist` with JSON `{ "email": "user@example.com", "company": "" }`
  - `company` is a hidden honeypot field; it must remain empty.

### Frontend configuration

The landing page can point at an API base URL via:

- `VITE_FOQUS_API_BASE`

If `VITE_FOQUS_API_BASE` is unset, the site will call a relative URL (same origin).

### Backend configuration

The backend must be configured to call MailerLite server-side:

- `MailerLite:ApiKey`: MailerLite API token (Bearer token)
- `MailerLite:WaitlistGroupId`: MailerLite Group ID to add subscribers to

Production: Terraform sets `MailerLite__ApiKey` and `MailerLite__WaitlistGroupId` on the API container; CI passes `MAILERLITE_WAITLIST_GROUP_ID` (GitHub variable) as `TF_VAR_mailerlite_waitlist_group_id`.

The backend upserts subscribers using MailerLite’s API docs: [Subscribers](https://developers.mailerlite.com/docs/subscribers.html).

### Double opt-in for API signups

To send confirmation emails for API-based signups, enable **Double opt-in for API and integrations** in MailerLite:

- Account settings → Subscribe settings → toggle **Double opt-in for API and integrations** to ON

Reference: [How to use double opt-in when collecting subscribers](https://www.mailerlite.com/help/how-to-use-double-opt-in-when-collecting-subscribers)
