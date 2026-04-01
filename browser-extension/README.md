# Foqus Browser Extension

Chrome/Edge extension (Manifest V3) for focus session tracking and alignment classification. Part of the [Foqus platform](../docs/platform-overview.md).

For full architecture, services, and implementation details, see [docs/browser-extension.md](../docs/browser-extension.md).

## Quick Start

```bash
npm install
npm run dev     # Development build with HMR (CRXJS)
npm run build   # Production build
npm test        # Run Vitest tests
```

## Load in Browser

1. `npm run build`
2. Open `chrome://extensions` or `edge://extensions`
3. Enable **Developer mode**
4. Click **Load unpacked** -> select `browser-extension/dist`

## Key Features

- Single active focus session with real-time alignment classification
- Dual-mode classification: BYOK (user API key) or Managed (Foqus account)
- Classification caching (SHA-256 keyed, IndexedDB)
- Distraction overlay on misaligned pages
- Desktop app integration via local WebSocket
- Cloud sync via SignalR hub
- Subscription/trial management via Foqus account

