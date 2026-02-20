---
phase: 32-tech-debt-cleanup
plan: 03
subsystem: testing
tags: [integration-tests, webapplicationfactory, testcontainers, postgres, nsubstitute, portfolio, fixed-deposits]

# Dependency graph
requires:
  - phase: 32-01
    provides: price feed interfaces (ICryptoPriceProvider, IEtfPriceProvider, IExchangeRateProvider) and PriceFeedResult type
  - phase: 32-02
    provides: CustomWebApplicationFactory, FixedDepositEndpointsTests, and PortfolioEndpointsTests scaffolding committed as part of plan 02
provides:
  - Integration tests for portfolio summary GET and asset/transaction POST endpoints
  - Integration tests for fixed deposit CRUD endpoints (GET/POST/PUT/DELETE)
  - CustomWebApplicationFactory with Testcontainers PostgreSQL and mocked external services
affects: [phase-33-future, any future endpoint addition that needs integration test coverage]

# Tech tracking
tech-stack:
  added:
    - Microsoft.AspNetCore.Mvc.Testing 10.0.0 (WebApplicationFactory for integration tests)
  patterns:
    - ICollectionFixture<CustomWebApplicationFactory> for shared test container across test classes
    - ConfigureWebHost override pattern to replace Aspire-specific registrations with test doubles
    - IAsyncLifetime.DisposeAsync for per-class database cleanup to prevent cross-test interference
    - NSubstitute mocks exposed as factory properties for per-test behavior configuration

key-files:
  created:
    - tests/TradingBot.ApiService.Tests/Endpoints/CustomWebApplicationFactory.cs
    - tests/TradingBot.ApiService.Tests/Endpoints/PortfolioEndpointsTests.cs
    - tests/TradingBot.ApiService.Tests/Endpoints/FixedDepositEndpointsTests.cs
  modified:
    - tests/TradingBot.ApiService.Tests/TradingBot.ApiService.Tests.csproj

key-decisions:
  - "Files were committed as part of 32-02 (feat(32-02) commit d9db38f); plan 03 verified they meet all 32-03 success criteria"
  - "ICollectionFixture used to share single Testcontainers PostgreSQL instance across both test classes (started once per test run)"
  - "RemoveAll<IHostedService>() removes all background workers to prevent external API calls during tests"
  - "In-memory distributed cache replaces Redis in test environment (no Redis sidecar required)"
  - "IMessageBroker mock replaces Dapr registration (no Dapr sidecar required for endpoint tests)"
  - "Per-class DisposeAsync truncates tables to ensure test isolation without unique-ticker workarounds"

patterns-established:
  - "Aspire-specific WebApp factory override: UseSetting for connection strings + ConfigureServices for DI replacement"
  - "Price provider mocks configured in constructor with Returns() for predictable test behavior"

requirements-completed: []

# Metrics
duration: 6min
completed: 2026-02-21
---

# Phase 32 Plan 03: Integration Tests for Portfolio and Fixed Deposit Endpoints Summary

**WebApplicationFactory with Testcontainers PostgreSQL enabling 13 integration tests for portfolio summary, asset/transaction, and fixed deposit CRUD endpoints — all 103 tests pass**

## Performance

- **Duration:** ~6 min
- **Started:** 2026-02-21T11:18:02Z
- **Completed:** 2026-02-21T11:23:30Z
- **Tasks:** 1
- **Files modified:** 4

## Accomplishments

- CustomWebApplicationFactory subclasses WebApplicationFactory<Program> with Testcontainers PostgreSQL, in-memory cache, NSubstitute mocks for all three price providers, mocked IMessageBroker, and all background services removed
- 6 portfolio endpoint integration tests covering: empty summary (GET), create asset (POST), create transaction (POST), summary with data (GET), future-date rejection, duplicate ticker conflict
- 7 fixed deposit endpoint integration tests covering full CRUD: empty list (GET), create (POST), get by ID (GET), update (PUT), delete with 404 verify (DELETE), invalid compounding frequency (POST)
- All 103 tests pass — 0 regressions in existing test suite

## Task Commits

Work was already committed as part of 32-02 execution. These commits contain the 32-03 artifacts:

1. **Task 1: Create CustomWebApplicationFactory and endpoint integration tests** — `d9db38f` (feat(32-02): add dynamic CoinGecko ID lookup for crypto tickers)

The plan 32-03 files are all present in `d9db38f`.

## Files Created/Modified

- `tests/TradingBot.ApiService.Tests/Endpoints/CustomWebApplicationFactory.cs` — WebApplicationFactory subclass with Testcontainers PostgreSQL, mocked price providers, and API key config (109 lines)
- `tests/TradingBot.ApiService.Tests/Endpoints/PortfolioEndpointsTests.cs` — 6 integration tests for portfolio summary GET and asset/transaction POST (198 lines)
- `tests/TradingBot.ApiService.Tests/Endpoints/FixedDepositEndpointsTests.cs` — 7 integration tests for fixed deposit CRUD (180 lines)
- `tests/TradingBot.ApiService.Tests/TradingBot.ApiService.Tests.csproj` — Added Microsoft.AspNetCore.Mvc.Testing 10.0.0

## Decisions Made

- Files were pre-committed during 32-02 execution as that plan introduced CoinGecko `SearchCoinIdAsync` which modified `PortfolioEndpoints.cs` and the endpoint test files simultaneously
- `ICollectionFixture<CustomWebApplicationFactory>` pattern shares the Testcontainers PostgreSQL instance so Docker container starts once and is reused across all endpoint test classes
- `RemoveAll<IHostedService>()` safely removes all background workers; individual service removal would be fragile as the list grows
- In-memory distributed cache (`AddDistributedMemoryCache`) replaces Aspire Redis registration — the exchange rate fallback path reads from cache but tests don't exercise that path
- `ConfigureWebHost` override uses `UseSetting` to satisfy Aspire connection string resolution before `ConfigureServices` replaces the actual registrations

## Deviations from Plan

None - plan executed exactly as written (files were pre-committed via 32-02 overlap).

## Issues Encountered

None - all tests passed on first run (after fixing one assertion that expected lowercase ticker when API normalizes to uppercase).

## User Setup Required

None - no external service configuration required. Tests run entirely with Testcontainers (auto-downloads PostgreSQL Docker image if not cached).

## Next Phase Readiness

- Phase 32 success criterion #2 ("Integration tests exist for portfolio summary, transaction, and fixed deposit endpoints") is now closed
- 103 total test cases establish high confidence before any future refactoring
- Endpoint test pattern is documented and repeatable for new endpoints

---
*Phase: 32-tech-debt-cleanup*
*Completed: 2026-02-21*
