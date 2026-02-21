# Phase 4: Partner Center & Store Submission

## Goal

Create the subscription add-on in Partner Center, test the complete purchase flow with sandbox accounts, and submit the app to the Windows Store.

## Prerequisites

Before starting this phase:

- [ ] Phase 1-3 complete and tested
- [ ] Microsoft Partner Center account (developer account, ~$19 one-time fee)
- [ ] App already submitted and approved (or ready for first submission)
- [ ] Privacy policy URL ready
- [ ] Bank account and tax information configured in Partner Center

## Part 1: Create Subscription Add-on

### Step 1: Access Partner Center

1. Go to [Partner Center](https://partner.microsoft.com/dashboard)
2. Navigate to **Apps and games** → Your app → **Add-ons**
3. Click **Create a new add-on**

### Step 2: Configure Add-on Identity

| Field | Value |
|-------|-------|
| **Product type** | Subscription |
| **Product ID** | `focusbot.subscription.monthly` |

Note: Product ID cannot be changed after creation. Use lowercase with dots.

### Step 3: Configure Properties

| Field | Value |
|-------|-------|
| **Subscription period** | Monthly |
| **Free trial** | Optional (consider 7 days) |
| **Product lifetime** | Forever (until cancelled) |

### Step 4: Configure Pricing

| Market | Price |
|--------|-------|
| **United States** | $4.99 |
| **Other markets** | Use auto-converted pricing or set manually |

Tips:
- Windows Store takes 15% cut for apps earning < $25M
- Consider regional pricing (lower for developing markets)
- Price changes take ~24 hours to propagate

### Step 5: Store Listing

Create listing for each language you support:

**Title**: FocusBot Pro

**Description**:
```
Unlock AI-powered focus tracking without managing API keys.

Included in your subscription:
- Unlimited AI classifications
- Automatic setup - no API key needed
- Priority support

Cancel anytime from your Microsoft account.
```

**Keywords**: focus, productivity, AI, subscription, pro

### Step 6: Submit Add-on

1. Review all sections
2. Click **Submit to the Store**
3. Wait for certification (typically 1-3 business days)

## Part 2: Update Main App Listing

### Description Updates

Add pricing information to your main app description:

```
## Pricing

**Free (Bring Your Own Key)**
Use your own AI API key from OpenAI, Anthropic, or other providers.
Full features, unlimited usage, $0/month.

**FocusBot Pro ($4.99/month)**
No API key needed - we handle everything.
Unlimited AI-powered focus tracking, cancel anytime.
```

### Screenshots

Add screenshots showing:
1. Settings page with both pricing options
2. Subscription active state
3. Focus tracking in action

### Privacy Policy Updates

Your privacy policy must disclose:

```
## AI Services and Data Processing

When using FocusBot Pro (subscription):
- Window titles and task descriptions are sent to OpenAI for classification
- Data is processed according to OpenAI's privacy policy
- We do not store your window activity data on our servers

When using your own API key:
- Data is sent directly to your chosen AI provider
- Subject to that provider's privacy policy

## Subscription Management

FocusBot Pro subscriptions are managed through Microsoft Store.
You can cancel anytime from your Microsoft account settings.
Subscription fees are non-refundable for partial billing periods.
```

## Part 3: Testing with Sandbox

### Configure Test Accounts

1. In Partner Center, go to **Settings** → **Developer settings** → **Xbox Live** → **Sandbox**
2. Create test accounts or use existing Microsoft accounts
3. Add test accounts to the sandbox

### Testing Flow

1. **Install Test Build**
   - Build app in Debug configuration with real `SubscriptionService`
   - Side-load or use Partner Center flight ring

2. **Sign In with Test Account**
   - On test device, sign in to Microsoft Store with test account
   - App should show "Not subscribed" state

3. **Test Purchase**
   - Click "Subscribe Now"
   - Complete purchase flow (uses test payment)
   - Verify subscription shows as active

4. **Test Classification**
   - Create a task
   - Switch windows
   - Verify AI classification works with managed key

5. **Test Expiration**
   - In Partner Center, expire the test subscription
   - Verify app correctly shows expired state
   - Verify classification prompts to resubscribe

6. **Test Renewal**
   - In Partner Center, renew test subscription
   - Verify app detects renewal

### Common Issues

| Issue | Solution |
|-------|----------|
| "Product not found" | Add-on not yet published or wrong Store ID |
| Purchase succeeds but app doesn't detect | Cache issue - clear Store cache or restart app |
| Subscription shows wrong status | Check license cache expiration logic |

## Part 4: Pre-submission Checklist

### Code Review

- [ ] No hardcoded test keys in release build
- [ ] Obfuscated managed key works correctly
- [ ] Error messages are user-friendly
- [ ] All logging is appropriate (no sensitive data)

### WACK Testing

Run Windows App Certification Kit:

```powershell
# Run WACK from command line
"C:\Program Files (x86)\Windows Kits\10\App Certification Kit\appcert.exe" `
    test -appxpackagepath "path\to\your.msix" `
    -reportoutputpath "wack-report.xml"
```

Common WACK issues:
- Missing privacy policy URL
- Incorrect capabilities declared
- Performance issues on startup
- Missing visual assets

### Manual Testing Matrix

| Scenario | Own Key | Pro Subscribed | Pro Not Subscribed |
|----------|---------|----------------|-------------------|
| Classification works | ✓ | ✓ | ✗ (shows subscribe) |
| Settings UI correct | ✓ | ✓ | ✓ |
| Mode persists | ✓ | ✓ | ✓ |
| Offline handling | ✓ | Cached | Cached |

### Accessibility

- [ ] All UI elements have automation names
- [ ] High contrast themes work
- [ ] Keyboard navigation works
- [ ] Screen reader compatible

## Part 5: Submit to Store

### Create Submission

1. In Partner Center, go to your app
2. Click **Start new submission**
3. Upload new MSIX package
4. Update all relevant sections

### Submission Notes

Add certification notes explaining:

```
This update adds subscription support:
- Users can choose between free (own API key) or Pro subscription
- Subscription is a monthly add-on at $4.99
- AI features work with either option

To test Pro subscription:
1. Go to Settings
2. Select "Subscribe to FocusBot Pro"
3. Complete purchase
4. AI classification should work automatically
```

### Age Rating

Update age rating questionnaire if needed:
- App uses AI services (online services: yes)
- No user-generated content displayed to others
- No in-app purchases of physical goods

### Wait for Certification

- Typical time: 1-3 business days
- Check Partner Center for status updates
- Address any certification failures

## Part 6: Post-Launch

### Monitor Metrics

Track in Partner Center:
- Subscription acquisitions
- Subscription churn
- Revenue by market
- Ratings and reviews

### Monitor Costs

Track in OpenAI dashboard:
- Daily API usage
- Cost per day/week/month
- Compare to subscription revenue

### Cost Alert Thresholds

Set up alerts:
- 50% of monthly limit: Warning
- 75% of monthly limit: Alert
- 90% of monthly limit: Critical

### Respond to Reviews

Common subscription questions:
- "How do I cancel?" → Microsoft account settings
- "Do you offer refunds?" → Contact Microsoft support
- "Can I share with family?" → No, subscription is per-account

## Pricing Strategy Notes

### Windows Store Revenue Split

| Annual Revenue | Microsoft Cut | Your Revenue |
|----------------|---------------|--------------|
| < $25M | 15% | 85% |
| ≥ $25M | 30% | 70% |

For a $4.99 subscription:
- Microsoft takes: $0.75
- You receive: $4.24

### Break-even Analysis

| Subscribers | Monthly Revenue | API Costs (Heavy) | Net |
|-------------|-----------------|-------------------|-----|
| 10 | $42.40 | $6.00 | $36.40 |
| 100 | $424.00 | $60.00 | $364.00 |
| 1000 | $4,240.00 | $600.00 | $3,640.00 |

Even with conservative estimates, the model is highly profitable.

### Future Pricing Options

Consider for the future:
- **Annual subscription** ($49.99/year = 2 months free)
- **Regional pricing** (lower in developing markets)
- **Free trial** (7 days to convert more users)

## File Changes Summary

This phase is primarily Partner Center and Store configuration. No code changes required beyond what's in Phase 1-3.

| Action | Location | Description |
|--------|----------|-------------|
| Create | Partner Center | Subscription add-on |
| Update | Partner Center | App listing description |
| Update | Privacy policy URL | Add AI data processing disclosure |
| Create | Partner Center | Store screenshots |
| Run | Local | WACK testing |
| Submit | Partner Center | New app submission |

## Definition of Done

- [ ] Subscription add-on created in Partner Center
- [ ] Add-on pricing configured
- [ ] Add-on store listing complete
- [ ] Test accounts configured
- [ ] Purchase flow tested in sandbox
- [ ] Subscription detection tested
- [ ] Classification with managed key tested
- [ ] Expiration/renewal tested
- [ ] Main app listing updated with pricing info
- [ ] Privacy policy updated
- [ ] Screenshots added
- [ ] WACK testing passed
- [ ] App submitted to Store
- [ ] Certification passed
- [ ] App live in Store
- [ ] Cost monitoring configured
- [ ] Revenue tracking configured

## Launch Checklist

Before announcing:
- [ ] App live and downloadable
- [ ] Subscription purchasable
- [ ] End-to-end flow verified on clean install
- [ ] Support process ready for subscription questions
- [ ] OpenAI billing limits in place
- [ ] Monitoring dashboards set up
