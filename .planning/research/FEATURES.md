# Feature Landscape: Backtesting Engine

**Domain:** DCA strategy backtesting and parameter optimization
**Researched:** 2026-02-12
**Confidence:** MEDIUM (based on established backtesting domain knowledge; web search unavailable for verification of latest tools)

## Table Stakes

Features that any DCA backtesting engine must have. Without these, the tool does not answer the fundamental questions: "Does smart DCA beat fixed DCA?" and "What parameters are optimal?"

| Feature | Why Expected | Complexity | Dependencies on Existing Code | Notes |
|---------|--------------|------------|-------------------------------|-------|
| **Historical price data ingestion** | Cannot backtest without historical prices | Medium | Extends `DailyPrice` entity and `PriceDataService` | CoinGecko free API provides 2-4 years of daily BTC/USD OHLCV. Must handle rate limits (10-30 req/min on free tier) |
| **Single-run backtest execution** | Core function: simulate a strategy over a date range | Medium | Reuses multiplier logic from `DcaExecutionService.CalculateMultiplierAsync` | Input: strategy config + date range. Output: simulated purchase history |
| **Strategy configuration as input** | User must be able to specify what strategy to test | Low | Maps directly to existing `DcaOptions` shape | Accept: base amount, multiplier tiers, bear boost, MA period, lookback days, max cap |
| **Fixed DCA baseline comparison** | The primary question is "smart vs fixed" -- need both | Low | Fixed DCA is just smart DCA with all multipliers = 1.0 | Always run fixed DCA alongside any smart DCA backtest for comparison |
| **Core performance metrics** | Standard metrics that any financial backtest must report | Low | Computed from simulated purchase list | See Metrics section below |
| **Date range selection** | User must specify which period to backtest | Low | Filters on `DailyPrice.Date` | Default to max available data, allow sub-ranges |
| **API endpoint returning JSON** | Project explicitly targets API-first (no UI) | Low | Existing ASP.NET API infrastructure | One or more endpoints returning structured backtest results |
| **Deterministic replay** | Same inputs must produce identical outputs | Low | Multiplier logic is already pure/stateless | No randomness, no external calls during simulation. All data pre-loaded |
| **Day-by-day simulation loop** | DCA executes daily, so backtest must step through each day | Low | Iterates over `DailyPrice` rows in date order | For each day: compute multiplier from historical context, compute purchase amount, accumulate |

### Core Metrics (Table Stakes)

These metrics are standard across all DCA backtesting tools (dcabtc.com, portfoliovisualizer, custom implementations):

| Metric | What It Measures | Formula/Approach |
|--------|-----------------|------------------|
| **Total invested (USD)** | How much was spent | Sum of all daily `baseDailyAmount * multiplier` |
| **Total BTC accumulated** | How much BTC was acquired | Sum of all daily `amountSpent / priceOnDay` |
| **Average cost basis** | Effective buy price | `totalInvested / totalBtc` |
| **Current portfolio value** | BTC value at end of backtest period | `totalBtc * finalPrice` |
| **Total return (%)** | Overall profit/loss | `(portfolioValue - totalInvested) / totalInvested * 100` |
| **Cost basis vs lump sum** | Smart DCA average price vs if bought all on day 1 | Lower cost basis = strategy is working |
| **Smart DCA vs fixed DCA delta** | The key metric: improvement over naive DCA | Difference in cost basis, total BTC, and total return between strategies |
| **Number of purchases** | How many days purchases executed | Count of simulation days |
| **Average daily spend** | Actual average (accounting for multipliers) | `totalInvested / numberOfDays` |
| **Max single-day spend** | Largest purchase (when multiplier was highest) | Max of daily spend amounts |

## Differentiators

Features that elevate this beyond a basic backtest calculator. Not all are needed for v1.1, but they provide significant additional value for strategy optimization.

| Feature | Value Proposition | Complexity | Dependencies | Notes |
|---------|-------------------|------------|--------------|-------|
| **Parameter sweep / grid search** | Automatically find optimal multiplier tiers and thresholds | High | Requires single-run backtest as building block | Sweep: tier boundaries (e.g., 3-7% in 1% steps), multiplier values (1.2-2.0 in 0.1 steps), bear boost (1.0-2.0), MA period (100-300). Rank results by chosen metric |
| **Multiplier breakdown per run** | Show how often each tier was triggered, how much extra was spent per tier | Low | Extends simulation loop to track tier hit counts | Answers "how much did the 20%+ tier actually contribute?" |
| **Period-specific analysis** | Separate metrics for bull/bear/sideways market regimes | Medium | Needs market regime detection (price vs MA200) | Shows if strategy works differently in different conditions |
| **Drawdown analysis** | Maximum drawdown and drawdown duration | Medium | Track portfolio value over time, compute peak-to-trough | Important for understanding worst-case unrealized loss |
| **Time-weighted vs money-weighted returns** | IRR/XIRR for accurate return calculation | Medium | Each purchase is a cash flow with a date | More accurate than simple return % when investment amounts vary |
| **Efficiency ratio** | BTC per dollar efficiency vs fixed DCA | Low | Simple division comparison | `(btcFromSmart / usdSpentSmart) / (btcFromFixed / usdSpentFixed)` -- ratio > 1.0 means smart wins |
| **Simulated purchase log** | Full day-by-day detail of every simulated buy | Low | Output the simulation array directly | Each entry: date, price, multiplier, tier, amount spent, BTC bought, running totals |
| **Multiple strategy comparison** | Compare N different configs side by side | Medium | Run single backtest N times, aggregate results | API accepts array of strategy configs, returns comparative table |
| **Sweep result ranking** | Sort parameter sweep results by chosen optimization target | Low | Post-processing on sweep results | Sort by: total BTC, cost basis, return %, efficiency ratio |
| **Historical data caching in DB** | Persist CoinGecko data to avoid re-fetching | Low | `DailyPrice` entity already exists | Fetch once, reuse. Only fetch new days on subsequent runs |
| **Data staleness detection** | Warn if historical data has gaps or is outdated | Low | Check for missing dates in `DailyPrice` table | Return warning in API response if data quality is poor |

## Anti-Features

Features to deliberately NOT build for this milestone. Each has a clear rationale.

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| **Web UI / charts / visualization** | Project is API-first. User explicitly scoped this as JSON endpoints. Building a frontend is massive scope creep | Return structured JSON. User can visualize in Excel, Jupyter, or a future frontend |
| **Real-time / streaming backtests** | Backtests are batch computations over historical data. No need for WebSocket or streaming | Run synchronously or as background job. Return complete results |
| **Monte Carlo simulation** | Adds statistical complexity without clear value for a DCA strategy. DCA is deterministic given price data | Stick to historical replay. Monte Carlo is for stochastic strategies |
| **Intraday price simulation** | Project uses daily DCA. Sub-daily prices add complexity without value | Use daily OHLCV close price for each simulated purchase |
| **Slippage / fee modeling** | For small spot DCA orders ($10-45/day), slippage and fees are negligible (<0.1%). Modeling them adds false precision | Acknowledge in documentation that real results will differ slightly due to fees. Could add as a simple flat % deduction later if desired |
| **Forward-testing / paper trading integration** | Dry-run mode already exists for live forward testing. Backtesting is purely historical | Use existing `DryRun` mode for forward testing |
| **Genetic algorithm / ML optimization** | Massive overkill for a ~5 parameter DCA strategy. Grid search over reasonable ranges is sufficient | Use exhaustive grid search. The parameter space is small enough (hundreds to low thousands of combinations) |
| **Multi-asset backtesting** | Project is BTC-only. Multi-asset adds data management complexity | Keep single-asset. Extend later if needed |
| **PDF/CSV report export** | API returns JSON. Export formats are a presentation concern | JSON is sufficient. External tools can convert if needed |
| **Backtest result persistence** | Storing every backtest result in DB adds schema complexity | Return results in response. User can re-run. Consider caching sweep results in memory if they're slow |
| **User authentication for backtest API** | Bot is single-user, running locally. No multi-tenancy needed | Unauthenticated endpoints are fine for a personal tool |
| **Configurable buy frequency** | Strategy is daily DCA. Weekly/monthly adds simulation complexity | Hardcode daily frequency. This matches the live strategy |

## Feature Dependencies

```
Historical Data Pipeline:
  CoinGecko API Client ──> DailyPrice Storage (existing entity)
       │                         │
       │                         v
       │              Data Completeness Check
       │                         │
       v                         v
  Backfill 2-4 years ──> Incremental daily update

Single Backtest Engine:
  DailyPrice data (loaded)
       │
       v
  Strategy Config (DcaOptions shape)
       │
       v
  Day-by-day simulation loop:
    For each day in range:
      1. Compute 30-day high from prior prices  ──> existing logic
      2. Compute 200-day SMA from prior prices  ──> existing logic
      3. Determine multiplier tier              ──> existing logic
      4. Apply bear boost if below MA           ──> existing logic
      5. Cap at MaxMultiplierCap                ──> existing logic
      6. Calculate: amountSpent = base * multiplier
      7. Calculate: btcBought = amountSpent / closePrice
      8. Accumulate totals
       │
       v
  Compute summary metrics from accumulated data
       │
       v
  Return BacktestResult

Fixed DCA Baseline:
  Same engine with multiplier tiers = [] and bearBoost = 1.0
       │
       v
  Comparison metrics (smart vs fixed deltas)

Parameter Sweep:
  Define parameter ranges
       │
       v
  Generate all combinations (cartesian product)
       │
       v
  Run single backtest for each combination
       │
       v
  Collect results, sort by optimization target
       │
       v
  Return ranked list of SweepResult
```

**Critical path:**
1. CoinGecko data ingestion (nothing works without historical prices)
2. Single backtest engine with multiplier reuse (core deliverable)
3. Fixed DCA baseline comparison (answers the primary question)
4. API endpoints (exposes results)
5. Parameter sweep (optimization layer on top)

## Backtest Input Schema

What the user provides to run a backtest. Maps closely to existing `DcaOptions`:

```
BacktestRequest:
  # Date range
  startDate: DateOnly          # e.g., "2022-01-01"
  endDate: DateOnly            # e.g., "2025-12-31" (or null for "today")

  # Strategy parameters (mirrors DcaOptions)
  baseDailyAmount: decimal     # e.g., 10.0
  highLookbackDays: int        # e.g., 30
  bearMarketMaPeriod: int      # e.g., 200
  bearBoostFactor: decimal     # e.g., 1.5
  maxMultiplierCap: decimal    # e.g., 4.5
  multiplierTiers: [           # e.g., [{5, 1.5}, {10, 2.0}, {20, 3.0}]
    { dropPercentage, multiplier }
  ]

  # Comparison control
  includeFixedDcaBaseline: bool  # default: true
```

## Backtest Output Schema

What the API returns:

```
BacktestResult:
  # Input echo
  config: BacktestRequest
  dataRange: { actualStart, actualEnd, tradingDays }

  # Smart DCA results
  smart:
    totalInvested: decimal
    totalBtcAccumulated: decimal
    averageCostBasis: decimal
    finalPortfolioValue: decimal
    totalReturnPercent: decimal
    numberOfPurchases: int
    averageDailySpend: decimal
    maxSingleDaySpend: decimal
    multiplierBreakdown:        # how often each tier fired
      - { tier, timesTriggered, percentOfDays, totalExtraSpend }
    bearBoostDays: int          # days where bear boost was active

  # Fixed DCA baseline (if requested)
  fixed:
    totalInvested: decimal
    totalBtcAccumulated: decimal
    averageCostBasis: decimal
    finalPortfolioValue: decimal
    totalReturnPercent: decimal

  # Comparison
  comparison:
    costBasisDelta: decimal         # smart - fixed (negative = smart wins)
    costBasisDeltaPercent: decimal   # % improvement
    extraBtcAccumulated: decimal     # smart - fixed
    extraBtcPercent: decimal         # % more BTC from smart
    efficiencyRatio: decimal         # smart BTC/USD / fixed BTC/USD
    totalReturnDelta: decimal        # return % difference

  # Optional: full purchase log
  purchases: [                       # can be toggled off for sweep results
    { date, price, multiplier, tier, amountSpent, btcBought, runningTotalBtc, runningTotalInvested }
  ]
```

## Parameter Sweep Input/Output Schema

```
SweepRequest:
  # Date range (same as single backtest)
  startDate: DateOnly
  endDate: DateOnly
  baseDailyAmount: decimal

  # Parameter ranges to sweep
  highLookbackDaysRange: { min, max, step }       # e.g., {20, 40, 10}
  bearBoostFactorRange: { min, max, step }         # e.g., {1.0, 2.0, 0.25}
  maxMultiplierCapRange: { min, max, step }         # e.g., {3.0, 6.0, 0.5}
  tierDropPercentages: [[5,10,20], [3,7,15,25]]    # tier boundary sets to try
  tierMultiplierRange: { min, max, step }           # e.g., {1.2, 3.0, 0.2} for each tier

  # Optimization target
  optimizeFor: "costBasis" | "totalBtc" | "totalReturn" | "efficiencyRatio"

  # Limits
  maxCombinations: int          # safety cap, e.g., 10000

SweepResult:
  totalCombinationsTested: int
  executionTimeMs: long
  optimizedFor: string

  # Top N results ranked by optimization target
  topResults: [
    {
      rank: int
      config: { ...strategy params... }
      metrics: { ...same as BacktestResult.smart... }
      vsFixedDca: { ...same as BacktestResult.comparison... }
    }
  ]

  # Fixed DCA baseline (computed once, shared across all comparisons)
  fixedDcaBaseline: { ...metrics... }
```

## MVP Recommendation

For the v1.1 backtesting milestone, build in this order:

### Must Have (MVP)

1. **CoinGecko historical data ingestion** - Fetch and store 2-4 years of BTC daily OHLCV
   - Extends existing `DailyPrice` entity
   - Cache in PostgreSQL, only fetch gaps
   - Handle CoinGecko rate limits gracefully
   - Complexity: Medium

2. **Single backtest engine** - Core simulation loop reusing multiplier logic
   - Extract multiplier logic from `DcaExecutionService` into a pure, testable calculator
   - Day-by-day replay against historical prices
   - Produce full metrics and optional purchase log
   - Complexity: Medium

3. **Fixed DCA baseline** - Always compare against naive fixed-amount DCA
   - Trivially derived: same engine with no multipliers
   - Comparison metrics computed automatically
   - Complexity: Low

4. **Backtest API endpoint** - `POST /api/backtest` returning JSON
   - Accept strategy config + date range
   - Return `BacktestResult` with smart, fixed, and comparison sections
   - Complexity: Low

5. **Parameter sweep** - Grid search over multiplier parameters
   - Accept parameter ranges, generate combinations
   - Run backtest for each, rank results
   - Safety cap on max combinations to prevent runaway computation
   - Complexity: High (but high value -- answers the optimization question)

6. **Sweep API endpoint** - `POST /api/backtest/sweep` returning JSON
   - Accept sweep ranges
   - Return ranked results
   - Complexity: Low

### Defer to Post-v1.1

- **Period-specific analysis** (bull/bear regime breakdown) - Nice but not essential for v1.1
- **Drawdown analysis** - Valuable but adds complexity to metrics computation
- **XIRR / time-weighted returns** - More accurate but simple return % is sufficient for comparison
- **Multiple strategy comparison endpoint** - Can be achieved by calling single backtest endpoint multiple times
- **Sweep result caching** - Only needed if sweeps are slow (optimize later if needed)

## Complexity Assessment

| Feature | Complexity | Effort Estimate | Risk |
|---------|-----------|-----------------|------|
| CoinGecko data ingestion | Medium | 1-2 days | CoinGecko API rate limits, data format changes |
| Multiplier logic extraction (pure calculator) | Low | 0.5 days | Logic already exists, just needs refactoring |
| Single backtest simulation | Medium | 2-3 days | Edge cases: insufficient MA data at start of range, partial days |
| Core metrics computation | Low | 0.5 days | Straightforward arithmetic |
| Fixed DCA baseline + comparison | Low | 0.5 days | Subset of backtest engine |
| API endpoints | Low | 0.5 days | Standard ASP.NET minimal API |
| Parameter sweep engine | High | 2-3 days | Combinatorial explosion management, execution time |
| Sweep ranking and output | Low | 0.5 days | Sort and slice |
| Testing | Medium | 1-2 days | Need known-answer test cases with hand-calculated expected values |
| **Total estimate** | | **8-12 days** | |

## Key Design Decisions to Make During Implementation

1. **CoinGecko vs Hyperliquid for historical data**: CoinGecko has longer BTC history (10+ years). Hyperliquid is newer and may only have data from its launch. Use CoinGecko for backtesting, Hyperliquid for live prices. These are different data sources for different purposes.

2. **Multiplier logic extraction**: The `CalculateMultiplierAsync` in `DcaExecutionService` is currently async (queries DB). For backtesting, we need a pure synchronous version that takes pre-loaded price arrays. Extract to a static/pure method, have both live service and backtest engine call it.

3. **MA warm-up period**: The first ~200 days of a backtest range won't have enough data for 200-day SMA. Options: (a) require extra lead-in data before `startDate`, (b) fall back to 1.0x bear multiplier when insufficient data (matches live behavior), (c) skip those days. Recommend (b) for consistency with live behavior.

4. **Sweep parallelization**: Each backtest in a sweep is independent. Use `Parallel.ForEachAsync` or similar for CPU-bound parallelism. But cap concurrency to avoid memory pressure.

5. **Response size management**: A 4-year backtest with daily purchase log = ~1,460 entries. For single backtests, include the log. For sweeps testing thousands of combinations, omit individual purchase logs (just return summary metrics).

## Sources

**Confidence Note:** This analysis is based on established quantitative finance backtesting patterns and the specific codebase examination. WebSearch and WebFetch were unavailable for this research session.

**Domain knowledge sources (from training data, MEDIUM confidence):**
- Standard DCA backtesting metrics from tools like dcabtc.com, portfoliovisualizer.com
- Quantitative finance backtesting patterns (walk-forward, parameter sweep, grid search)
- CoinGecko API patterns for historical price data (free tier: daily OHLCV, 365-day limit per call, pagination for longer ranges)
- .NET API design patterns for computation-heavy endpoints

**Codebase examination (HIGH confidence):**
- `DcaOptions` and `MultiplierTier` configuration shape (exact parameters to sweep)
- `DcaExecutionService.CalculateMultiplierAsync` multiplier logic (reusable for simulation)
- `DailyPrice` entity OHLCV schema (reusable for historical data storage)
- `PriceDataService` data pipeline patterns (extendable for CoinGecko source)
- `appsettings.json` default parameter values (baseline for sweep ranges)

**Gaps requiring validation:**
- CoinGecko free tier rate limits may have changed (verify before implementation)
- CoinGecko API response format for `/coins/bitcoin/market_chart/range` endpoint (verify with official docs)
- Optimal sweep parameter ranges should be validated against BTC historical volatility data
