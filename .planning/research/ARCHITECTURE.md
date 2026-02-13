# Architecture Patterns: Web Dashboard

**Domain:** Trading bot web dashboard
**Researched:** 2026-02-13

## Recommended Architecture

**Pattern:** Frontend-consuming-backend-APIs with strict read-only separation for MVP

```
┌────────────────────────────────────────────────────────────┐
│                    USER BROWSER                            │
│  ┌──────────────────────────────────────────────────────┐ │
│  │              Nuxt 3 App (SSR + SPA)                  │ │
│  │  ┌────────────┐  ┌────────────┐  ┌────────────┐    │ │
│  │  │ Portfolio  │  │ Purchases  │  │ Backtest   │    │ │
│  │  │   Page     │  │    Page    │  │   Page     │    │ │
│  │  └─────┬──────┘  └─────┬──────┘  └─────┬──────┘    │ │
│  │        │                │                │           │ │
│  │        └────────────────┴────────────────┘           │ │
│  │                         │                            │ │
│  │                         ▼                            │ │
│  │              ┌──────────────────┐                    │ │
│  │              │   Composables    │                    │ │
│  │              │ - usePortfolio() │                    │ │
│  │              │ - usePurchases() │                    │ │
│  │              │ - useBtcPrice()  │                    │ │
│  │              │ - useBacktest()  │                    │ │
│  │              └──────────────────┘                    │ │
│  │                         │                            │ │
│  │                         ▼                            │ │
│  │              ┌──────────────────┐                    │ │
│  │              │   useFetch()     │                    │ │
│  │              │   (Nuxt built-in)│                    │ │
│  │              └──────────────────┘                    │ │
│  └──────────────────────┬───────────────────────────────┘ │
└─────────────────────────┼─────────────────────────────────┘
                          │ HTTP (API Key in header)
                          ▼
┌────────────────────────────────────────────────────────────┐
│              .NET 10 API Service                           │
│  ┌──────────────────────────────────────────────────────┐ │
│  │          API Key Middleware (Phase 1)                │ │
│  │          CORS Middleware                             │ │
│  └──────────────────────────────────────────────────────┘ │
│  ┌──────────────────────────────────────────────────────┐ │
│  │           Dashboard Endpoints (NEW)                  │ │
│  │  GET  /api/portfolio      → Aggregate Purchase data │ │
│  │  GET  /api/price          → Hyperliquid spot price  │ │
│  │  GET  /api/purchases      → Paginated purchase list │ │
│  │  GET  /api/config         → DcaOptions (read-only)  │ │
│  │  GET  /api/status         → Bot health + next buy   │ │
│  └──────────────────────────────────────────────────────┘ │
│  ┌──────────────────────────────────────────────────────┐ │
│  │           Existing Endpoints                         │ │
│  │  POST /api/backtest       → Single backtest run     │ │
│  │  POST /api/backtest/sweep → Parameter sweep         │ │
│  │  GET  /api/backtest/data/status → Data freshness    │ │
│  │  GET  /health             → Health check            │ │
│  └──────────────────────────────────────────────────────┘ │
│  ┌──────────────────────────────────────────────────────┐ │
│  │           Data Access (EF Core)                      │ │
│  │  - Purchase (existing)                               │ │
│  │  - DailyPrice (existing)                             │ │
│  │  - IOptionsMonitor<DcaOptions> (existing)            │ │
│  └──────────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────┘
                          │
                          ▼
┌────────────────────────────────────────────────────────────┐
│                  PostgreSQL Database                       │
│  - Purchase table (Id, ExecutedAt, Price, Quantity, etc)  │
│  - DailyPrice table (Symbol, Date, Open, High, Low, etc)  │
└────────────────────────────────────────────────────────────┘
```

### Component Boundaries

| Component | Responsibility | Communicates With |
|-----------|---------------|-------------------|
| **Nuxt Pages** | Route handling, layout, SEO | Composables |
| **Composables** | Data fetching logic, state management | useFetch → API |
| **useFetch** | HTTP client with SSR, caching, reactivity | Dashboard Endpoints |
| **Dashboard Endpoints** | New API routes for dashboard-specific data | Purchase/DailyPrice tables, HyperliquidClient |
| **Existing Endpoints** | Backtest, health, data status (no changes) | BacktestSimulator, IngestionJob |
| **Purchase Table** | Source of truth for portfolio and history | EF Core queries |

### Data Flow

**Portfolio Overview Flow:**
```
User loads / page
  → Nuxt SSR renders page
  → usePortfolio() composable
  → useFetch('/api/portfolio')
  → Dashboard GET /api/portfolio endpoint
  → EF Core: SELECT SUM(Cost), SUM(Quantity) FROM Purchase WHERE Status = 'Filled'
  → Hyperliquid: GET current BTC price
  → Calculate: totalBtc, totalCost, currentValue, unrealizedPnL
  → Return JSON
  → useFetch caches response (60s TTL)
  → Vue renders <PortfolioCard> component
```

**Purchase History Flow:**
```
User loads /purchases page
  → usePurchases(page = 1, pageSize = 50)
  → useFetch('/api/purchases?page=1&pageSize=50')
  → Dashboard GET /api/purchases endpoint
  → EF Core: SELECT * FROM Purchase ORDER BY ExecutedAt DESC LIMIT 50 OFFSET 0
  → Return { items: [...], total: 1234, page: 1, pageSize: 50 }
  → Vue renders <PurchaseTable> with pagination controls
```

**Live BTC Price Flow (polling):**
```
User on any page
  → useBtcPrice() composable with setInterval(5000)
  → useFetch('/api/price', { server: false })  // client-only
  → Dashboard GET /api/price endpoint
  → HyperliquidClient.GetSpotPriceAsync("BTC")
  → Return { symbol: "BTC", price: 45123.45, timestamp: "..." }
  → Reactive ref updates → UI updates
```

**Backtest Visualization Flow:**
```
User submits backtest form
  → useBacktest() composable
  → useFetch('/api/backtest', { method: 'POST', body: config })
  → Existing BacktestEndpoints.RunBacktestAsync()
  → Return BacktestResult (smart DCA, fixed DCA, comparison, purchase log)
  → <BacktestChart> component renders equity curve using Unovis
  → Line series: Smart DCA portfolio value over time
  → Line series: Fixed DCA portfolio value over time
  → Markers: Purchase events
```

## Patterns to Follow

### Pattern 1: Composable-First Data Fetching
**What:** Encapsulate all API calls in composables, not directly in components
**When:** Every data fetch from backend
**Example:**
```typescript
// composables/usePortfolio.ts
export const usePortfolio = () => {
  const config = useRuntimeConfig()

  const { data, pending, error, refresh } = useFetch(
    `${config.public.apiBase}/api/portfolio`,
    {
      headers: { 'X-API-Key': config.public.apiKey },
      lazy: true,
      server: true,  // SSR on initial load
      getCachedData(key) {
        const cached = useNuxtData(key)
        // Cache for 60 seconds
        if (cached.data.value && Date.now() - cached.fetchedAt < 60000) {
          return cached.data.value
        }
      }
    }
  )

  return { portfolio: data, loading: pending, error, refresh }
}
```

### Pattern 2: API Endpoint Design (Backend)
**What:** Lean, focused endpoints that return exactly what UI needs
**When:** Adding new dashboard endpoints
**Example:**
```csharp
// TradingBot.ApiService/Endpoints/DashboardEndpoints.cs
public static class DashboardEndpoints
{
    public static WebApplication MapDashboardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api")
            .RequireAuthorization("ApiKeyPolicy");  // Auth middleware

        group.MapGet("/portfolio", GetPortfolioAsync);
        group.MapGet("/price", GetPriceAsync);
        group.MapGet("/purchases", GetPurchasesAsync);
        group.MapGet("/config", GetConfigAsync);
        group.MapGet("/status", GetStatusAsync);

        return app;
    }

    private static async Task<IResult> GetPortfolioAsync(
        TradingBotDbContext db,
        IHyperliquidClient hyperliquidClient,
        CancellationToken ct)
    {
        // Aggregate purchase data
        var summary = await db.Purchases
            .Where(p => p.Status == PurchaseStatus.Filled)
            .GroupBy(p => 1)
            .Select(g => new {
                TotalBtc = g.Sum(p => p.Quantity),
                TotalCost = g.Sum(p => p.Cost),
                PurchaseCount = g.Count(),
                FirstPurchase = g.Min(p => p.ExecutedAt),
                LastPurchase = g.Max(p => p.ExecutedAt)
            })
            .FirstOrDefaultAsync(ct);

        if (summary == null)
        {
            return Results.Ok(new PortfolioResponse
            {
                TotalBtc = 0,
                TotalCost = 0,
                CurrentValue = 0,
                UnrealizedPnL = 0,
                UnrealizedPnLPercent = 0,
                AverageCostBasis = 0,
                PurchaseCount = 0
            });
        }

        // Get current BTC price
        var currentPrice = await hyperliquidClient.GetSpotPriceAsync("BTC", ct);
        var currentValue = summary.TotalBtc * currentPrice;
        var unrealizedPnL = currentValue - summary.TotalCost;
        var avgCostBasis = summary.TotalCost / summary.TotalBtc;

        return Results.Ok(new PortfolioResponse
        {
            TotalBtc = summary.TotalBtc,
            TotalCost = summary.TotalCost,
            CurrentValue = currentValue,
            UnrealizedPnL = unrealizedPnL,
            UnrealizedPnLPercent = (unrealizedPnL / summary.TotalCost) * 100,
            AverageCostBasis = avgCostBasis,
            PurchaseCount = summary.PurchaseCount,
            FirstPurchase = summary.FirstPurchase,
            LastPurchase = summary.LastPurchase,
            CurrentPrice = currentPrice,
            AsOf = DateTimeOffset.UtcNow
        });
    }
}
```

### Pattern 3: Pagination for Large Lists
**What:** Server-side pagination with cursor or offset
**When:** Purchase history (potentially 1000+ records)
**Example:**
```csharp
private static async Task<IResult> GetPurchasesAsync(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 50,
    TradingBotDbContext db,
    CancellationToken ct)
{
    pageSize = Math.Min(pageSize, 100);  // Cap at 100
    page = Math.Max(page, 1);  // Min page 1

    var query = db.Purchases
        .Where(p => !p.IsDryRun)
        .OrderByDescending(p => p.ExecutedAt);

    var total = await query.CountAsync(ct);
    var items = await query
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .Select(p => new PurchaseDto
        {
            Id = p.Id,
            ExecutedAt = p.ExecutedAt,
            Price = p.Price,
            Quantity = p.Quantity,
            Cost = p.Cost,
            Multiplier = p.Multiplier,
            MultiplierTier = p.MultiplierTier,
            Status = p.Status.ToString()
        })
        .ToListAsync(ct);

    return Results.Ok(new PaginatedResponse<PurchaseDto>
    {
        Items = items,
        Total = total,
        Page = page,
        PageSize = pageSize,
        TotalPages = (int)Math.Ceiling(total / (double)pageSize)
    });
}
```

## Anti-Patterns to Avoid

### Anti-Pattern 1: Fetching in Components
**What:** Calling API directly in component `<script setup>`
**Why bad:** No SSR, no caching, duplicated logic, harder to test
**Instead:** Use composables with useFetch

### Anti-Pattern 2: N+1 Queries
**What:** Loading portfolio summary, then individual purchases in separate queries
**Why bad:** 100+ purchases = 100+ DB roundtrips
**Instead:** Use aggregation queries or pagination

### Anti-Pattern 3: Polling Too Aggressively
**What:** Fetching live price every 100ms
**Why bad:** Hammers API, no user-visible benefit (price doesn't change that fast)
**Instead:** 5-10 second polling interval, or SSE for push

### Anti-Pattern 4: Storing API Keys in Frontend Code
**What:** Hardcoding API key in Nuxt source or committing `.env` to git
**Why bad:** Exposed in browser, committed to repo history
**Instead:** `.env.local` (gitignored), server-side proxy, or HTTP-only cookie

### Anti-Pattern 5: Building Config Editing Before Proving Dashboard Value
**What:** Phase 1 includes PUT /api/config endpoint + hot-reload logic
**Why bad:** Complex (validation, restart handling), deferred value (users edit appsettings.json today)
**Instead:** Read-only config view in Phase 1, defer editing to Phase 4 after dashboard proven useful

## Scalability Considerations

| Concern | At 100 purchases | At 1,000 purchases | At 10,000 purchases |
|---------|------------------|--------------------|---------------------|
| **Portfolio aggregation** | <10ms query | <50ms query | <200ms query (add index on Status, ExecutedAt) |
| **Purchase history pagination** | No pagination needed | Page size 50, ~10ms/page | Page size 100, <50ms/page with index |
| **Chart rendering (equity curve)** | All points, <100ms | All points, <500ms | Downsample to 1000 points, <1s |
| **Backtest result size** | ~10KB JSON | ~100KB JSON | ~1MB JSON (omit purchase log for sweeps) |

**Indexes needed (add if performance degrades):**
```sql
CREATE INDEX idx_purchase_status_executed ON Purchase(Status, ExecutedAt DESC);
CREATE INDEX idx_purchase_dryrun_executed ON Purchase(IsDryRun, ExecutedAt DESC);
```

## Security Considerations

**Phase 1 Auth (API Key Middleware):**
```csharp
// TradingBot.ApiService/Middleware/ApiKeyMiddleware.cs
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _apiKey;

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next = next;
        _apiKey = config["Dashboard:ApiKey"]
            ?? throw new InvalidOperationException("Dashboard:ApiKey not configured");
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip auth for health endpoint
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-API-Key", out var extractedApiKey))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsync("API Key missing");
            return;
        }

        if (!string.Equals(_apiKey, extractedApiKey, StringComparison.Ordinal))
        {
            context.Response.StatusCode = 403;
            await context.Response.WriteAsync("Invalid API Key");
            return;
        }

        await _next(context);
    }
}

// Program.cs
app.UseMiddleware<ApiKeyMiddleware>();
```

**CORS Configuration:**
```csharp
// Program.cs
builder.Services.AddCors(options =>
{
    options.AddPolicy("DashboardPolicy", policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Dashboard:AllowedOrigins")
            .Get<string[]>() ?? ["http://localhost:3000"];

        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

app.UseCors("DashboardPolicy");
```

## Sources

- [Crypto Bot UI/UX Design: Best Practices](https://www.companionlink.com/blog/2025/01/crypto-bot-ui-ux-design-best-practices/)
- [How to Build Your Custom Analytics Dashboards - Platform Trading Bot Mevx](https://blog.mevx.io/guide/how-to-build-your-custom-analytics-dashboards)
- [Best Crypto Trading Bot Platforms for 2026](https://medium.com/coinmonks/5-best-crypto-trading-bot-platforms-for-2026-top-automated-trading-tools-b5cf60ffd433)
