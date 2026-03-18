# Feature 4 -- Paddle Subscription + 24h Trial

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
- `subscription.created` -- Creates or updates subscription to active
- `subscription.updated` -- Updates subscription status
- `subscription.canceled` -- Marks subscription as canceled
- `transaction.completed` -- Updates current period end date

## Notes
- Paddle webhook signature verification is not implemented in this MVP slice (marked with TODO)
- User ID is passed to Paddle via `custom_data.user_id` in the subscription payload
