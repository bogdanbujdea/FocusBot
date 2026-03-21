# Legal Pages — Paddle Domain Verification

## Why

Paddle provisionally approved the `foqus.me` domain but requires publicly accessible **Terms & Conditions**, **Privacy Policy**, and **Refund Policy** pages with footer links before payouts can be enabled. This is part of Paddle's standard domain verification process.

Reference: [Paddle Domain Review Guide](https://www.paddle.com/help/start/account-verification/what-is-domain-verification)

## What Was Added

### Pages (3 legal documents)

| Page | Route | Content |
|---|---|---|
| Terms & Conditions | `/terms` | Service description, account registration, subscription tiers, Paddle as MoR, acceptable use, IP, liability, termination, Romanian/EU governing law |
| Privacy Policy | `/privacy` | Data controller (NeuroQode SRL), data collected, GDPR legal basis, third-party processors (Supabase, Paddle, AI providers), data retention, GDPR rights, cookies, security |
| Refund Policy | `/refund` | 30-day money-back guarantee, how to request, Paddle processing, post-30-day cancellation policy, Paddle buyer protection |

### Sites Updated

Both sites host identical legal content, styled to match each site's theme:

**foqus-website (foqus.me)** — Marketing landing page
- Added `react-router-dom` dependency (site previously had no routing)
- Refactored `App.tsx` into routing shell + `LandingPage.tsx`
- Created `TermsPage.tsx`, `PrivacyPage.tsx`, `RefundPage.tsx` in `src/pages/`
- Expanded footer with legal links (Terms & Conditions, Privacy Policy, Refund Policy)
- Added `staticwebapp.config.json` for SPA fallback

**foqus-web-app (app.foqus.me)** — Cloud dashboard
- Created `TermsPage.tsx`, `PrivacyPage.tsx`, `RefundPage.tsx` in `src/pages/`
- Added `/terms`, `/privacy`, `/refund` as **public routes** (outside `ProtectedRoute`) in `App.tsx`
- Added legal links to the sidebar footer in `Layout.tsx`
- Added legal links below the login card in `LoginPage.tsx`

### Footer Links

Per Paddle's requirements, legal links appear in the footer of every page:
- **foqus-website**: Shared footer in `App.tsx` renders on all routes (landing + legal pages)
- **foqus-web-app**: Sidebar footer in `Layout.tsx` (authenticated pages) + login page footer (unauthenticated)

## Architecture Decisions

1. **Both sites host legal pages** — Rather than one site linking to the other, both foqus.me and app.foqus.me host the legal pages. This ensures crawlability from both domains and avoids cross-origin dependencies.

2. **react-router-dom added to foqus-website** — The marketing site had no router. Adding it enables clean `/terms`, `/privacy`, `/refund` URLs. Minimal overhead (~15KB gzipped). The landing page was extracted to `LandingPage.tsx` with no behavioral changes.

3. **Legal pages are public routes in the web app** — Placed alongside `/login` and `/auth/callback`, not behind `ProtectedRoute`. This ensures Paddle's crawler and unauthenticated visitors can access them.

4. **Content duplicated, styling separate** — The legal text is identical across both sites. CSS uses each site's design tokens (`--fb-*` for website, `--color-*` for web app). If content needs updating, both sites must be updated.

## Business Details

- **Entity**: SC NeuroQode Solutions SRL
- **Country**: Romania, European Union
- **Contact**: bogdan@neuroqode.ai
- **Merchant of Record**: Paddle.com (handles payments, invoicing, tax)
- **Refund policy**: 30-day money-back guarantee on first payment; cancel anytime after

## How to Update Legal Content

### File Locations

| Site | File | Path |
|---|---|---|
| Website | Terms | `src/foqus-website/src/pages/TermsPage.tsx` |
| Website | Privacy | `src/foqus-website/src/pages/PrivacyPage.tsx` |
| Website | Refund | `src/foqus-website/src/pages/RefundPage.tsx` |
| Web App | Terms | `src/foqus-web-app/src/pages/TermsPage.tsx` |
| Web App | Privacy | `src/foqus-web-app/src/pages/PrivacyPage.tsx` |
| Web App | Refund | `src/foqus-web-app/src/pages/RefundPage.tsx` |
| Website CSS | Legal styles | `src/foqus-website/src/pages/LegalPage.css` |
| Web App CSS | Legal styles | `src/foqus-web-app/src/pages/LegalPage.css` |

### When Updating

1. Update the "Last updated" date in the `<p className="legal-last-updated">` element
2. Update content in **both** website and web app versions (keep them in sync)
3. For material changes, consider notifying users by email per GDPR best practices
4. Rebuild and deploy both sites

## Verification Checklist

- [ ] `foqus.me/terms` — accessible, renders Terms & Conditions
- [ ] `foqus.me/privacy` — accessible, renders Privacy Policy
- [ ] `foqus.me/refund` — accessible, renders Refund Policy
- [ ] `foqus.me` footer — shows all three legal links
- [ ] `app.foqus.me/terms` — accessible without login
- [ ] `app.foqus.me/privacy` — accessible without login
- [ ] `app.foqus.me/refund` — accessible without login
- [ ] `app.foqus.me/login` — shows legal links below login card
- [ ] Dashboard sidebar — shows Terms, Privacy, Refund links
- [ ] Both sites build without errors
