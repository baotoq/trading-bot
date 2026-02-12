# Requirements: BTC Smart DCA Bot — v1.1 Backtesting Engine

**Defined:** 2026-02-12
**Core Value:** Validate that smart DCA beats fixed DCA and find optimal multiplier parameters through historical backtesting

## v1.1 Requirements

Requirements for backtesting engine milestone. Each maps to roadmap phases.

### Historical Data Pipeline

- [ ] **DATA-01**: System can fetch 2-4 years of BTC daily price data from CoinGecko free API
- [ ] **DATA-02**: Fetched price data is persisted in existing DailyPrice PostgreSQL table
- [ ] **DATA-03**: Data ingestion is incremental — only fetches dates not already in database
- [ ] **DATA-04**: System detects and reports gaps in historical price data
- [ ] **DATA-05**: User can check available price data range via API endpoint

### Simulation Engine

- [ ] **SIM-01**: MultiplierCalculator extracted as pure static class reusable by both live DCA and backtest
- [ ] **SIM-02**: Backtest simulates day-by-day DCA purchases over a configurable date range
- [ ] **SIM-03**: Fixed DCA baseline is always computed alongside smart DCA for comparison
- [ ] **SIM-04**: Simulation is deterministic — same inputs always produce identical outputs
- [ ] **SIM-05**: Backtest reports core metrics: total invested, total BTC, average cost basis, portfolio value, return %
- [ ] **SIM-06**: Backtest reports smart vs fixed DCA comparison: cost basis delta, extra BTC %, efficiency ratio
- [ ] **SIM-07**: Backtest reports multiplier tier breakdown (how often each tier triggered, extra spend per tier)
- [ ] **SIM-08**: Backtest reports max drawdown (unrealized loss vs total invested)
- [ ] **SIM-09**: Backtest optionally includes full day-by-day simulated purchase log

### Parameter Sweep

- [ ] **SWEEP-01**: User can define parameter ranges (tier thresholds, multipliers, bear boost, lookback, MA period)
- [ ] **SWEEP-02**: System generates all valid combinations and runs backtest for each
- [ ] **SWEEP-03**: Results are ranked by user-chosen optimization target (cost basis, total BTC, return %, efficiency)
- [ ] **SWEEP-04**: Walk-forward validation splits data into train/test to prevent overfitting
- [ ] **SWEEP-05**: Sweep presets available (conservative, full) for common parameter ranges
- [ ] **SWEEP-06**: Safety cap on maximum combinations to prevent runaway computation

### API Endpoints

- [ ] **API-01**: POST /api/backtest — run single backtest with strategy config, return structured JSON
- [ ] **API-02**: POST /api/backtest/sweep — run parameter sweep, return ranked JSON results
- [ ] **API-03**: POST /api/backtest/data/ingest — trigger CoinGecko historical data ingestion
- [ ] **API-04**: GET /api/backtest/data/status — check available price data date range and completeness

## Future Requirements

Deferred to post-v1.1. Tracked but not in current roadmap.

### Advanced Analytics

- **ADV-01**: Period-specific analysis (separate metrics for bull/bear/sideways regimes)
- **ADV-02**: Time-weighted / money-weighted returns (IRR/XIRR)
- **ADV-03**: Multiple strategy comparison in single request (N configs side-by-side)
- **ADV-04**: Balance simulation mode (track USDC balance with optional monthly deposits)

### Export & Caching

- **CACHE-01**: Cache backtest results by parameter hash with TTL
- **EXPORT-01**: CSV/PDF export of backtest results

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| Web UI / charts / visualization | API-first; user can visualize JSON in Excel, Jupyter, etc. |
| Real-time / streaming backtests | Backtests are batch computations, no streaming needed |
| Monte Carlo simulation | DCA is deterministic given price data; Monte Carlo adds complexity without value |
| Intraday price simulation | Daily DCA only; sub-daily prices add noise without value |
| Slippage / fee modeling | For small spot orders ($10-45/day), fees are <0.1% — negligible |
| Genetic algorithm / ML optimization | Grid search is sufficient for ~5 DCA parameters |
| Multi-asset backtesting | BTC only, consistent with v1.0 scope |
| Backtest result persistence | Compute-on-fly is cheaper; user re-runs as needed |
| User authentication | Single-user personal tool; no multi-tenancy |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| DATA-01 | — | Pending |
| DATA-02 | — | Pending |
| DATA-03 | — | Pending |
| DATA-04 | — | Pending |
| DATA-05 | — | Pending |
| SIM-01 | — | Pending |
| SIM-02 | — | Pending |
| SIM-03 | — | Pending |
| SIM-04 | — | Pending |
| SIM-05 | — | Pending |
| SIM-06 | — | Pending |
| SIM-07 | — | Pending |
| SIM-08 | — | Pending |
| SIM-09 | — | Pending |
| SWEEP-01 | — | Pending |
| SWEEP-02 | — | Pending |
| SWEEP-03 | — | Pending |
| SWEEP-04 | — | Pending |
| SWEEP-05 | — | Pending |
| SWEEP-06 | — | Pending |
| API-01 | — | Pending |
| API-02 | — | Pending |
| API-03 | — | Pending |
| API-04 | — | Pending |

**Coverage:**

- v1.1 requirements: 24 total
- Mapped to phases: 0
- Unmapped: 24 (pending roadmap creation)

---
*Requirements defined: 2026-02-12*
*Last updated: 2026-02-12 after initial definition*
