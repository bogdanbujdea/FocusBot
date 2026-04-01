# Foqus Website

Marketing landing page at `foqus.me`. React 19 SPA with waitlist signup, no authentication, no backend integration beyond the waitlist API. Deployed as an Azure Static Web App.

---

## Routes

| Path | Component | Description |
|---|---|---|
| `/` | `LandingPage` | Hero, examples, coverage callout, anti-positioning, analytics preview, how-it-works, waitlist CTA |
| `/terms` | `TermsPage` | Terms & Conditions |
| `/privacy` | `PrivacyPage` | Privacy Policy (GDPR) |
| `/refund` | `RefundPage` | 30-day Refund Policy |

Footer with legal links is hidden on legal pages (replaced with "Back to Foqus" header link).

---

## Landing Page (`LandingPage.tsx`, 735 lines)

### Sections

1. **Hero** — tagline, subtitle, waitlist email form
2. **Classification examples** — 4 persona scenarios (writer + Facebook, writer + Excel, programmer + YouTube fun, programmer + YouTube tutorial) with Focused/Distracted verdict badges
3. **Coverage callout** — visual diagram showing platform coverage: "Extension (Browser tabs) — One session — Windows app (Foreground apps)"
4. **Anti-positioning** — 3 items: "doesn't block", "doesn't assume", "doesn't guilt"
5. **Analytics preview** — visual preview of focus tracking dashboard
6. **How it works** — 3 steps explaining the flow
7. **Footer CTA** — second waitlist signup form

### Waitlist Form

- `WaitlistSignupForm` component used twice (hero + footer CTA)
- Honeypot field for bot prevention
- Posts to `POST /api/waitlist` (Foqus WebAPI)
- Double opt-in: shows confirmation when `?accepted=true` query param is present
- States: idle → submitting → submitted (shows "check your email")

### Inline Components

| Component | Purpose |
|---|---|
| `WaitlistSignupForm` | Reusable email form with honeypot, loading state, confirmation |
| `IconExampleHead` | SVG persona avatars |
| `IconWhereBrand` | SVG brand icons (Facebook, Excel, YouTube) |
| `FoqusExampleVerdictBadge` | Focused/Distracted classification badge |
| `FoqusExampleTaskLinkGlyph` | Decorative arrow SVG |
| `FoqusExampleWhyIcon` | Circle check/info SVG |

---

## Legal Pages

Self-contained Terms, Privacy, and Refund pages. Content duplicated from web app (identical sections, entity, dates). Back link goes to landing page.

- **Entity**: SC NeuroQode Solutions SRL, Romania
- **Last updated**: March 21, 2026

---

## Styling

**No Tailwind, no CSS modules** — plain CSS with CSS custom properties.

| File | Lines | Purpose |
|---|---|---|
| `index.css` | 82 | Global reset, `:root` design tokens, focus ring |
| `App.css` | 1388 | All landing page + component styles, responsive breakpoints |
| `LegalPage.css` | 117 | Legal page layout + contact card |

### Design Tokens

| Token | Value | Purpose |
|---|---|---|
| `--fb-bg` | `#0f1220` | Page background |
| `--fb-text` | `#e7ecff` | Primary text |
| `--fb-text-muted` | `rgba(231,236,255,0.78)` | Secondary text |
| `--fb-border` | `rgba(255,255,255,0.12)` | Borders |
| `--fb-accent` | `#365cff` | CTA buttons |
| `--fb-aligned` | `#2db871` | Focused state (green) |
| `--fb-distracting` | `#ff6666` | Distracted state (red) |
| `--fb-surface-1/2/3` | White-alpha layers | Surface elevation |
| `--fb-radius-sm/md/lg` | `8px / 10px / 14px` | Corner radii |
| `--fb-sans` | Inter, Segoe UI, system-ui | Body font |
| `--fb-mono` | ui-monospace, Consolas | Code font |

**Aesthetic**: Dark glassy surfaces, `backdrop-filter: blur()`, subtle white-alpha borders, radial gradient hero decoration, color-coded focused/distracted states.

---

## Environment Configuration

| Variable | Purpose | Default |
|---|---|---|
| `VITE_FOQUS_API_BASE` | WebAPI base URL for waitlist | `""` (relative, dev), `"https://api.foqus.me"` (production) |

### Cross-Project Import

`LandingPage.tsx` imports the desktop app icon directly:
```typescript
import appIcon from "../../../FocusBot.App/Assets/1080.png";
```
This requires `fs.allow: [".."]` in Vite config to access files outside `src/foqus-website/`.

---

## Deployment (Azure Static Web App)

**Config**: `staticwebapp.config.json` (project root, copied to dist on build)

```json
{
  "navigationFallback": {
    "rewrite": "/index.html",
    "exclude": ["/assets/*", "/*.svg", "/*.ico", "/*.png", "/*.jpg"]
  },
  "globalHeaders": {
    "X-Content-Type-Options": "nosniff",
    "X-Frame-Options": "DENY",
    "Referrer-Policy": "strict-origin-when-cross-origin"
  }
}
```

- SPA fallback to `index.html` for client-side routing
- Security headers: nosniff, DENY framing, strict-origin referrer
- No auth routes or custom error pages

---

## Building

```bash
cd src/foqus-website

# Install
npm install

# Dev
npm run dev

# Build
npm run build

# Lint
npm run lint
```

**No tests** — no test framework or test files in this project.

---

## Key Dependencies

| Package | Version | Purpose |
|---|---|---|
| `react` | ^19.2.4 | UI framework |
| `react-dom` | ^19.2.4 | DOM rendering |
| `react-router-dom` | ^7.13.1 | Client-side routing |
