# Subscriptions & Paddle Billing

## Goal
Allow users to activate a free 24-hour trial and manage paid subscriptions via Paddle billing webhooks.

## Endpoints

| Method | Path                            | Auth     | Description                       |
|--------|---------------------------------|----------|-----------------------------------|
| GET    | /subscriptions/status           | Required | Current subscription status       |
| POST   | /subscriptions/trial            | Required | Activate 24-hour trial            |
| POST   | /subscriptions/paddle-webhook   | Anonymous| Paddle webhook receiver           |

## Subscription Statuses
- `none` -- No subscription record exists
- `trial` -- 24-hour trial active
- `active` -- Paid subscription active via Paddle
- `expired` -- Subscription expired or paused
- `canceled` -- Subscription canceled

## Trial Rules
- Each user gets exactly one 24-hour trial
- Attempting to activate a trial when a subscription record already exists returns 409
- Trial status is checked by comparing `TrialEndsAtUtc` against `DateTime.UtcNow`

## Classification Gate
The `/classify` endpoint checks subscription status when operating in managed mode (no `X-Api-Key` header).
Returns 402 Payment Required if the user has no active subscription or trial.

## Paddle Webhook Events Handled

Webhooks are deserialized using strongly-typed C# models (`PaddleWebhookModels.cs`):

- `subscription.created` — New subscription starts; status mapped (`"trialing"` → `"trial"`, `"active"` → `"active"`)
- `subscription.updated` — Plan or billing changes
- `subscription.canceled` — Subscription canceled
- `transaction.completed` — Payment succeeds; enriches billing period and payment method

## Webhook Processing

- **Signature verification**: HMAC-SHA256 with `Paddle-Signature` header (see `PaddleWebhookVerifier`)
- **Status mapping**: Paddle statuses are mapped to app conventions (`MapSubscriptionStatus`)
- **Plan type**: Resolved from `custom_data.plan_type` (subscription or price level), with `license` fallback
- **Payment details**: Card type and last 4 digits extracted from nested `method_details.card.last4`
- **SignalR**: `PlanChanged` event sent after DB updates so desktop/web clients refresh immediately

## Custom Data

Pass from checkout (Paddle.js `customData`):

- `user_id` — App user guid (string)
- `plan_type` — `"cloud-byok"` or `"cloud-managed"` (maps to `PlanType` enum)
