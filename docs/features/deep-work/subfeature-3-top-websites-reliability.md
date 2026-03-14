# Subfeature 3: Top Distracting Websites and Reliability Hardening

## Goal

Finalize distraction analytics by adding:
- top distracting websites in session summary and daily panel,
- reliability safeguards so analytics remain correct after app restart/crash/sleep interruptions.

This subfeature completes the user-facing analytics story and hardens production behavior.

## User Value

Users can answer:
- "Which sites pull me off task most often?"
- "Can I trust today's analytics after app restarts?"

This turns app-level analytics into actionable web-behavior insights with confidence in data integrity.

## User Experience

## Session Summary Additions
- show `Top distracting websites` list (top 3 by duration),
- if domain extraction fails, use `Unknown Website` bucket.

## Daily Panel Additions
- add `Top distracting websites` section aligned with app-level section.

## Reliability Experience
- after unexpected app close/restart, today's analytics should still appear consistent,
- user should not see obvious count resets, spikes, or duplicate inflation.

## Behavioral Rules

### Website Extraction
- Use best-effort extraction from browser context:
  - inspect process name and window title snapshot,
  - parse known title/domain patterns where feasible.
- If parse fails: map to `Unknown Website`.
- Do not block event emit if domain cannot be parsed.

### Ranking
- Website ranking:
  1. distracted duration descending,
  2. event count descending,
  3. domain name ascending.

### Reconciliation Trigger
- On app startup (or resume), validate daily rollup consistency for current local day.
- If mismatch detected between source events/segments and rollup, rebuild rollup deterministically.

### Drift Tolerance
- Define exact drift detection strategy (example):
  - compare rollup totals against recomputed totals for current day,
  - if any metric differs, run full day rebuild.

## Data and Service Design

## Event Enrichment
Extend event model to carry website domain when available:
- `WebsiteDomain` nullable/string
- normalized lowercase storage for stable grouping

## Analytics Query Services
Add methods for:
- session top websites query,
- daily top websites query,
- recompute-and-reconcile current day rollup.

## Reconciliation Strategy

### Inputs
- canonical source:
  - distraction events,
  - focus segments/time-state tracking for duration counters.

### Process
1. compute expected local-day totals from canonical source,
2. compare to persisted rollup,
3. if mismatch, replace rollup with recomputed values in one transaction.

### Safety
- operation must be idempotent,
- deterministic ordering preserved after rebuild.

## Implementation Details

### Parser Design
- Implement domain parser as isolated service with clear contract:
  - input: process name + window title,
  - output: normalized domain or failure result.
- Keep parsing table/rules easy to extend for browsers used by FocusBot users.

### Time and Date Handling
- Continue using local-day boundaries from Subfeature 2.
- Ensure reconciliation and ranking use same normalization rules as incremental path.

### Observability (optional but recommended)
- add lightweight logs around:
  - parser success/failure counts,
  - reconciliation start/end and drift detected.

## Files Likely Affected

- `src/FocusBot.Infrastructure/Services` (domain parsing + reconciliation service)
- `src/FocusBot.Core/Interfaces` (parser/reconciliation contracts as needed)
- analytics query services used by session summary and dashboard
- session summary and board viewmodels for website lists
- `src/FocusBot.Infrastructure/Data/AppDbContext.cs` and migrations (if schema extended)

## Unit and Integration Test Plan (Mandatory)

Follow `.cursor/rules/focusbot-tests.mdc`.

### Website Parser Tests
- `ReturnDomain_WhenWindowTitleContainsParsableWebsite`
- `ReturnUnknown_WhenTitleCannotBeParsed`
- `NormalizeDomainToLowercase_WhenDomainExtracted`
- `HandleNonBrowserProcess_WhenWebsiteNotApplicable`

### Website Aggregation Tests
- `BucketUnderUnknownWebsite_WhenParserFails`
- `ReturnTopWebsitesOrderedByDurationThenCount_WhenDataExists`
- `MergeSameDomainAcrossEvents_WhenNormalizedDomainMatches`

### Reconciliation Tests
- `DoNotRebuild_WhenRollupMatchesCanonicalTotals`
- `RebuildRollup_WhenDriftDetectedForCurrentDay`
- `ProduceDeterministicTotals_WhenRebuildRunsMultipleTimes`
- `PreserveLocalDateBoundary_WhenRebuildingCrossMidnightData`

### Suggested Test Locations
- `tests/FocusBot.Infrastructure.Tests/Services/WebsiteDomainParserTests/*`
- `tests/FocusBot.Infrastructure.Tests/Services/DailyAnalyticsReconciliationServiceTests/*`
- existing analytics service tests for session/daily query extensions

## Manual User Test Checklist

1. During a session, browse distracting websites in browser tabs.
2. End session and verify top websites list includes expected domains.
3. Trigger at least one non-parsable title and verify `Unknown Website` appears.
4. During same day, verify dashboard website rankings align with behavior.
5. Restart app and verify today's totals and rankings remain consistent.
6. Optional cross-midnight test:
   - perform distractions before and after midnight,
   - verify correct day split and no duplication after restart.

## Acceptance Criteria

- Session summary and daily panel both show top distracting websites.
- `Unknown Website` fallback works and is visible when needed.
- Rollup reconciliation keeps daily analytics consistent after restart/crash.
- Unit/integration tests cover parser, website aggregation, and reconciliation edge cases.
- Existing analytics behavior from Subfeatures 1 and 2 remains stable.

## Non-Goals for This Subfeature

- Browser extension or deep URL capture.
- Cross-device/cloud analytics sync.
