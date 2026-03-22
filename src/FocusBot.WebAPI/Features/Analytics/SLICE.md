# Analytics Slice

Aggregation endpoints for focus session analytics. All endpoints require authentication.

## Endpoints

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/analytics/summary` | Aggregated metrics for a date range |
| GET | `/analytics/trends` | Time-series data points grouped by day/week/month |
| GET | `/analytics/devices` | Per-device session breakdown |

## Query Parameters

All endpoints accept optional `from` and `to` (DateTime) parameters for date range filtering.

- **Summary**: Also accepts `deviceId` filter. Defaults to last 7 days.
- **Trends**: Also accepts `granularity` (daily/weekly/monthly) and `deviceId`. Defaults to last 30 days, daily granularity.
- **Devices**: Defaults to last 30 days.

## Data Source

All aggregations are computed on-read from completed sessions (where `EndedAtUtc IS NOT NULL`). No materialized views or pre-computed tables.

## Architecture

- `AnalyticsEndpoints.cs` — Minimal API endpoint registration
- `AnalyticsService.cs` — Aggregation logic
- `AnalyticsDtos.cs` — Request/response records
