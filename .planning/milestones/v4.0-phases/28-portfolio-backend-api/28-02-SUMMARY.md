---
requirements-completed: [PORT-04, PORT-05]
---

# Phase 28 Plan 02 — Summary

**Completed:** 2026-02-20
**Duration:** Single pass, zero errors

## What Was Built

Complete API surface for portfolio management and fixed deposit CRUD, ready for Flutter app consumption:

### Portfolio Endpoints (`/api/portfolio`)

1. **GET /summary** — Cross-currency portfolio summary:
   - Loads all PortfolioAssets with transactions
   - Triggers historical migration when BTC asset exists with no bot-imported transactions
   - Fetches live prices from CoinGecko (crypto) and VNDirect (ETF) providers
   - Fetches USD/VND exchange rate for cross-currency conversion
   - Includes active fixed deposits in total (computed via InterestCalculator)
   - Returns: total value/cost/P&L in both USD and VND, allocation percentages by asset type, exchange rate timestamp
   - Graceful degradation: price feed failures use 0 for that asset, don't fail entire endpoint

2. **GET /assets** — Per-asset breakdown:
   - Net quantity (buys - sells), weighted average cost, current price
   - Current value in both USD and VND
   - Unrealized P&L (absolute USD + percentage)
   - Price staleness indicator from PriceFeedResult

3. **POST /assets/{id}/transactions** — Manual transaction creation:
   - Validates: currency enum, transaction type enum, no future dates, no fractional ETF quantities
   - Returns 201 Created with transaction response
   - Returns 404 if asset not found, 400 for validation errors

### Fixed Deposit Endpoints (`/api/portfolio/fixed-deposits`)

4. **GET /** — List all fixed deposits ordered by maturity date
5. **GET /{id}** — Single fixed deposit by ID
6. **POST /** — Create with validation (CompoundingFrequency enum, VndAmount, domain validation)
7. **PUT /{id}** — Update all fields with same validation as create
8. **DELETE /{id}** — Hard delete

All fixed deposit responses include computed values:
- AccruedValueVnd (as of today, via InterestCalculator)
- ProjectedMaturityValueVnd (at maturity date)
- DaysToMaturity

### DTOs

- `PortfolioSummaryResponse`, `AllocationDto`, `PortfolioAssetResponse`
- `CreateTransactionRequest`, `TransactionResponse`
- `CreateFixedDepositRequest`, `UpdateFixedDepositRequest`, `FixedDepositResponse`

## Files Created

| File | Purpose |
|------|---------|
| `Endpoints/PortfolioDtos.cs` | Request/response DTOs for portfolio endpoints |
| `Endpoints/PortfolioEndpoints.cs` | Portfolio summary, assets list, transaction creation |
| `Endpoints/FixedDepositDtos.cs` | Request/response DTOs for fixed deposit endpoints |
| `Endpoints/FixedDepositEndpoints.cs` | Fixed deposit CRUD endpoints |

## Files Modified

| File | Change |
|------|--------|
| `Program.cs` | Added `MapPortfolioEndpoints()` and `MapFixedDepositEndpoints()` registrations |

## Verification

- `dotnet build TradingBot.slnx` — 0 errors
- `dotnet test` — all 76 existing tests pass
- All endpoints protected by `ApiKeyEndpointFilter` (x-api-key header required)
- Summary endpoint correctly triggers historical migration on first call

## Decisions Made

- CoinGecko ID mapping via static dictionary (BTC=bitcoin, ETH=ethereum) — extensible for future coins
- P&L computation uses weighted average cost from buy transactions only (sells reduce position but don't change avg cost basis)
- Fixed deposit response computes accrued/projected values on every request (no caching — computation is cheap)
- All price fetches wrapped in try/catch — individual asset price failures don't break the portfolio summary
- Exchange rate failure defaults to 0 (VND values show as 0 when rate unavailable)
