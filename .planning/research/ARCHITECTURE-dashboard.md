# Architecture Research: Nuxt + .NET Integration

**Domain:** Trading bot web dashboard integration with .NET API
**Researched:** 2026-02-13
**Confidence:** HIGH

## Standard Architecture

### System Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                    Nuxt Frontend (Port 3000)                     │
├─────────────────────────────────────────────────────────────────┤
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐         │
│  │ Pages    │  │ Composables│ │ Components│ │ Layouts  │         │
│  │ (routes) │  │ (logic)    │ │ (UI)      │ │ (shells) │         │
│  └────┬─────┘  └────┬──────┘ └────┬──────┘ └────┬─────┘         │
│       │             │               │             │               │
│       └─────────────┴───────────────┴─────────────┘               │
│                         │                                         │
│  ┌──────────────────────┴────────────────────────────┐           │
│  │        $fetch / useFetch (HTTP Client)            │           │
│  └──────────────────────┬────────────────────────────┘           │
│                         │                                         │
│  ┌──────────────────────┴────────────────────────────┐           │
│  │  EventSource (SSE for real-time price updates)    │           │
│  └───────────────────────────────────────────────────┘           │
└──────────────────────────┬──────────────────────────────────────┘
                           │ HTTP/SSE
                           │
┌──────────────────────────┴──────────────────────────────────────┐
│              .NET API Service (Port 5096)                        │
├─────────────────────────────────────────────────────────────────┤
│  ┌──────────────────────────────────────────────────────────┐   │
│  │              Minimal API Endpoints                        │   │
│  │  /api/portfolio, /api/purchases, /api/config, /api/health│   │
│  │  /api/price/stream (SSE), /api/backtest (existing)       │   │
│  └─────────────────────┬────────────────────────────────────┘   │
│                        │                                         │
│  ┌─────────────────────┴────────────────────────────────────┐   │
│  │           Application Services Layer                      │   │
│  │  (DcaExecutionService, PriceDataService, etc.)           │   │
│  └─────────────────────┬────────────────────────────────────┘   │
│                        │                                         │
│  ┌─────────────────────┴────────────────────────────────────┐   │
│  │                EF Core + TradingBotDbContext              │   │
│  └─────────────────────┬────────────────────────────────────┘   │
└────────────────────────┴─────────────────────────────────────────┘
                         │
┌────────────────────────┴─────────────────────────────────────────┐
│             Infrastructure (via Aspire AppHost)                   │
├──────────────────────────────────────────────────────────────────┤
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐         │
│  │PostgreSQL│  │  Redis   │  │   Dapr   │  │  Node.js │         │
│  │  :5432   │  │  :6379   │  │ (pubsub) │  │  (Nuxt)  │         │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘         │
└──────────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

| Component | Responsibility | Typical Implementation |
|-----------|----------------|------------------------|
| **Nuxt Pages** | Route-based UI screens (portfolio, backtest, config) | `pages/index.vue`, `pages/backtest.vue` |
| **Nuxt Composables** | Reusable data-fetching and state logic | `composables/usePortfolio.ts`, `composables/usePriceStream.ts` |
| **Nuxt Components** | UI building blocks (charts, tables, forms) | `components/PurchaseHistory.vue`, `components/BacktestChart.vue` |
| **Minimal API Endpoints** | HTTP request handlers returning JSON | `Endpoints/PortfolioEndpoints.cs`, `Endpoints/ConfigEndpoints.cs` |
| **Application Services** | Business logic (aggregations, calculations) | `PortfolioService.cs`, `ConfigUpdateService.cs` |
| **EF Core DbContext** | Database access and queries | `TradingBotDbContext` (existing) |

## Recommended Project Structure

### Nuxt Frontend Structure

```
nuxt-app/
├── pages/                    # Route-based pages (file-based routing)
│   ├── index.vue            # Dashboard home (portfolio overview)
│   ├── purchases.vue        # Purchase history timeline
│   ├── backtest.vue         # Backtest visualization
│   └── config.vue           # Configuration management
├── components/              # Reusable Vue components
│   ├── portfolio/           # Portfolio-specific components
│   │   ├── PortfolioCard.vue
│   │   └── MetricsSummary.vue
│   ├── purchases/           # Purchase history components
│   │   ├── PurchaseTimeline.vue
│   │   └── PurchaseCard.vue
│   ├── backtest/            # Backtest components
│   │   ├── BacktestChart.vue
│   │   └── ParameterForm.vue
│   └── shared/              # Shared UI components
│       ├── AppHeader.vue
│       └── StatusBadge.vue
├── composables/             # Reusable logic (state + data fetching)
│   ├── usePortfolio.ts      # Portfolio data fetching
│   ├── usePurchases.ts      # Purchase history fetching
│   ├── useBacktest.ts       # Backtest execution
│   ├── useConfig.ts         # Config CRUD operations
│   └── usePriceStream.ts    # SSE price stream
├── layouts/                 # Page layout shells
│   └── default.vue          # Default layout with header/nav
├── middleware/              # Route guards (if needed for auth later)
├── server/                  # Nuxt server routes (optional API layer)
│   └── api/                 # Server-side API routes (if needed)
├── types/                   # TypeScript types/interfaces
│   ├── portfolio.ts         # Portfolio, metrics types
│   ├── purchase.ts          # Purchase, purchase status types
│   ├── backtest.ts          # Backtest request/response types
│   └── config.ts            # DCA config types
├── nuxt.config.ts           # Nuxt configuration (proxy, CORS, etc.)
└── package.json             # Dependencies (npm/pnpm/yarn)
```

### .NET API Structure (additions to existing)

```
TradingBot.ApiService/
├── Endpoints/
│   ├── PortfolioEndpoints.cs         # NEW: Portfolio metrics API
│   ├── PurchaseEndpoints.cs          # NEW: Purchase history API
│   ├── ConfigEndpoints.cs            # NEW: Config CRUD API
│   ├── PriceStreamEndpoints.cs       # NEW: SSE price stream
│   ├── BacktestEndpoints.cs          # EXISTING (no changes)
│   └── DataEndpoints.cs              # EXISTING (no changes)
├── Application/
│   └── Services/
│       ├── PortfolioService.cs       # NEW: Portfolio aggregation logic
│       └── ConfigUpdateService.cs    # NEW: Config validation + persistence
├── Models/
│   ├── Purchase.cs                   # EXISTING (no changes)
│   ├── DailyPrice.cs                 # EXISTING (no changes)
│   └── PortfolioMetrics.cs           # NEW: Portfolio response DTO
└── Configuration/
    └── DcaOptions.cs                 # EXISTING (no changes, but read/write via API)
```

### Aspire AppHost Structure

```
TradingBot.AppHost/
├── AppHost.cs                        # MODIFIED: Add Nuxt dev server orchestration
├── TradingBot.AppHost.csproj         # MODIFIED: Add Aspire.Hosting.JavaScript
└── (rest of project unchanged)
```

### Structure Rationale

- **`pages/`:** File-based routing is Nuxt's killer feature — `pages/backtest.vue` → `/backtest` route
- **`composables/`:** Co-locate data-fetching logic with reactive state management (useFetch + ref)
- **`components/`:** Grouped by feature (portfolio, purchases, backtest) for maintainability
- **`types/`:** Shared TypeScript types mirror .NET DTOs for type safety
- **`Endpoints/` (NEW):** Keep new dashboard endpoints separate from existing backtest endpoints

## Architectural Patterns

### Pattern 1: API Proxy via Nuxt Route Rules

**What:** Configure Nuxt to proxy `/api/*` requests to .NET backend during development, avoiding CORS issues.

**When to use:** Development environment where Nuxt (`:3000`) and .NET API (`:5096`) run on different ports.

**Trade-offs:**
- **Pros:** No CORS configuration needed, simpler frontend code (relative URLs)
- **Cons:** Production requires different setup (reverse proxy or direct API calls with CORS)

**Example:**
```typescript
// nuxt.config.ts
export default defineNuxtConfig({
  routeRules: {
    '/api/**': {
      proxy: process.env.API_BASE_URL || 'http://localhost:5096/api/**'
    }
  }
})
```

**Source:** [Integrating an ASP.NET Core API with a Nuxt Front End](https://techwatching.dev/posts/aspnetcore-with-nuxt/)

### Pattern 2: Composable-Based Data Fetching

**What:** Encapsulate API calls in composables (`usePortfolio`, `usePurchases`) that return reactive refs.

**When to use:** Any data fetching that multiple components might need.

**Trade-offs:**
- **Pros:** Reusable, testable, prevents double-fetching on SSR, built-in caching
- **Cons:** Requires understanding useFetch vs $fetch distinction

**Example:**
```typescript
// composables/usePortfolio.ts
export const usePortfolio = () => {
  const { data, pending, error, refresh } = useFetch('/api/portfolio', {
    key: 'portfolio', // Cache key
    lazy: false,      // Fetch immediately
  })

  return {
    portfolio: data,
    loading: pending,
    error,
    refresh
  }
}

// Usage in pages/index.vue
const { portfolio, loading, error } = usePortfolio()
```

**Source:** [Data Fetching in Nuxt 3: useFetch, useAsyncData, and $fetch](https://www.zignuts.com/blog/nuxt3-data-fetching-guide)

### Pattern 3: Server-Sent Events (SSE) for Real-Time Price Updates

**What:** Use EventSource on frontend to subscribe to `/api/price/stream` endpoint that pushes price updates.

**When to use:** Real-time updates without bidirectional communication (server → client only).

**Trade-offs:**
- **Pros:** Simpler than SignalR, standard HTTP, automatic reconnection, less overhead than WebSockets
- **Cons:** Unidirectional only (no client → server), IE not supported (not an issue in 2026)

**Example (Frontend):**
```typescript
// composables/usePriceStream.ts
export const usePriceStream = () => {
  const currentPrice = ref<number | null>(null)
  let eventSource: EventSource | null = null

  const connect = () => {
    eventSource = new EventSource('/api/price/stream')

    eventSource.onmessage = (event) => {
      currentPrice.value = JSON.parse(event.data).price
    }

    eventSource.onerror = () => {
      console.error('SSE connection failed')
      eventSource?.close()
    }
  }

  onUnmounted(() => {
    eventSource?.close()
  })

  return { currentPrice, connect }
}
```

**Example (Backend):**
```csharp
// Endpoints/PriceStreamEndpoints.cs
public static WebApplication MapPriceStreamEndpoints(this WebApplication app)
{
    app.MapGet("/api/price/stream", async (HttpContext context, CancellationToken ct) =>
    {
        context.Response.Headers.Append("Content-Type", "text/event-stream");
        context.Response.Headers.Append("Cache-Control", "no-cache");
        context.Response.Headers.Append("Connection", "keep-alive");

        while (!ct.IsCancellationRequested)
        {
            var price = await GetCurrentPriceAsync(); // Fetch from Hyperliquid or DB
            await context.Response.WriteAsync($"data: {{\"price\": {price}}}\n\n", ct);
            await context.Response.Body.FlushAsync(ct);
            await Task.Delay(5000, ct); // Push every 5 seconds
        }
    });

    return app;
}
```

**Sources:**
- [Server-Sent Events in ASP.NET Core and .NET 10](https://www.milanjovanovic.tech/blog/server-sent-events-in-aspnetcore-and-dotnet-10)
- [You don't need SignalR for real-time updates](https://medium.com/@denmaklucky/you-dont-need-signalr-for-real-time-updates-server-sent-events-in-net-c-e032ff5d096e)
- [Server-Sent Events in Nuxt 3: A Beginner's Guide](https://medium.com/@saadamd/server-sent-events-in-nuxt-3-a-beginners-guide-to-real-time-features-c8e760207aca)

### Pattern 4: IOptionsMonitor for Dynamic Config Updates

**What:** Use `IOptionsMonitor<DcaOptions>` to detect config changes at runtime via API.

**When to use:** When dashboard updates DcaOptions via API, and DCA background service needs fresh values.

**Trade-offs:**
- **Pros:** No restart required, real-time updates, singleton-safe
- **Cons:** Changes to appsettings.json only (unless custom config provider added)

**Example:**
```csharp
// Endpoints/ConfigEndpoints.cs
public static WebApplication MapConfigEndpoints(this WebApplication app)
{
    app.MapGet("/api/config", (IOptionsMonitor<DcaOptions> dcaOptions) =>
    {
        return Results.Ok(dcaOptions.CurrentValue);
    });

    app.MapPut("/api/config", async (
        DcaOptions updatedConfig,
        IOptionsMonitor<DcaOptions> dcaOptions,
        IConfiguration config) =>
    {
        // Validate
        var validator = new DcaOptionsValidator();
        var validationResult = validator.Validate(null, updatedConfig);
        if (validationResult.Failed)
        {
            return Results.BadRequest(new { errors = validationResult.Failures });
        }

        // Persist to appsettings.json (requires custom logic)
        await UpdateAppSettingsAsync(updatedConfig);

        // IOptionsMonitor will auto-reload
        return Results.Ok(new { message = "Config updated" });
    });

    return app;
}
```

**Source:** [Options pattern in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-10.0)

### Pattern 5: Aspire Orchestration with Nuxt Dev Server

**What:** Use `Aspire.Hosting.JavaScript` to orchestrate Nuxt dev server (`npm run dev`) alongside .NET API.

**When to use:** Local development — single `dotnet run` starts all services (PostgreSQL, Redis, API, Nuxt).

**Trade-offs:**
- **Pros:** Unified startup, service discovery, logging aggregation
- **Cons:** Requires Node.js installed, Nuxt must be in monorepo or adjacent folder

**Example:**
```csharp
// AppHost.cs (MODIFIED)
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder
    .AddPostgres("postgres")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume()
    .WithPgAdmin(c => c.WithLifetime(ContainerLifetime.Persistent).WithHostPort(5050));

var postgresdb = postgres.AddDatabase("tradingbotdb");

var redis = builder.AddRedis("redis")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithDataVolume()
    .WithRedisInsight(c => c.WithLifetime(ContainerLifetime.Persistent).WithHostPort(5051));

var redisHost = redis.Resource.PrimaryEndpoint.Property(EndpointProperty.Host);
var redisPort = redis.Resource.PrimaryEndpoint.Property(EndpointProperty.Port);

var pubSub = builder
    .AddDaprPubSub("pubsub")
    .WithMetadata("redisHost", ReferenceExpression.Create($"{redisHost}:{redisPort}"))
    .WaitFor(redis);
if (redis.Resource.PasswordParameter is not null)
{
    pubSub.WithMetadata("redisPassword", redis.Resource.PasswordParameter);
}

var apiService = builder.AddProject<Projects.TradingBot_ApiService>("apiservice")
    .WithReference(postgresdb)
    .WithReference(redis)
    .WithDaprSidecar(sidecar =>
    {
        sidecar.WithReference(pubSub);
    })
    .WithHttpHealthCheck("/health");

// NEW: Add Nuxt frontend
var nuxtApp = builder.AddNpmApp("nuxt-app", "../nuxt-app", "dev")
    .WithReference(apiService) // Service discovery for API URL
    .WithHttpEndpoint(port: 3000, env: "PORT")
    .PublishAsDockerFile();

builder.Build().Run();
```

**Sources:**
- [Orchestrate Node.js apps in Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/get-started/build-aspire-apps-with-nodejs)
- [.NET Aspire with React/NextJS (or any other Node.js)](https://medium.com/@adamtrip/net-aspire-with-react-nextjs-or-any-other-node-js-ef99f398815f)

## Data Flow

### Request Flow (API Calls)

```
[User Action: View Portfolio]
    ↓
[pages/index.vue] → usePortfolio() composable
    ↓
useFetch('/api/portfolio') → Nuxt proxy
    ↓
HTTP GET /api/portfolio → .NET API
    ↓
PortfolioEndpoints.cs → PortfolioService.GetMetrics()
    ↓
TradingBotDbContext.Purchases.ToListAsync()
    ↓
PostgreSQL (Purchase, DailyPrice tables)
    ↓
Aggregate: { totalBTC, costBasis, currentValue, pnl }
    ↓
JSON response → useFetch reactive ref (data)
    ↓
Vue template renders: {{ portfolio.totalBTC }}
```

### Real-Time Price Update Flow (SSE)

```
[Component mounted: pages/index.vue]
    ↓
usePriceStream().connect()
    ↓
EventSource → HTTP GET /api/price/stream (keep-alive)
    ↓
PriceStreamEndpoints.cs (while loop)
    ↓
Every 5 seconds: Fetch current BTC price from Hyperliquid
    ↓
Write SSE message: data: {"price": 95234.50}\n\n
    ↓
EventSource.onmessage → currentPrice.value = 95234.50
    ↓
Vue reactivity → UI updates: {{ currentPrice }}
```

### Config Update Flow (Dashboard → API → Background Service)

```
[User Action: Update BaseDailyAmount in UI]
    ↓
pages/config.vue → Submit form
    ↓
$fetch('/api/config', { method: 'PUT', body: updatedConfig })
    ↓
HTTP PUT /api/config → ConfigEndpoints.cs
    ↓
Validate with DcaOptionsValidator
    ↓
Persist to appsettings.json (custom logic)
    ↓
IOptionsMonitor auto-reloads DcaOptions
    ↓
DcaSchedulerBackgroundService.ExecuteAsync()
    ↓
Next purchase uses new BaseDailyAmount
```

### Key Data Flows

1. **Portfolio aggregation:** API aggregates Purchase records (SUM cost, SUM quantity) + latest BTC price → portfolio metrics
2. **Purchase history:** API queries Purchase table with pagination → timeline with price/multiplier metadata
3. **Backtest execution:** Frontend submits parameters → API runs BacktestSimulator → returns metrics + charts
4. **Real-time price:** SSE stream pushes BTC price every 5 seconds → reactive UI updates
5. **Config updates:** Frontend PUT request → API validates + persists → IOptionsMonitor reloads → background service uses new config

## Scaling Considerations

| Scale | Architecture Adjustments |
|-------|--------------------------|
| **0-100 users** | Current architecture is fine — single .NET API instance, Nuxt SSR on single server, PostgreSQL on single node |
| **100-1k users** | Add Redis caching for portfolio metrics (TTL 30s), consider CDN for Nuxt static assets, keep API stateless |
| **1k-10k users** | Horizontal scaling: multiple API instances behind load balancer, PostgreSQL read replicas for queries, Nuxt static site generation (SSG) for public pages |
| **10k+ users** | Consider SignalR with Redis backplane for real-time (SSE doesn't scale well beyond 10k concurrent connections), database sharding by date ranges, separate read/write databases |

### Scaling Priorities

1. **First bottleneck:** PostgreSQL query performance on Purchase table → **Fix:** Add index on `ExecutedAt DESC`, cache portfolio metrics in Redis (30s TTL)
2. **Second bottleneck:** SSE connection limits (max ~10k concurrent on single server) → **Fix:** Switch to SignalR with Redis backplane, or use polling with Redis pub-sub
3. **Third bottleneck:** Backtest parameter sweep CPU usage → **Fix:** Background job queue (Hangfire), offload to worker processes

## Anti-Patterns

### Anti-Pattern 1: Using SignalR for Simple Real-Time Updates

**What people do:** Add SignalR package, configure hubs, write connection logic on frontend for price updates.

**Why it's wrong:**
- Overkill for unidirectional updates (server → client only)
- Adds complexity (hubs, connection lifetime management, Azure SignalR Service for scale)
- SSE is simpler, lighter, and sufficient for this use case

**Do this instead:** Use SSE (Server-Sent Events) for price stream. Only use SignalR if you need bidirectional communication (e.g., live chat, collaborative editing).

**Source:** [You Probably Don't Need SignalR in .NET 10 (use Server-Sent Events)](https://systemshogun.com/p/you-probably-dont-need-signalr-in)

### Anti-Pattern 2: Fetching Same Data in Multiple Components with $fetch

**What people do:** Call `$fetch('/api/portfolio')` in multiple components, causing duplicate network requests.

**Why it's wrong:**
- No caching, no SSR deduplication
- Performance penalty (3 components = 3 identical API calls)
- Can't share reactive state

**Do this instead:** Create a composable (`usePortfolio`) with `useFetch` that returns a keyed, cached reactive ref. All components using `usePortfolio()` share the same data.

**Source:** [When to Use $fetch, useFetch, or useAsyncData in Nuxt](https://masteringnuxt.com/blog/when-to-use-fetch-usefetch-or-useasyncdata-in-nuxt-a-comprehensive-guide)

### Anti-Pattern 3: Directly Mutating appsettings.json for Config Updates

**What people do:** Read appsettings.json, parse JSON, mutate, write back to file from API endpoint.

**Why it's wrong:**
- File I/O race conditions (multiple requests = corrupted JSON)
- Requires file system write permissions (security risk in production)
- Doesn't work in containerized environments (ephemeral file systems)

**Do this instead:**
- **Option 1 (Simple):** Store DcaOptions in database (ConfigSettings table), use IOptionsMonitor with custom config provider
- **Option 2 (Production):** Use Azure App Configuration or similar external config store, IOptionsMonitor auto-reloads from there

**Source:** [Auto Refresh Settings Changes in ASP.NET Core Runtime](https://edi.wang/post/2019/1/5/auto-refresh-settings-changes-in-aspnet-core-runtime)

### Anti-Pattern 4: Configuring CORS for "AllowAnyOrigin" in Production

**What people do:** Keep the development CORS policy (`AllowAnyOrigin`) in production for simplicity.

**Why it's wrong:**
- Security vulnerability (any website can call your API)
- Credentials (cookies, auth tokens) won't work with `AllowAnyOrigin`

**Do this instead:**
- **Development:** Use Nuxt proxy (no CORS needed)
- **Production:** Configure specific origin (e.g., `https://dashboard.example.com`) or use reverse proxy (Nginx, Caddy)

**Example:**
```csharp
// Program.cs
builder.Services.AddCors(options =>
{
    options.AddPolicy("Production", policy =>
    {
        policy.WithOrigins(builder.Configuration["AllowedOrigins"] ?? "https://dashboard.example.com")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// Use policy conditionally
if (app.Environment.IsProduction())
{
    app.UseCors("Production");
}
else
{
    app.UseCors("AllowAll"); // Development only
}
```

## Integration Points

### External Services

| Service | Integration Pattern | Notes |
|---------|---------------------|-------|
| **Hyperliquid API** | Existing HyperliquidClient (no changes) | Used for current BTC price in SSE stream |
| **CoinGecko API** | Existing CoinGeckoClient (no changes) | Historical data already available via `/api/backtest/data` endpoints |
| **Telegram Bot** | Existing TelegramNotifier (no changes) | No dashboard integration needed |

### Internal Boundaries (New vs Existing)

| Boundary | Communication | Notes |
|----------|---------------|-------|
| **Nuxt ↔ .NET API** | HTTP (JSON) + SSE | Proxy via Nuxt route rules in dev, direct or reverse proxy in prod |
| **Dashboard API ↔ Backtest API** | Same .NET process (shared services) | PortfolioService can reuse PriceDataService for current price |
| **ConfigEndpoints ↔ DcaSchedulerBackgroundService** | IOptionsMonitor (in-process) | Config updates auto-reload via IOptionsMonitor, no pub-sub needed |
| **PriceStreamEndpoints ↔ HyperliquidClient** | Direct service call | Inject IHyperliquidClient into endpoint, call GetPriceAsync() in SSE loop |

## New vs Modified Components

### New Components (Dashboard-Specific)

**Frontend (Nuxt):**
- `nuxt-app/` — Entire Nuxt project (new)
- All pages, components, composables, types

**Backend (.NET):**
- `Endpoints/PortfolioEndpoints.cs` — Portfolio metrics API
- `Endpoints/PurchaseEndpoints.cs` — Purchase history with pagination
- `Endpoints/ConfigEndpoints.cs` — Config CRUD API
- `Endpoints/PriceStreamEndpoints.cs` — SSE price stream
- `Application/Services/PortfolioService.cs` — Portfolio aggregation logic
- `Application/Services/ConfigUpdateService.cs` — Config validation + persistence (if using DB option)
- `Models/PortfolioMetrics.cs` — Response DTO

### Modified Components

**AppHost:**
- `TradingBot.AppHost/AppHost.cs` — Add Nuxt orchestration with `AddNpmApp`
- `TradingBot.AppHost/TradingBot.AppHost.csproj` — Add `Aspire.Hosting.JavaScript` package reference

**API Service:**
- `TradingBot.ApiService/Program.cs` — Add `app.MapPortfolioEndpoints()`, `app.MapPurchaseEndpoints()`, `app.MapConfigEndpoints()`, `app.MapPriceStreamEndpoints()`
- No changes to existing models, services, or backtest endpoints

### Unchanged Components (Reused)

- `TradingBotDbContext` (existing Purchase, DailyPrice, IngestionJob tables)
- `DcaOptions` (existing, but now read/write via API)
- `HyperliquidClient` (reused for current price in SSE stream)
- `PriceDataService` (reused for latest price from DB)
- Backtest endpoints (no changes, called as-is from Nuxt)
- Data ingestion endpoints (no changes, called as-is from Nuxt)

## Suggested Build Order (Considering Dependencies)

### Phase 1: API Foundation (Backend First)
**Goal:** Build dashboard-specific API endpoints before frontend.

1. Create `PortfolioEndpoints.cs` with GET `/api/portfolio`
   - Implement `PortfolioService.GetMetricsAsync()` to aggregate Purchase records
   - Return DTO: `{ totalBTC, costBasis, currentValue, pnl, purchaseCount }`
2. Create `PurchaseEndpoints.cs` with GET `/api/purchases?page=1&limit=50`
   - Query Purchase table with pagination, order by `ExecutedAt DESC`
   - Return list of purchases with metadata (price, multiplier, tier)
3. Test API endpoints with curl/Postman before building frontend

**Why first:** Frontend can't be built without API contracts defined.

### Phase 2: Aspire Integration (Orchestration)
**Goal:** Set up Nuxt dev server orchestration before building Nuxt app.

4. Update `TradingBot.AppHost.csproj` to add `Aspire.Hosting.JavaScript` package
5. Modify `AppHost.cs` to add `builder.AddNpmApp("nuxt-app", "../nuxt-app", "dev")`
6. Create basic Nuxt project: `npx nuxi@latest init nuxt-app`
7. Verify Aspire can start Nuxt dev server alongside API

**Why second:** Ensures local dev environment works before building features.

### Phase 3: Basic Nuxt Pages (Frontend Foundation)
**Goal:** Set up Nuxt structure and basic navigation.

8. Configure `nuxt.config.ts` with route proxy (`/api/**` → `http://localhost:5096/api/**`)
9. Create TypeScript types (`types/portfolio.ts`, `types/purchase.ts`) mirroring .NET DTOs
10. Create basic pages: `pages/index.vue` (portfolio), `pages/purchases.vue` (history)
11. Create layout: `layouts/default.vue` with header and navigation

**Why third:** Provides structure for feature implementation.

### Phase 4: Data Fetching (Composables)
**Goal:** Implement data-fetching logic before UI components.

12. Create `composables/usePortfolio.ts` with `useFetch('/api/portfolio')`
13. Create `composables/usePurchases.ts` with pagination support
14. Test composables in pages (verify API calls work via proxy)

**Why fourth:** Separates data layer from UI, testable independently.

### Phase 5: UI Components (Portfolio + Purchase History)
**Goal:** Build interactive UI for portfolio and purchase history.

15. Create `components/portfolio/PortfolioCard.vue` (display total BTC, cost basis, P&L)
16. Create `components/purchases/PurchaseTimeline.vue` (paginated purchase history)
17. Integrate components into `pages/index.vue` and `pages/purchases.vue`

**Why fifth:** Depends on composables and API being functional.

### Phase 6: Real-Time Price Stream (SSE)
**Goal:** Add live price updates to dashboard.

18. Create `PriceStreamEndpoints.cs` with SSE implementation
19. Create `composables/usePriceStream.ts` with EventSource
20. Add current price display to `pages/index.vue`

**Why sixth:** Independent feature, can be added after basic portfolio works.

### Phase 7: Config Management (Optional)
**Goal:** Enable config editing from dashboard.

21. Create `ConfigEndpoints.cs` with GET/PUT `/api/config`
22. Implement `ConfigUpdateService.cs` (DB-backed or appsettings.json strategy)
23. Create `pages/config.vue` with form for editing DcaOptions
24. Create `composables/useConfig.ts` for CRUD operations

**Why seventh:** Most complex feature, requires decision on config persistence strategy.

### Phase 8: Backtest Integration (Reuse Existing)
**Goal:** Integrate existing backtest endpoints into dashboard UI.

25. Create `composables/useBacktest.ts` wrapping existing `/api/backtest` endpoints
26. Create `pages/backtest.vue` with parameter form
27. Create `components/backtest/BacktestChart.vue` for visualizing results
28. Reuse existing backtest types (no backend changes needed)

**Why last:** Fully independent feature, reuses existing API endpoints.

## Sources

**Nuxt + .NET Integration:**
- [Integrating an ASP.NET Core API with a Nuxt Front End: A Step-by-Step Guide](https://techwatching.dev/posts/aspnetcore-with-nuxt/)
- [Nuxt3 API Proxy Setup](https://hackmd.io/@lilybon/nuxt3-api-proxy-setup)

**Real-Time Updates (SSE vs SignalR):**
- [Server-Sent Events in ASP.NET Core and .NET 10](https://www.milanjovanovic.tech/blog/server-sent-events-in-aspnetcore-and-dotnet-10)
- [You don't need SignalR for real-time updates | Server-Sent Events in .NET/C#](https://medium.com/@denmaklucky/you-dont-need-signalr-for-real-time-updates-server-sent-events-in-net-c-e032ff5d096e)
- [You Probably Don't Need SignalR in .NET 10 (use Server-Sent Events)](https://systemshogun.com/p/you-probably-dont-need-signalr-in)
- [Server-Sent Events in Nuxt 3: A Beginner's Guide to Real-Time Features](https://medium.com/@saadamd/server-sent-events-in-nuxt-3-a-beginners-guide-to-real-time-features-c8e760207aca)

**Aspire Orchestration:**
- [Orchestrate Node.js apps in Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/get-started/build-aspire-apps-with-nodejs)
- [.NET Aspire with React/NextJS (or any other Node.js)](https://medium.com/@adamtrip/net-aspire-with-react-nextjs-or-any-other-node-js-ef99f398815f)
- [Aspire for JavaScript developers](https://devblogs.microsoft.com/aspire/aspire-for-javascript-developers/)
- [.NET Aspirations - Tailor It To Your Stack](https://techwatching.dev/posts/aspire-tailor-to-your-stack/)

**Nuxt Data Fetching:**
- [When to Use $fetch, useFetch, or useAsyncData in Nuxt: A Comprehensive Guide](https://masteringnuxt.com/blog/when-to-use-fetch-usefetch-or-useasyncdata-in-nuxt-a-comprehensive-guide)
- [Data Fetching in Nuxt 3: useFetch, useAsyncData, and $fetch](https://www.zignuts.com/blog/nuxt3-data-fetching-guide)
- [Data Fetching with Nuxt 3](https://medium.com/@enestalayy/data-fetching-with-nuxt-3-ede89fb0509f)

**ASP.NET Core Configuration:**
- [Options pattern in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-10.0)
- [Auto Refresh Settings Changes in ASP.NET Core Runtime](https://edi.wang/post/2019/1/5/auto-refresh-settings-changes-in-aspnet-core-runtime)

**Health Checks & Monitoring:**
- [Health checks in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0)
- [AspNetCore.Diagnostics.HealthChecks (UI)](https://github.com/Xabaril/AspNetCore.Diagnostics.HealthChecks)

---
*Architecture research for: Nuxt + .NET Trading Bot Dashboard Integration*
*Researched: 2026-02-13*
