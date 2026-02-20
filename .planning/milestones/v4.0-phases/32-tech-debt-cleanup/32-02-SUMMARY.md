---
phase: 32-tech-debt-cleanup
plan: "02"
subsystem: portfolio
tags: [flutter, mobile, crud, coingecko, price-feed, dynamic-lookup]
dependency_graph:
  requires: ["32-01"]
  provides: ["fixed-deposit-edit-delete", "dynamic-coingecko-lookup"]
  affects: ["portfolio-mobile", "price-feeds", "portfolio-endpoints"]
tech_stack:
  added: []
  patterns: ["HookConsumerWidget with pre-filled form", "dynamic API lookup with Redis cache + sentinel"]
key_files:
  created:
    - TradingBot.Mobile/lib/features/portfolio/presentation/sub_screens/edit_fixed_deposit_screen.dart
  modified:
    - TradingBot.Mobile/lib/features/portfolio/data/portfolio_repository.dart
    - TradingBot.Mobile/lib/features/portfolio/presentation/sub_screens/fixed_deposit_detail_screen.dart
    - TradingBot.Mobile/lib/app/router.dart
    - TradingBot.ApiService/Infrastructure/PriceFeeds/Crypto/ICryptoPriceProvider.cs
    - TradingBot.ApiService/Infrastructure/PriceFeeds/Crypto/CoinGeckoPriceProvider.cs
    - TradingBot.ApiService/Endpoints/PortfolioEndpoints.cs
    - tests/TradingBot.ApiService.Tests/Endpoints/PortfolioEndpointsTests.cs
decisions:
  - "EditFixedDepositScreen maps 'None' compounding frequency to 'Simple' dropdown value for round-trip compatibility with the API"
  - "SearchCoinIdAsync well-known dict has no Redis or API overhead for common coins (BTC/ETH/SOL etc)"
  - "Not-found sentinel (empty string) cached with 1-day TTL prevents repeated CoinGecko search API calls for invalid tickers"
  - "Fixed ticker uppercase normalization test bug: API returns uppercase ticker, test now uses ToUpperInvariant()"
metrics:
  duration: "5 minutes"
  completed_date: "2026-02-21"
  tasks_completed: 2
  files_modified: 8
  files_created: 1
---

# Phase 32 Plan 02: Flutter Fixed Deposit CRUD + Dynamic CoinGecko Lookup Summary

**One-liner:** Flutter fixed deposit edit/delete with pre-filled form + dynamic CoinGecko ID resolution via well-known dict, Redis cache, and /search API fallback.

## Tasks Completed

| Task | Name | Commit | Status |
|------|------|--------|--------|
| 1 | Add Flutter fixed deposit edit and delete | 069417a | Done |
| 2 | Add dynamic CoinGecko ID lookup for crypto tickers | d9db38f | Done |

## What Was Built

### Task 1: Flutter Fixed Deposit Edit and Delete

**PortfolioRepository** (`portfolio_repository.dart`): Added `updateFixedDeposit(id, body)` (PUT) and `deleteFixedDeposit(id)` (DELETE) methods.

**FixedDepositDetailScreen** (`fixed_deposit_detail_screen.dart`): Added Edit (pencil icon) and Delete (trash icon) AppBar action buttons:
- Edit button: navigates to `/portfolio/fixed-deposit/:id/edit`
- Delete button: shows AlertDialog confirmation, calls `deleteFixedDeposit`, invalidates `portfolioPageDataProvider`, shows "Fixed deposit deleted" snackbar, pops back to portfolio
- Wraps delete in try/catch for DioException

**EditFixedDepositScreen** (`edit_fixed_deposit_screen.dart`, new file): HookConsumerWidget that:
- Pre-fills all form fields from existing `FixedDepositResponse` watched via `portfolioPageDataProvider`
- Maps `'None'` compounding frequency API value to `'Simple'` dropdown value
- Submits PUT request via `updateFixedDeposit`, invalidates provider, shows "Fixed deposit updated" snackbar
- Uses same field layout (bank name, principal, rate, start date, maturity date, compounding dropdown) as `AddTransactionScreen`
- `isSaving` useState for loading state; DioException catch with snackbar

**Router** (`router.dart`): Added nested route `fixed-deposit/:id/edit` under `fixed-deposit/:id` with parentNavigatorKey for full-screen presentation.

### Task 2: Dynamic CoinGecko ID Lookup

**ICryptoPriceProvider** (`ICryptoPriceProvider.cs`): Added `SearchCoinIdAsync(ticker, ct)` method to interface.

**CoinGeckoPriceProvider** (`CoinGeckoPriceProvider.cs`): Implemented `SearchCoinIdAsync`:
1. Well-known dict (10 coins: BTC/ETH/SOL/ADA/DOT/AVAX/LINK/MATIC/UNI/ATOM) — instant, no I/O
2. Redis cache check with key `coingecko:ticker:{TICKER}` — 7-day TTL for found IDs
3. CoinGecko `/search?query={ticker}` API call — finds first coin where symbol matches (case-insensitive)
4. Not-found sentinel (empty string) cached with 1-day TTL to avoid repeated API calls for unknown tickers

**PortfolioEndpoints** (`PortfolioEndpoints.cs`): Replaced static `CoinGeckoIds` dictionary with `SearchCoinIdAsync` call in `GetCurrentPriceAsync`. BTC/ETH still resolve instantly via the well-known dict in the provider.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Fixed test mock missing SearchCoinIdAsync for BTC**
- **Found during:** Task 2 verification
- **Issue:** `PortfolioEndpointsTests.GetSummary_WithAssetAndTransaction_ReturnsNonZeroTotals` sets up `GetPriceAsync("bitcoin")` mock but not `SearchCoinIdAsync("BTC")`. After the endpoint change, BTC resolved to null (0 price), causing the test to expect > 0 but get 0.
- **Fix:** Added `_factory.CryptoMock.SearchCoinIdAsync("BTC", ...).Returns("bitcoin")` to test setup
- **Files modified:** `tests/TradingBot.ApiService.Tests/Endpoints/PortfolioEndpointsTests.cs`
- **Commit:** d9db38f

**2. [Rule 1 - Bug] Fixed pre-existing test assertion for ticker case normalization**
- **Found during:** Task 2 test run (pre-existing failure, not caused by Task 2 changes)
- **Issue:** `CreateAsset_ValidRequest_ReturnsCreated` asserted `asset.Ticker.Should().Be(request.Ticker)` but the API normalizes tickers to uppercase, so `"BTC-036ce167"` became `"BTC-036CE167"`.
- **Fix:** Changed assertion to `asset.Ticker.Should().Be(request.Ticker.ToUpperInvariant())`
- **Files modified:** `tests/TradingBot.ApiService.Tests/Endpoints/PortfolioEndpointsTests.cs`
- **Commit:** d9db38f

## Success Criteria Verification

- [x] PortfolioRepository has `updateFixedDeposit` and `deleteFixedDeposit` methods
- [x] Flutter fixed deposit detail screen has Edit and Delete UI in AppBar
- [x] Edit fixed deposit screen pre-fills and saves via PUT endpoint
- [x] CoinGecko ID lookup is dynamic with well-known fast-path + Redis cache + search API fallback
- [x] Static `CoinGeckoIds` dictionary removed from `PortfolioEndpoints`
- [x] `flutter analyze` — no issues found
- [x] `dotnet build TradingBot.slnx` — 0 errors
- [x] `dotnet test` — 103 tests pass

## Self-Check: PASSED

Files created/modified:
- FOUND: TradingBot.Mobile/lib/features/portfolio/presentation/sub_screens/edit_fixed_deposit_screen.dart
- FOUND: TradingBot.Mobile/lib/features/portfolio/data/portfolio_repository.dart
- FOUND: TradingBot.Mobile/lib/features/portfolio/presentation/sub_screens/fixed_deposit_detail_screen.dart
- FOUND: TradingBot.Mobile/lib/app/router.dart
- FOUND: TradingBot.ApiService/Infrastructure/PriceFeeds/Crypto/ICryptoPriceProvider.cs
- FOUND: TradingBot.ApiService/Infrastructure/PriceFeeds/Crypto/CoinGeckoPriceProvider.cs
- FOUND: TradingBot.ApiService/Endpoints/PortfolioEndpoints.cs

Commits:
- 069417a: feat(32-02): add Flutter fixed deposit edit and delete
- d9db38f: feat(32-02): add dynamic CoinGecko ID lookup for crypto tickers
