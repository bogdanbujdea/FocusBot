# Foqus Deep Work Browser Extension (MVP)

Standalone Chrome/Edge extension implementing:

- single active focus session with one task,
- active tab URL/title tracking,
- OpenAI alignment classification (`aligned` or `distracting`),
- local classification cache,
- end-of-session summary (focus %, distraction count, context switch cost),
- daily analytics for today / last 7 days / last 30 days,
- user-managed OpenAI API key and excluded domains.

## Development

```bash
npm install
npm run build
npm run test
```

## Load in browser

1. Build extension: `npm run build`
2. Open `chrome://extensions` or `edge://extensions`
3. Enable Developer mode
4. Load unpacked extension from `browser-extension/dist`
