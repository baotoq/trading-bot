# Architecture Research

**Domain:** Multi-asset portfolio tracker integrated into existing BTC DCA bot
**Researched:** 2026-02-20
**Confidence:** HIGH (existing codebase patterns concrete; external API confidence noted per source)

## Standard Architecture

### System Overview

```
┌────────────────────────────────────────────────────────────────────┐
│                    Flutter Mobile App (iOS)                         │
│  ┌──────────┐  ┌──────────────┐  ┌──────────┐  ┌──────────┐       │
│  │  Home    │  │  Portfolio   │  │ History  │  │  Config  │       │
│  │ (DCA)    │  │  (NEW)       │  │ (unified)│  │          │       │
│  └────┬─────┘  └──────┬───────┘  └────┬─────┘  └────┬─────┘       │
│       │               │               │              │              │
│  ┌────┴───────────────┴───────────────┴──────────────┴──────┐       │
│  │     Riverpod State Layer (currency_provider global toggle) │       │
│  └────────────────────────────────┬───────────────────────────┘       │
│                                   │ HTTP (x-api-key)                  │
└───────────────────────────────────┼──────────────────────────────────┘
                                    │
┌───────────────────────────────────┼──────────────────────────────────┐
│                    .NET 10.0 API Service                              │
│  ┌──────────────────────────────────────────────────────────────┐    │
│  │  Endpoints                                                    │    │
│  │  /api/dashboard/**  (existing)                               │    │
│  │  /api/portfolio/**  (NEW)                                    │    │
│  └──────────────────────────────────────────────────────────────┘    │
│  ┌──────────────────────┐  ┌────────────────────────────────────┐    │
│  │  DCA Domain          │  │  Portfolio Domain (NEW)             │    │
│  │  Purchase (AR)       │  │  PortfolioAsset (AR)               │    │
│  │  DcaConfiguration(AR)│  │    AssetTransaction (child)        │    │
│  │                      │  │  FixedDeposit (AR)                 │    │
│  └──────────────────────┘  └────────────────────────────────────┘    │
│  ┌──────────────────────────────────────────────────────────────┐    │
│  │  Application/Services/Portfolio                              │    │
│  │  PriceProviderFactory                                        │    │
│  │    -> ICryptoPriceProvider  (CoinGecko)                      │    │
│  │    -> IVnStockPriceProvider (VCI public endpoint)            │    │
│  │    -> IExchangeRateProvider (ExchangeRate-API)               │    │
│  │  PortfolioCalculationService                                 │    │
│  │  PriceCacheService (Redis TTL wrapper)                       │    │
│  └──────────────────────────────────────────────────────────────┘    │
│  ┌──────────────────────────────────────────────────────────────┐    │
│  │  Infrastructure/Prices (NEW)                                 │    │
│  │  CoinGeckoPriceProvider   VnStockClient   ExchangeRateClient │    │
│  └──────────────────────────────────────────────────────────────┘    │
│  ┌───────────────────────────┐  ┌──────────────────────────────┐    │
│  │  PostgreSQL               │  │  Redis                        │    │
│  │  (existing + 3 new tables)│  │  (price cache, existing)      │    │
│  └───────────────────────────┘  └──────────────────────────────┘    │
└──────────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility | Typical Implementation |
|-----------|----------------|------------------------|
| `PortfolioAsset` | Aggregate root; owns asset transactions, maintains computed cost basis | New `AggregateRoot<PortfolioAssetId>` |
| `AssetTransaction` | Single buy/sell event inside one asset | Child entity; no independent lifetime |
| `FixedDeposit` | Term deposit with principal, rate, maturity; value computed on read | New `AggregateRoot<FixedDepositId>`; no transactions |
| `PriceProviderFactory` | Selects correct price fetcher by `AssetType`; calls providers in parallel | Application service; no DB access |
| `PriceCacheService` | Redis-backed TTL wrapper; 5-min for prices, 60-min for exchange rate | Wraps `IDistributedCache` |
| `PortfolioCalculationService` | Computes P&L, allocation %, total value in USD + VND | Pure service; depends on price factory |
| `PurchaseCompletedPortfolioHandler` | Auto-imports DCA fills as `AssetTransaction` on BTC asset | New MediatR `INotificationHandler` |
| `PortfolioEndpoints` | REST API for Flutter portfolio screens | New minimal-API group `/api/portfolio/**` |
| `currency_provider.dart` | Global VND/USD toggle visible across all Flutter screens | Riverpod `StateProvider<Currency>` |

## Recommended Project Structure

```
TradingBot.ApiService/
├── Models/
│   ├── PortfolioAsset.cs           # New aggregate root
│   ├── AssetTransaction.cs         # New child entity (owned by PortfolioAsset)
│   ├── FixedDeposit.cs             # New aggregate root
│   ├── AssetType.cs                # Enum: Crypto, VnEtf, FixedDeposit
│   ├── TransactionType.cs          # Enum: Buy, Sell, Deposit, Withdrawal
│   └── Ids/
│       ├── PortfolioAssetId.cs     # Vogen [ValueObject] typed ID
│       ├── AssetTransactionId.cs   # Vogen [ValueObject] typed ID
│       └── FixedDepositId.cs       # Vogen [ValueObject] typed ID
├── Application/
│   ├── Services/
│   │   ├── Portfolio/
│   │   │   ├── PortfolioCalculationService.cs
│   │   │   ├── PriceProviderFactory.cs
│   │   │   └── Models/
│   │   │       ├── AssetSummaryDto.cs
│   │   │       └── PortfolioSummaryDto.cs
│   │   └── Prices/
│   │       ├── ICryptoPriceProvider.cs
│   │       ├── IVnStockPriceProvider.cs
│   │       ├── IExchangeRateProvider.cs
│   │       └── PriceCacheService.cs
│   ├── Handlers/
│   │   └── PurchaseCompletedPortfolioHandler.cs  # Auto-import DCA buys
│   └── Specifications/
│       └── Portfolio/
│           ├── AssetTransactionsByAssetSpec.cs
│           └── PortfolioAssetBySymbolSpec.cs
├── Infrastructure/
│   ├── Prices/
│   │   ├── CoinGeckoPriceProvider.cs  # Implements ICryptoPriceProvider
│   │   ├── VnStockClient.cs           # Implements IVnStockPriceProvider
│   │   └── ExchangeRateClient.cs      # Implements IExchangeRateProvider
│   └── Data/
│       └── TradingBotDbContext.cs     # +3 new DbSets
└── Endpoints/
    └── PortfolioEndpoints.cs

TradingBot.Mobile/lib/
├── core/
│   └── providers/
│       └── currency_provider.dart      # Global StateProvider<Currency>
└── features/
    └── portfolio/                      # New feature module (same structure as existing)
        ├── data/
        │   ├── portfolio_providers.dart
        │   ├── portfolio_repository.dart
        │   └── models/
        │       ├── portfolio_summary_response.dart
        │       ├── portfolio_asset_response.dart
        │       └── fixed_deposit_response.dart
        └── presentation/
            ├── portfolio_screen.dart
            └── widgets/
                ├── asset_card.dart
                ├── allocation_chart.dart
                └── add_transaction_sheet.dart
```

### Structure Rationale

- **`Models/PortfolioAsset` vs `Models/Purchase`:** Separate aggregates because DCA purchases are system-generated audit records, while portfolio assets are user-managed inventory. They interact only via domain event (`PurchaseCompletedPortfolioHandler`).
- **`Models/FixedDeposit` as its own aggregate:** FDs have no market price, no buy/sell transactions, and compute value via an interest formula. Folding them into `PortfolioAsset` with nullable fields is an anti-pattern (see Anti-Patterns below).
- **`Application/Services/Prices/`:** Price-fetching interfaces live in Application per the existing pattern (`IPriceDataService`). Concrete HTTP clients live in `Infrastructure/Prices/` alongside `HyperliquidClient`.
- **`features/portfolio/` in Flutter:** Exact same folder structure as `features/home/`, `features/chart/`, etc. No deviation from established pattern.
- **`core/providers/currency_provider.dart`:** Currency toggle is cross-cutting (home screen, portfolio screen both display it). It must live in `core/`, not inside a single feature.

## Architectural Patterns

### Pattern 1: PortfolioAsset Aggregate with Child Transactions

**What:** `PortfolioAsset` is the aggregate root. `AssetTransaction` is a child entity owned by that aggregate. All mutations flow through the root's behavior methods.

**When to use:** Asset holding state (quantity, average cost) must stay consistent with transaction list. Transactions are never queried independently of their asset.

**Trade-offs:** Loading the aggregate loads all transactions. For a personal tracker with lifetime <1000 transactions per asset, this is fine. For high-frequency trading at scale, this would need to be split.

**Example:**
```csharp
public class PortfolioAsset : AggregateRoot<PortfolioAssetId>
{
    protected PortfolioAsset() { }

    public Symbol Symbol { get; private set; }
    public AssetType AssetType { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public string? CoinGeckoId { get; private set; }   // null for VN stocks
    public string NativeCurrency { get; private set; } = "USD"; // "USD" or "VND"

    private readonly List<AssetTransaction> _transactions = [];
    public IReadOnlyList<AssetTransaction> Transactions => _transactions.AsReadOnly();

    // Computed fields — updated on AddTransaction
    public decimal TotalQuantity { get; private set; }
    public decimal TotalCost { get; private set; }        // In NativeCurrency
    public decimal AverageCostBasis { get; private set; } // Cost per unit

    public static PortfolioAsset Create(
        Symbol symbol, AssetType assetType, string displayName,
        string nativeCurrency, string? coinGeckoId = null)
    {
        // Validate, assign, raise PortfolioAssetCreatedEvent
    }

    public ErrorOr<Success> AddTransaction(
        TransactionType type, decimal quantity, decimal pricePerUnit,
        DateTimeOffset executedAt, string? externalReference = null, string? notes = null)
    {
        if (quantity <= 0) return Error.Validation("quantity must be positive");

        var tx = AssetTransaction.Create(
            PortfolioAssetId: Id, type, quantity, pricePerUnit, executedAt,
            externalReference, notes);
        _transactions.Add(tx);
        RecalculateHolding();
        AddDomainEvent(new AssetTransactionAddedEvent(Id, tx.Id, type, quantity, DateTimeOffset.UtcNow));
        return Result.Success;
    }

    private void RecalculateHolding()
    {
        // Weighted average cost basis
        var buys = _transactions.Where(t => t.Type == TransactionType.Buy);
        var sells = _transactions.Where(t => t.Type == TransactionType.Sell);
        TotalQuantity = buys.Sum(t => t.Quantity) - sells.Sum(t => t.Quantity);
        TotalCost = buys.Sum(t => t.Quantity * t.PricePerUnit);
        AverageCostBasis = TotalQuantity > 0 ? TotalCost / TotalQuantity : 0;
    }
}
```

### Pattern 2: FixedDeposit as Separate Aggregate

**What:** `FixedDeposit` is its own aggregate. Accrued value is a computed property (never persisted), calculated from principal, rate, and elapsed days.

**When to use:** Fixed deposits behave fundamentally differently from tradeable assets — no market price, no buy/sell transactions, deterministic value growth.

**Trade-offs:** Separate DB table and API shape. This is the correct separation. The portfolio summary service aggregates both `PortfolioAsset` and `FixedDeposit` at the read layer.

**Example:**
```csharp
public class FixedDeposit : AggregateRoot<FixedDepositId>
{
    protected FixedDeposit() { }

    public string BankName { get; private set; } = string.Empty;
    public decimal PrincipalVnd { get; private set; }
    public decimal AnnualRatePercent { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly MaturityDate { get; private set; }
    public string? Notes { get; private set; }
    public bool IsMatured => DateOnly.FromDateTime(DateTime.UtcNow) >= MaturityDate;

    // Computed on read — never stored
    public decimal AccruedValueVnd(DateOnly asOf)
    {
        var effectiveDate = asOf > MaturityDate ? MaturityDate : asOf;
        var days = (effectiveDate.ToDateTime(TimeOnly.MinValue)
                   - StartDate.ToDateTime(TimeOnly.MinValue)).Days;
        return PrincipalVnd * (1 + (AnnualRatePercent / 100m) * days / 365m);
    }
}
```

### Pattern 3: PriceProviderFactory with Parallel Fetching

**What:** A factory dispatches price lookups to the correct provider by `AssetType` and calls providers in parallel with `Task.WhenAll`.

**When to use:** Portfolio may have crypto + VN ETF assets simultaneously. Sequential fetching would add unnecessary latency (CoinGecko + VCI calls are independent).

**Trade-offs:** Partial failures (e.g., VCI unavailable) should not block crypto prices. Each provider must handle its own failure gracefully (return cached value or null).

**Example:**
```csharp
public class PriceProviderFactory(
    ICryptoPriceProvider cryptoProvider,
    IVnStockPriceProvider vnStockProvider,
    IExchangeRateProvider exchangeRateProvider)
{
    public async Task<PortfolioPriceData> GetAllPricesAsync(
        IReadOnlyList<PortfolioAsset> assets, CancellationToken ct)
    {
        var cryptoIds = assets
            .Where(a => a.AssetType == AssetType.Crypto && a.CoinGeckoId != null)
            .Select(a => a.CoinGeckoId!);
        var vnSymbols = assets
            .Where(a => a.AssetType == AssetType.VnEtf)
            .Select(a => a.Symbol.Value);

        var (cryptoPrices, vnPrices, usdVndRate) = await (
            cryptoProvider.GetPricesUsdAsync(cryptoIds, ct),
            vnStockProvider.GetPricesVndAsync(vnSymbols, ct),
            exchangeRateProvider.GetUsdToVndRateAsync(ct)
        ).WhenAll();

        return new PortfolioPriceData(cryptoPrices, vnPrices, usdVndRate);
    }
}
```

### Pattern 4: DCA Auto-Import via Event Handler

**What:** `PurchaseCompletedPortfolioHandler` is a new `INotificationHandler<PurchaseCompletedEvent>` alongside the existing notification handler. It upserts the BTC `PortfolioAsset` and adds an `AssetTransaction` with the `PurchaseId` as `ExternalReference` for idempotency.

**When to use:** The existing event already flows through Dapr pub-sub. Adding a second handler is the lowest-coupling integration — the DCA domain does not know about the portfolio domain.

**Trade-offs:** Must handle idempotency (the event could be re-delivered). The `ExternalReference` uniqueness check handles this.

**Example:**
```csharp
public class PurchaseCompletedPortfolioHandler(
    TradingBotDbContext db,
    ILogger<PurchaseCompletedPortfolioHandler> logger)
    : INotificationHandler<PurchaseCompletedEvent>
{
    public async Task Handle(PurchaseCompletedEvent ev, CancellationToken ct)
    {
        if (ev.IsDryRun) return; // Don't import dry-run buys

        var btcAsset = await db.PortfolioAssets
            .Include(a => a.Transactions)
            .FirstOrDefaultAsync(a => a.Symbol == Symbol.Btc, ct);

        if (btcAsset == null)
        {
            btcAsset = PortfolioAsset.Create(
                Symbol.Btc, AssetType.Crypto, "Bitcoin", "USD", "bitcoin");
            db.PortfolioAssets.Add(btcAsset);
        }

        // Idempotency: skip if already imported
        if (btcAsset.Transactions.Any(t => t.ExternalReference == ev.PurchaseId.ToString()))
        {
            logger.LogDebug("Purchase {PurchaseId} already imported to portfolio", ev.PurchaseId);
            return;
        }

        var result = btcAsset.AddTransaction(
            TransactionType.Buy, ev.Quantity, ev.Price,
            ev.ExecutedAt, externalReference: ev.PurchaseId.ToString());

        if (result.IsError)
        {
            logger.LogError("Failed to add transaction for purchase {PurchaseId}: {Error}",
                ev.PurchaseId, result.FirstError.Description);
            return;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Imported purchase {PurchaseId} to BTC portfolio asset", ev.PurchaseId);
    }
}
```

## Data Flow

### Portfolio Summary Request Flow

```
Flutter GET /api/portfolio/summary
    ↓
PortfolioEndpoints.GetSummaryAsync
    ↓
PortfolioCalculationService.CalculateSummaryAsync
    ├── db.PortfolioAssets.Include(a => a.Transactions).ToListAsync()
    ├── db.FixedDeposits.ToListAsync()
    └── PriceProviderFactory.GetAllPricesAsync(assets)     [Redis cache first]
            ├── CoinGeckoPriceProvider.GetPricesUsdAsync() [5-min TTL]
            ├── VnStockClient.GetPricesVndAsync()          [5-min TTL]
            └── ExchangeRateClient.GetUsdToVndRateAsync()  [60-min TTL]
    ↓
Compute per-asset: currentValue, unrealizedPnl, allocationPct
Compute totals: totalValueUsd, totalValueVnd (= totalUsd * usdVnd)
    ↓
PortfolioSummaryDto {
    totalValueUsd, totalValueVnd,
    assets: [ { symbol, displayName, quantity, costBasis, valueUsd, valueVnd, pnlPct } ],
    fixedDeposits: [ { bankName, principalVnd, accruedVnd, maturityDate } ]
}
    ↓
Flutter renders with currency_provider toggle (USD or VND)
No re-fetch on currency toggle — response always includes both
```

### DCA Auto-Import Flow

```
DcaSchedulerBackgroundService executes buy
    ↓
Purchase.RecordFill() → AddDomainEvent(PurchaseCompletedEvent)
    ↓
DomainEventOutboxInterceptor intercepts SaveChanges → writes OutboxMessage
    ↓
OutboxMessageBackgroundService → DaprMessageBroker publishes
    ↓
Dapr pub-sub delivers to subscribed HTTP endpoint → MediatR.Publish()
    ├── PurchaseCompletedHandler     (Telegram + FCM) [EXISTING — unchanged]
    └── PurchaseCompletedPortfolioHandler             [NEW — adds AssetTransaction]
```

### Historical Purchase Migration Flow (one-time)

```
EF Core migration or startup job:
    FOR each Purchase WHERE Status = Filled AND IsDryRun = false:
        Upsert BTC PortfolioAsset (if not exists)
        IF NOT EXISTS AssetTransaction WHERE ExternalReference = purchase.Id:
            INSERT AssetTransaction (Buy, quantity, price, executedAt, externalRef=purchaseId)
```

### Currency Toggle State Flow

```
currency_provider.dart  (Riverpod StateProvider<Currency>)
    ↓ (global, persisted across bottom nav tab switches)
PortfolioScreen.ref.watch(currencyProvider)
HomeScreen.ref.watch(currencyProvider)  ← if home shows total portfolio value
    ↓
Flutter selects valueUsd or valueVnd from cached API response
No API re-fetch — currency toggle is a pure display concern
```

## New vs. Modified Components

### New Components

| Component | Type | Location |
|-----------|------|----------|
| `PortfolioAsset` | Domain aggregate | `Models/PortfolioAsset.cs` |
| `AssetTransaction` | Child entity | `Models/AssetTransaction.cs` |
| `FixedDeposit` | Domain aggregate | `Models/FixedDeposit.cs` |
| `PortfolioAssetId`, `AssetTransactionId`, `FixedDepositId` | Vogen typed IDs | `Models/Ids/` |
| `AssetType`, `TransactionType` | Enums | `Models/` |
| `PortfolioCalculationService` | Application service | `Application/Services/Portfolio/` |
| `PriceProviderFactory` | Application service | `Application/Services/Portfolio/` |
| `ICryptoPriceProvider`, `IVnStockPriceProvider`, `IExchangeRateProvider` | Interfaces | `Application/Services/Prices/` |
| `CoinGeckoPriceProvider` | Infrastructure | `Infrastructure/Prices/` |
| `VnStockClient` | Infrastructure | `Infrastructure/Prices/` |
| `ExchangeRateClient` | Infrastructure | `Infrastructure/Prices/` |
| `PriceCacheService` | Application service | `Application/Services/Prices/` |
| `PurchaseCompletedPortfolioHandler` | Event handler | `Application/Handlers/` |
| `PortfolioEndpoints` | Minimal API endpoints | `Endpoints/PortfolioEndpoints.cs` |
| `portfolio/` feature module | Flutter screens + widgets | `TradingBot.Mobile/lib/features/portfolio/` |
| `currency_provider.dart` | Riverpod global state | `TradingBot.Mobile/lib/core/providers/` |

### Modified Components

| Component | Change | Why |
|-----------|--------|-----|
| `TradingBotDbContext` | Add `PortfolioAssets`, `AssetTransactions`, `FixedDeposits` DbSets + Vogen converters | Persistence for new aggregates |
| `DomainEventOutboxInterceptor` | No change — already processes any `IAggregateRoot` | New aggregates inherit `AggregateRoot<TId>` |
| `NavigationShell` (Flutter) | Add "Portfolio" tab to `NavigationBar` | New screen needs nav entry |
| `router.dart` (Flutter) | Add `/portfolio` branch to `StatefulShellRoute` | Wire portfolio screen |
| EF Core migrations | New migration for 3 portfolio tables | Schema change |
| `Program.cs` | Register new services + map `PortfolioEndpoints` | DI wiring |

## Integration Points

### External Price APIs

| Service | Asset Type | Endpoint | Rate Limit | Confidence |
|---------|-----------|----------|------------|------------|
| CoinGecko `/simple/price` | Crypto (BTC + any coin with `coinGeckoId`) | `https://api.coingecko.com/api/v3/simple/price?ids={ids}&vs_currencies=usd` | 30 calls/min (demo key) | HIGH — already in codebase |
| VCI public API | VN ETF (E1VFVN30, FUESSV30) | Undocumented endpoint; same source used by vnstock Python lib | Unknown; cache aggressively | MEDIUM — unofficial, may change |
| ExchangeRate-API free | USD/VND rate | `https://open.exchangerate-api.com/v6/latest/USD` | ~1500 req/month | MEDIUM — free, no key required |

**VN Stock API note (MEDIUM confidence):** There is no official documented REST API for VN30 ETF prices that is free. The vnstock Python library wraps the same VCI internal API endpoint the VCI trading platform uses. This is the pragmatic approach for a personal tracker — same pattern as informal Yahoo Finance access. Design `VnStockClient` with graceful degradation: on HTTP failure, return last Redis-cached price (identical to existing stale-data policy in `PriceDataService`).

**ExchangeRate-API:** The free endpoint at `https://open.exchangerate-api.com/v6/latest/USD` requires no API key. Returns all currency rates in a single response. Cache 60 minutes in Redis — VND/USD rate is stable intraday.

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| DCA domain → Portfolio domain | `PurchaseCompletedEvent` via Dapr pub-sub | Portfolio handler is additive; DCA domain has zero awareness of portfolio |
| Portfolio endpoints → Calculation service | Direct in-process call | No events needed; synchronous read path |
| Calculation service → Price providers | `Task.WhenAll` parallel async | Crypto + VN stock fetches are independent; don't serialize them |
| Price providers → Redis | `PriceCacheService` (5-min / 60-min TTL) | Consistent with existing `IDistributedCache` usage |
| Flutter currency toggle → Screens | `currencyProvider` Riverpod `StateProvider` | Global; no prop drilling; same response, display toggled in Flutter |

## Scaling Considerations

This is a single-user personal portfolio tracker. The constraints that matter are API rate limits, not user load.

| Concern | Approach |
|---------|----------|
| CoinGecko free tier rate limit | Batch all crypto assets into one `/simple/price` call; 5-min Redis TTL means at most 12 calls/hour |
| VCI endpoint rate limit (unknown) | 5-min Redis TTL is safe; VCI serves its own platform at far higher rates |
| ExchangeRate-API 1500 req/month limit | With 60-min TTL: ~720 calls/month — 48% of limit |
| Transaction history query performance | Personal use: lifetime <1000 transactions; loading with aggregate via `Include` is fine |
| Portfolio summary latency | Dominated by external API calls; Redis cache eliminates this after first call |

## Anti-Patterns

### Anti-Pattern 1: Merging FixedDeposit into PortfolioAsset

**What people do:** Add nullable `InterestRate`, `MaturityDate`, `PrincipalVnd` columns to `PortfolioAsset`; handle FD as a special `AssetType.FixedDeposit` case.

**Why it's wrong:** Fixed deposits have no market price, no buy/sell transactions, and compute value via a formula, not from a price feed. A unified model produces a table full of nullables and conditional branches throughout every service.

**Do this instead:** `FixedDeposit` as its own aggregate with its own table. `PortfolioCalculationService` queries both tables and merges at the DTO level.

### Anti-Pattern 2: Calling Price APIs on Every Request

**What people do:** Hit CoinGecko or VCI inside every `GET /api/portfolio/summary` invocation.

**Why it's wrong:** CoinGecko free tier has a 30-call/min limit. VCI's endpoint is unofficial and may throttle. Every screen load causes an external HTTP call.

**Do this instead:** `PriceCacheService` with Redis. Check cache first; on miss, fetch and store. Same stale-data policy already established in `PriceDataRefreshService`.

### Anti-Pattern 3: Persisting Computed P&L Values

**What people do:** Store `unrealizedPnlUsd`, `currentValueUsd` as DB columns, updated by a background job.

**Why it's wrong:** P&L is a function of current price, which changes continuously. Stored P&L is stale the moment it is written. The only truth is `quantity * currentPrice - totalCost`.

**Do this instead:** Compute P&L in `PortfolioCalculationService` from Redis-cached prices at request time. Only persist immutable facts: transactions, cost per unit, principal amounts.

### Anti-Pattern 4: Currency Conversion on the Backend per Request

**What people do:** Accept `?currency=VND` query param; convert values server-side; return only the requested currency.

**Why it's wrong:** Requires a round-trip when user toggles currency. Complicates server-side caching (two cache keys per endpoint).

**Do this instead:** API always returns both `valueUsd` and `valueVnd` in the same response. Flutter holds user's preference in `currencyProvider` and toggles the displayed field locally. Currency selection is a pure UI concern.

### Anti-Pattern 5: Duplicating DCA Transaction Data

**What people do:** Run a cron job that periodically re-reads `Purchases` and re-inserts them into `AssetTransactions`.

**Why it's wrong:** Two sources of truth. Historical data could diverge. The `Purchases` table is the authoritative DCA record.

**Do this instead:** `PurchaseCompletedPortfolioHandler` handles new buys going forward. A one-time EF migration imports historical purchases using `PurchaseId` as `ExternalReference`. Idempotency check prevents double-import if migration runs twice.

## Build Order Recommendation

Dependencies flow strictly downward. Build in this sequence to minimize blocked work:

1. **Domain models + EF Core migration** — `PortfolioAsset`, `AssetTransaction`, `FixedDeposit`, typed IDs, `TradingBotDbContext` extensions, and migration. No external dependencies. Everything else depends on this.

2. **Price provider infrastructure** — `CoinGeckoPriceProvider`, `VnStockClient`, `ExchangeRateClient`, `PriceCacheService`. Independently testable with mocked `IDistributedCache` and `HttpClient`. Returns `Dictionary<string, decimal>` — simple interface.

3. **Portfolio calculation service + read endpoints** — `PortfolioCalculationService`, `PriceProviderFactory`, and `GET /api/portfolio/**` endpoints. Depends on (1) and (2). Unblocks Flutter development.

4. **DCA auto-import handler + historical migration** — `PurchaseCompletedPortfolioHandler` and one-time import job. Depends only on (1). Can be built in parallel with (3) by different work sessions.

5. **Manual transaction entry endpoints** — `POST /api/portfolio/assets/{id}/transactions`, `DELETE`, etc. Depends on (1) and (3). Required for Flutter's manual entry flow.

6. **Flutter portfolio feature** — `portfolio/` feature module, `currency_provider`, nav changes. Depends on (3) and (5) being stable. Should be the last component built.

## Sources

- Existing codebase inspection: `Purchase.cs`, `DcaConfiguration.cs`, `AggregateRoot.cs`, `TradingBotDbContext.cs`, `DashboardEndpoints.cs` — HIGH confidence (direct code analysis)
- vnstock Python library: https://github.com/thinh-vu/vnstock — MEDIUM confidence (Python-only wrapper around VCI/KBS APIs; confirms data is accessible but via unofficial endpoints)
- ExchangeRate-API free tier: https://www.exchangerate-api.com/docs/free — MEDIUM confidence (no key required; ~1500 req/month free; VND supported as major currency)
- CoinGecko API: https://www.coingecko.com/en/api — HIGH confidence (already used in codebase for historical BTC data; `/simple/price` supports multi-coin in one call)
- DDD aggregate design: https://martinfowler.com/bliki/DDD_Aggregate.html, Ardalis.Specification patterns — HIGH confidence (matches existing codebase patterns exactly)
- Riverpod 3.0 (Flutter): https://riverpod.dev — HIGH confidence (`StateProvider` for global currency toggle is idiomatic Riverpod)

---
*Architecture research for: Multi-asset portfolio tracker (v4.0)*
*Researched: 2026-02-20*
