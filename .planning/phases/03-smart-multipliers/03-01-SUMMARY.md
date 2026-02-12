---
phase: 03-smart-multipliers
plan: 01
subsystem: database
tags: [ef-core, postgresql, hyperliquid, ohlcv, candles]

# Dependency graph
requires:
  - phase: 01-foundation
    provides: Hyperliquid client infrastructure with PostInfoAsync pattern
  - phase: 02-core-dca
    provides: Purchase entity and database context
provides:
  - DailyPrice entity with composite key for historical OHLCV data storage
  - HyperliquidClient.GetCandlesAsync method for fetching historical candles
  - Purchase multiplier metadata fields (MultiplierTier, DropPercentage, High30Day, Ma200Day)
  - Database migration for DailyPrice table and Purchase columns
affects: [03-02-calculation-service, 03-03-integration, smart-multiplier-logic]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Composite primary key (Date, Symbol) for time-series data without UUIDv7"
    - "CandleResponse → CandleData mapping pattern for API-to-domain model conversion"
    - "Multiplier audit trail via metadata fields on Purchase entity"

key-files:
  created:
    - TradingBot.ApiService/Models/DailyPrice.cs
    - TradingBot.ApiService/Infrastructure/Data/Migrations/20260212151619_AddDailyPriceAndPurchaseMultiplierMetadata.cs
  modified:
    - TradingBot.ApiService/Infrastructure/Hyperliquid/Models/HyperliquidModels.cs
    - TradingBot.ApiService/Infrastructure/Hyperliquid/HyperliquidClient.cs
    - TradingBot.ApiService/Models/Purchase.cs
    - TradingBot.ApiService/Infrastructure/Data/TradingBotDbContext.cs

key-decisions:
  - "DailyPrice uses composite key (Date, Symbol) instead of UUIDv7 BaseEntity"
  - "Non-nullable decimal fields with default 0 for multiplier metadata (0 = 'not calculated')"
  - "CandleData as intermediate type between raw API response and domain entity"
  - "All OHLCV decimal fields use precision(18,8) for price storage"

patterns-established:
  - "Time-series entity pattern: composite key with DateOnly + Symbol for partitioning"
  - "Audit trail pattern: metadata fields on aggregate root for decision transparency"

# Metrics
duration: 3min
completed: 2026-02-12
---

# Phase 3 Plan 1: Data Foundation for Smart Multipliers

**DailyPrice entity with OHLCV fields, Hyperliquid candle API method, and Purchase multiplier audit trail for smart multiplier calculation infrastructure**

## Performance

- **Duration:** 3 min
- **Started:** 2026-02-12T15:14:22Z
- **Completed:** 2026-02-12T15:17:02Z
- **Tasks:** 2
- **Files modified:** 8

## Accomplishments
- Created DailyPrice entity with composite key (Date, Symbol) for efficient time-series queries
- Implemented HyperliquidClient.GetCandlesAsync for fetching historical daily candles from Hyperliquid API
- Added Purchase multiplier metadata fields to track multiplier decision rationale
- Generated EF Core migration creating DailyPrice table and Purchase columns with proper decimal precision

## Task Commits

Each task was committed atomically:

1. **Task 1: Create DailyPrice entity and Hyperliquid candle models** - `ba8d930` (feat)
2. **Task 2: Add GetCandlesAsync to HyperliquidClient, update Purchase entity, configure DbContext, and create migration** - `91668eb` (feat)

## Files Created/Modified

### Created
- `TradingBot.ApiService/Models/DailyPrice.cs` - Time-series entity for historical OHLCV candle data with composite key (Date, Symbol)
- `TradingBot.ApiService/Infrastructure/Data/Migrations/20260212151619_AddDailyPriceAndPurchaseMultiplierMetadata.cs` - Migration creating DailyPrice table and Purchase multiplier columns

### Modified
- `TradingBot.ApiService/Infrastructure/Hyperliquid/Models/HyperliquidModels.cs` - Added CandleResponse (raw API JSON) and CandleData (parsed domain model) for candle data
- `TradingBot.ApiService/Infrastructure/Hyperliquid/HyperliquidClient.cs` - Added GetCandlesAsync method calling candleSnapshot endpoint
- `TradingBot.ApiService/Models/Purchase.cs` - Added MultiplierTier, DropPercentage, High30Day, Ma200Day fields for audit trail
- `TradingBot.ApiService/Infrastructure/Data/TradingBotDbContext.cs` - Added DailyPrice DbSet and configured both entities with proper keys and precision

## Decisions Made

**1. DailyPrice uses composite key (Date, Symbol) instead of UUIDv7 BaseEntity**
- Rationale: Time-series data naturally partitioned by date and symbol; UUIDv7 would add unnecessary overhead and less intuitive queries
- Trade-off: Cannot inherit from BaseEntity (which has UUIDv7 Id), but inherit from AuditedEntity directly

**2. Non-nullable decimal fields with default 0 for multiplier metadata**
- Rationale: C# decimal defaults to 0. For Phase 2 purchases without multiplier calculations, 0 correctly means "not calculated"
- Alternative: Nullable decimals would require null checks throughout calculation logic
- Decision: 0 is the sentinel value, MultiplierTier is nullable string (null is natural sentinel)

**3. CandleData as intermediate type between raw API and domain entity**
- Rationale: Separates API deserialization (CandleResponse) from domain model (CandleData → DailyPrice)
- Benefit: GetCandlesAsync returns CandleData for immediate use; persistence to DailyPrice is separate concern

**4. All OHLCV decimal fields use precision(18,8)**
- Rationale: Matches crypto exchange precision standards (up to 8 decimals for BTC prices)
- Consistency: Same precision as existing Price/Quantity fields in Purchase entity

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None - all tasks completed without issues.

## Next Phase Readiness

**Ready for Plan 02 (Calculation Service):**
- DailyPrice entity and database table ready for historical candle storage
- HyperliquidClient.GetCandlesAsync ready for data fetching
- Purchase entity has metadata fields for storing multiplier decision audit trail
- Database migration ready to apply (auto-migrates on startup)

**Blockers:** None

**Considerations for next plan:**
- Historical candle backfill strategy (how far back to fetch on bootstrap)
- Candle data refresh strategy (daily update vs on-demand fetch)
- Data validation (ensure no gaps in daily candles for accurate MA200 calculation)

---
*Phase: 03-smart-multipliers*
*Completed: 2026-02-12*
