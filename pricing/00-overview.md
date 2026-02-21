# FocusBot Pricing Model

## Overview

FocusBot uses a dual-tier pricing model that balances accessibility with monetization:

| Tier | Price | Target User | Revenue |
|------|-------|-------------|---------|
| **Free (BYOK)** | $0 | Technical users with existing API access | $0 (adoption driver) |
| **Pro Subscription** | $4.99/month | Professionals who want convenience | Primary revenue |

## How It Works

### Free Tier (Bring Your Own Key)

Users provide their own API key from OpenAI, Anthropic, or other supported providers. This gives them:

- Full access to all features
- Unlimited usage (limited only by their API quota)
- Complete control over their AI provider and model selection

This tier exists because:
1. Many developers and power users already have API keys
2. It removes friction for trying the app
3. It generates positive reviews from the technical community
4. Zero cost to us as the developer

### Pro Subscription

Users pay $4.99/month via Windows Store for a hassle-free experience:

- No API key management required
- Unlimited AI-powered focus tracking
- Automatic billing via Microsoft account

This tier exists because:
1. Most professionals don't want to deal with API keys
2. They're willing to pay for convenience
3. Predictable monthly revenue

## Cost Analysis

### API Costs (gpt-4o-mini)

| Metric | Value |
|--------|-------|
| Cost per classification | ~$0.0001 (0.01 cents) |
| Typical user (50 calls/day × 20 days) | 1,000 calls/month = $0.10 |
| Heavy user (200 calls/day × 30 days) | 6,000 calls/month = $0.60 |

### Revenue per Subscriber

| Item | Amount |
|------|--------|
| Subscription price | $4.99 |
| Windows Store cut (15-30%) | -$0.75 to -$1.50 |
| API costs (heavy user) | -$0.60 |
| **Net revenue** | **$2.90 to $3.65** |

Even with heavy users and the maximum Store cut, each subscriber is profitable.

## Implementation Phases

The pricing model is implemented in four phases, each building on the previous:

1. **[Phase 1: Mode Selection](phase-1-mode-selection.md)** - UI infrastructure for switching between BYOK and subscription modes
2. **[Phase 2: Subscription Service](phase-2-subscription-service.md)** - Windows Store integration for subscription management
3. **[Phase 3: Managed Key](phase-3-managed-key.md)** - Embedded API key for subscribed users
4. **[Phase 4: Store Submission](phase-4-store-submission.md)** - Partner Center setup and store listing

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                        Settings UI                               │
│  ┌─────────────────────┐    ┌─────────────────────────────────┐ │
│  │ ○ Use my own key    │    │ ● Subscribe ($4.99/month)       │ │
│  │   [Provider ▼]      │    │   ✓ Active until Apr 22, 2026   │ │
│  │   [API Key ****]    │    │   [Manage Subscription]         │ │
│  │   [Model ▼]         │    │                                 │ │
│  └─────────────────────┘    └─────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      SettingsService                             │
│  • GetApiKeyModeAsync() → Own | Managed                          │
│  • GetApiKeyAsync() → user's key (Own mode only)                 │
└─────────────────────────────────────────────────────────────────┘
                              │
              ┌───────────────┴───────────────┐
              ▼                               ▼
┌─────────────────────────┐     ┌─────────────────────────────────┐
│   SubscriptionService   │     │     ManagedKeyProvider          │
│  • IsSubscribedAsync()  │     │  • GetApiKeyAsync()             │
│  • PurchaseAsync()      │     │  (obfuscated embedded key)      │
└─────────────────────────┘     └─────────────────────────────────┘
              │                               │
              ▼                               ▼
┌─────────────────────────────────────────────────────────────────┐
│                         LlmService                               │
│  if (mode == Own) → use user's key                               │
│  if (mode == Managed && subscribed) → use managed key            │
│  if (mode == Managed && !subscribed) → prompt to subscribe       │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
                         [OpenAI API]
```

## Security Considerations

### MVP Approach (Embedded Key)

For the initial release, the managed API key is embedded in the app with obfuscation:

- **Risk**: Determined attackers can extract the key
- **Mitigation**: Set billing limits on OpenAI, monitor usage

### Future Approach (Server Proxy)

When revenue justifies infrastructure investment, migrate to a server proxy:

- App sends classification requests to your server
- Server validates subscription with Microsoft Store API
- Server calls OpenAI with your key (never exposed to client)
- Server returns result to app

**Migration trigger**: Monthly revenue > $500 or suspicious usage detected.

## Windows Store Integration

FocusBot uses the `Windows.Services.Store` namespace for:

- Checking subscription status
- Triggering the purchase UI
- Handling subscription expiration and renewal

The subscription is created as an add-on in Partner Center with:
- Product ID: `focusbot.subscription.monthly`
- Price: $4.99/month
- Billing: Monthly, auto-renewing
