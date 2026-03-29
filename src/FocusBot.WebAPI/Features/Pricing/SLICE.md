# Pricing slice

- `GET /pricing` — anonymous; returns `PricingResponse` from Paddle Billing API (`GET /prices` filtered by `Paddle:CatalogProductId`) with 10-minute server cache.
- Implemented by `PaddleBillingApiClient` (HTTP + `IMemoryCache`).

Requires `Paddle:ApiBase`, `Paddle:ApiKey`, `Paddle:CatalogProductId`, and `Paddle:ClientToken` in configuration.
