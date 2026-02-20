---
phase: 30-critical-bug-fixes
plan: 01
subsystem: api, ui
tags: [flutter, dotnet, portfolio, fixed-deposit, donut-chart, asset-creation]

# Dependency graph
requires:
  - phase: 29-flutter-portfolio-ui
    provides: "add_transaction_screen, allocation_donut_chart, portfolio_repository, PortfolioEndpoints"
provides:
  - "CompoundingFrequency enum aligned between Flutter ('Simple') and backend enum"
  - "AllocationDto carries both valueUsd and valueVnd for currency-aware tooltip"
  - "POST /api/portfolio/assets endpoint for creating new portfolio assets"
  - "Flutter 'New Asset' form mode in add entry screen"
affects: [portfolio, flutter-mobile, api-endpoints]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Parallel USD/VND allocation tracking dictionaries in summary endpoint"
    - "Three-mode SegmentedButton form (transaction, fixedDeposit, asset)"

key-files:
  created: []
  modified:
    - TradingBot.ApiService/Endpoints/PortfolioDtos.cs
    - TradingBot.ApiService/Endpoints/PortfolioEndpoints.cs
    - TradingBot.Mobile/lib/features/portfolio/data/models/portfolio_summary_response.dart
    - TradingBot.Mobile/lib/features/portfolio/presentation/widgets/allocation_donut_chart.dart
    - TradingBot.Mobile/lib/features/portfolio/presentation/sub_screens/add_transaction_screen.dart
    - TradingBot.Mobile/lib/features/portfolio/data/portfolio_repository.dart

key-decisions:
  - "allocationsByTypeVnd tracked as parallel dictionary to allocationsByType (USD) in GetSummaryAsync — no schema change needed"
  - "createAsset repository method returns Map<String,dynamic> (not a typed DTO) since asset creation is infrequent and avoids a new model class"
  - "New Asset tab uses else branch after else if fixedDeposit — clean three-way conditional without nesting"

patterns-established:
  - "Parallel currency tracking: for each USD allocation dict, maintain a VND dict updated in same loop"

requirements-completed: [PORT-01, PORT-03, DISP-03, DISP-05]

# Metrics
duration: 3min
completed: 2026-02-20
---

# Phase 30 Plan 01: Critical Bug Fixes Summary

**Three v4.0 integration gaps closed: CompoundingFrequency enum aligned to 'Simple', donut chart tooltip shows correct USD/VND value, and POST /api/portfolio/assets endpoint added with Flutter 'New Asset' form**

## Performance

- **Duration:** 3 min
- **Started:** 2026-02-20T16:58:23Z
- **Completed:** 2026-02-20T17:01:22Z
- **Tasks:** 2
- **Files modified:** 6

## Accomplishments

- Fixed Flutter fixed-deposit form sending 'None' instead of 'Simple' — now backend accepts the request (no more 400)
- Added ValueVnd to AllocationDto so donut chart tooltip correctly shows VND amounts in VND mode and USD amounts in USD mode
- Added POST /api/portfolio/assets endpoint with input validation, duplicate ticker check, and 201 response
- Added "New Asset" third tab to the add entry screen with name/ticker/asset type/currency fields and submitAsset() function

## Task Commits

Each task was committed atomically:

1. **Task 1: Fix CompoundingFrequency enum mismatch and donut chart tooltip currency bug** - `80b0611` (fix)
2. **Task 2: Add POST /api/portfolio/assets endpoint and Flutter asset creation UI** - `b31ff94` (feat)

**Plan metadata:** (docs commit follows)

## Files Created/Modified

- `TradingBot.ApiService/Endpoints/PortfolioDtos.cs` - Added ValueVnd to AllocationDto, added CreateAssetRequest and CreateAssetResponse DTOs
- `TradingBot.ApiService/Endpoints/PortfolioEndpoints.cs` - Added allocationsByTypeVnd parallel tracking, added CreateAssetAsync handler and route
- `TradingBot.Mobile/lib/features/portfolio/data/models/portfolio_summary_response.dart` - Added valueVnd field to AllocationDto with fromJson parsing
- `TradingBot.Mobile/lib/features/portfolio/presentation/widgets/allocation_donut_chart.dart` - Tooltip now uses valueVnd vs valueUsd based on isVnd flag
- `TradingBot.Mobile/lib/features/portfolio/presentation/sub_screens/add_transaction_screen.dart` - FormMode.asset enum value, 'New Asset' segment, asset form fields, submitAsset() function
- `TradingBot.Mobile/lib/features/portfolio/data/portfolio_repository.dart` - Added createAsset() method calling POST /api/portfolio/assets

## Decisions Made

- `allocationsByTypeVnd` tracked as a parallel dictionary to `allocationsByType` in `GetSummaryAsync` — computed in the same per-asset loop without any schema or model changes
- `createAsset` in Flutter repository returns `Map<String,dynamic>` rather than a typed model — avoids creating a new PortfolioAssetCreatedResponse class for an infrequently used call
- Form conditional restructured from `if/else` to `if/else if/else` to accommodate the third asset mode cleanly

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- All three v4.0 milestone audit gaps are now closed
- Fixed deposit creation works end-to-end (Flutter sends 'Simple', backend parses it correctly)
- Donut chart shows correct currency-denominated values in tooltip
- Users can create new portfolio assets directly from the mobile app without needing manual DB inserts

---
*Phase: 30-critical-bug-fixes*
*Completed: 2026-02-20*
