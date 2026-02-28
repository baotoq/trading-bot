# BTC Smart DCA Bot

A personal investment platform that automatically accumulates Bitcoin on the Hyperliquid spot market using intelligent dollar-cost-averaging with multipliers based on price drops and bear market conditions. Includes a backtesting engine for strategy validation, a Nuxt 4 web dashboard, and a Flutter iOS app that serves as a unified portfolio tracker across crypto, Vietnamese ETFs, and fixed deposits with live prices and multi-currency P&L.

## Table of Contents

- [Key Features](#key-features)
- [Tech Stack](#tech-stack)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Configuration](#configuration)
- [Architecture](#architecture) 
- [Mobile App](#mobile-app)
- [Web Dashboard](#web-dashboard)
- [Backtesting Engine](#backtesting-engine)
- [API Endpoints](#api-endpoints)
- [Testing](#testing)
- [Available Commands](#available-commands)
- [Troubleshooting](#troubleshooting)
- [Development Roadmap](#development-roadmap)
- [License](#license)

## Key Features

**DCA Engine**
- Daily fixed-amount BTC purchases on Hyperliquid spot market via EIP-712 signed IOC orders
- Smart multipliers that increase position size during price dips (1.5x at 5%, 2x at 10%, 3x at 20% drop from 30-day high)
- Bear market boost (+1.5x additive when BTC is below its 200-day moving average, capped at 4.5x)
- Configurable schedule, dry-run simulation mode, and missed purchase detection
- Telegram notifications on each buy with multiplier reasoning and weekly summaries
- FCM push notifications to the mobile app with deep-link navigation

**Backtesting**
- Day-by-day simulation comparing smart DCA vs fixed DCA using the same pure `MultiplierCalculator`
- Parameter sweep with cartesian product grid search, ranked results, and walk-forward 70/30 validation
- CoinGecko historical data pipeline with incremental ingestion and gap detection

**Portfolio Tracker**
- Multi-asset portfolio: crypto, Vietnamese ETFs (VNDirect), and fixed deposits with interest accrual
- Live prices from CoinGecko (crypto), VNDirect dchart-api (ETFs), and open.er-api.com (USD/VND)
- Dual-currency P&L (USD/VND) with per-asset breakdowns and allocation donut chart
- DCA bot purchases auto-imported into portfolio via domain events

**Frontend**
- Flutter iOS app with premium glassmorphism design, animated charts, staggered entrance animations
- Nuxt 4 web dashboard with portfolio overview, purchase history, price charts, backtest visualization
- Both frontends use `x-api-key` server-side proxying (API key never reaches the client)

## Tech Stack

| Category | Technology |
|---|---|
| **Runtime** | .NET 10.0 / C# |
| **Framework** | ASP.NET Core Minimal APIs |
| **Database** | PostgreSQL 16 (EF Core 10.0) |
| **Cache** | Redis (StackExchange.Redis + MessagePack serialization) |
| **Message Bus** | Dapr pub/sub with transactional outbox |
| **Orchestration** | .NET Aspire 13.0 |
| **Crypto Signing** | Nethereum (EIP-712 for Hyperliquid) |
| **Price Data** | CoinGecko (crypto), VNDirect dchart-api (VN ETFs), open.er-api.com (FX rates) |
| **Notifications** | Telegram.Bot, Firebase Cloud Messaging |
| **DDD** | Vogen (typed IDs/value objects), ErrorOr (result pattern), Ardalis.Specification |
| **Observability** | Serilog (structured logging), OpenTelemetry (traces + metrics) |
| **Mobile** | Flutter 3.7+ / Dart, Riverpod, GoRouter, fl_chart |
| **Web Dashboard** | Nuxt 4, Vue 3, @nuxt/ui v4, Chart.js |
| **Testing** | xUnit, FluentAssertions, Snapper, NSubstitute, Testcontainers |

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for PostgreSQL, Redis, and Dapr via Aspire)
- [Node.js 20+](https://nodejs.org/) (for the web dashboard, optional)
- [Flutter SDK 3.7+](https://flutter.dev/docs/get-started/install) (for the mobile app, optional)
- [Xcode](https://developer.apple.com/xcode/) (for iOS development, optional)

## Getting Started

### 1. Clone the Repository

```bash
git clone <repository-url>
cd trading-bot
```

### 2. Configure Secrets

The API service uses .NET User Secrets for sensitive configuration. Run these from the `TradingBot.ApiService/` directory:

```bash
cd TradingBot.ApiService

# Required: Hyperliquid exchange credentials
dotnet user-secrets set "Hyperliquid:PrivateKey" "<your-private-key>"
dotnet user-secrets set "Hyperliquid:WalletAddress" "<your-wallet-address>"

# Required: API key for dashboard/mobile-to-backend authentication
dotnet user-secrets set "Dashboard:ApiKey" "<any-secret-string>"

# Optional: Telegram bot for purchase notifications and weekly summaries
dotnet user-secrets set "Telegram:BotToken" "<your-bot-token>"
dotnet user-secrets set "Telegram:ChatId" "<your-chat-id>"

# Optional: CoinGecko API key for historical data (free tier works without it)
dotnet user-secrets set "CoinGecko:ApiKey" "<your-api-key>"
```

### 3. Run with Aspire (Recommended)

Aspire orchestrates all infrastructure and services with a single command:

```bash
cd TradingBot.AppHost
dotnet run
```

This automatically starts:

| Service | Port | Description |
|---|---|---|
| API Service | dynamic | Main .NET API (assigned by Aspire) |
| PostgreSQL | 5432 | Primary database |
| Redis | 6379 | Distributed cache |
| PgAdmin | 5050 | Database management UI |
| RedisInsight | 5051 | Redis management UI |
| Dapr Sidecar | — | Pub/sub messaging |
| Aspire Dashboard | 15888 | Service health, logs, traces |

Open the Aspire dashboard URL (printed to console) to see all services, health checks, distributed traces, and structured logs.

Database migrations run automatically on API service startup — no manual migration step needed.

### 4. Run Without Aspire

If you prefer to manage infrastructure manually:

1. Start PostgreSQL and Redis (via Docker or native install)
2. Set connection strings via environment variables or `appsettings.Development.json`
3. Run the API service:

```bash
cd TradingBot.ApiService
dotnet run
```

Note: Dapr sidecar must be running separately for pub-sub events (Telegram/FCM notifications) to work.

### 5. Run the Mobile App (Optional)

```bash
cd TradingBot.Mobile
flutter pub get
flutter run --dart-define=API_BASE_URL=http://localhost:5000 --dart-define=API_KEY=<your-dashboard-api-key>
```

### 6. Run the Web Dashboard (Optional)

```bash
cd TradingBot.Dashboard
npm install
npm run dev
```

Set environment variables for the dashboard:

```bash
NUXT_API_KEY=<same-as-Dashboard:ApiKey>
NUXT_PUBLIC_API_ENDPOINT=http://localhost:5000
```

## Configuration

### DCA Strategy

Edit `TradingBot.ApiService/appsettings.json` or manage via the Configuration API:

```json
{
  "DcaOptions": {
    "BaseDailyAmount": 10.0,
    "DailyBuyHour": 14,
    "DailyBuyMinute": 0,
    "HighLookbackDays": 30,
    "BearMarketMaPeriod": 200,
    "BearBoostFactor": 1.5,
    "MaxMultiplierCap": 4.5,
    "DryRun": true,
    "MultiplierTiers": [
      { "DropPercentage": 0.05, "Multiplier": 1.5 },
      { "DropPercentage": 0.10, "Multiplier": 2.0 },
      { "DropPercentage": 0.20, "Multiplier": 3.0 }
    ]
  }
}
```

### Configuration Reference

| Parameter | Description | Default |
|---|---|---|
| `BaseDailyAmount` | USD amount to buy daily (before multipliers) | `10.0` |
| `DailyBuyHour` | Hour (UTC) to execute daily buy | `14` |
| `DailyBuyMinute` | Minute to execute daily buy | `0` |
| `HighLookbackDays` | Days to look back for price high calculation | `30` |
| `BearMarketMaPeriod` | Moving average period for bear market detection | `200` |
| `BearBoostFactor` | Additive multiplier boost when below MA | `1.5` |
| `MaxMultiplierCap` | Maximum total multiplier allowed | `4.5` |
| `DryRun` | If true, simulates purchases without placing orders | `true` |
| `MultiplierTiers` | Drop percentage thresholds and their multipliers | See above |
| `Hyperliquid:IsTestnet` | Use testnet vs mainnet API | `true` |

### Multiplier Logic

The bot calculates how far the current price has dropped from its 30-day high, then applies the highest matching tier:

```
Current price: $90,000 | 30-day high: $100,000 | Drop: 10%
→ Matches 10% tier → Multiplier: 2.0x → Buys $20 instead of $10

If also below 200-day MA (bear market):
→ Bear boost: +1.5 → Total multiplier: 3.5x → Buys $35
→ Capped at MaxMultiplierCap (4.5x)
```

### Secrets Reference

| Secret | Required | Description |
|---|---|---|
| `Hyperliquid:PrivateKey` | Yes | Private key for EIP-712 order signing |
| `Hyperliquid:WalletAddress` | Yes | Wallet address matching the private key |
| `Dashboard:ApiKey` | Yes | Shared secret for dashboard/mobile authentication |
| `Telegram:BotToken` | No | Telegram bot token for notifications |
| `Telegram:ChatId` | No | Telegram chat ID for notifications |
| `CoinGecko:ApiKey` | No | API key for historical price data (free tier works) |

## Architecture

### Project Structure

```
trading-bot/
├── TradingBot.ApiService/              # Main API service (.NET 10.0)
│   ├── Application/
│   │   ├── BackgroundJobs/             # Scheduled services
│   │   │   ├── DcaSchedulerBackgroundService      # Daily buy at configured time
│   │   │   ├── PriceDataRefreshService            # Daily price refresh at 00:05 UTC
│   │   │   ├── WeeklySummaryService               # Sunday Telegram summary
│   │   │   └── MissedPurchaseVerificationService  # Every 30 min verification
│   │   ├── Events/                     # Integration events (6 event types)
│   │   ├── Handlers/                   # Event handlers (Telegram + FCM)
│   │   ├── Health/                     # Health check implementations
│   │   ├── Services/
│   │   │   ├── Backtest/               # Backtesting engine
│   │   │   │   ├── BacktestSimulator              # Day-by-day simulation
│   │   │   │   ├── ParameterSweepService          # Grid search optimization
│   │   │   │   └── WalkForwardValidator           # Overfitting detection
│   │   │   ├── HistoricalData/         # CoinGecko data pipeline
│   │   │   │   ├── DataIngestionService           # Fetch & bulk upsert prices
│   │   │   │   ├── GapDetectionService            # Find missing dates
│   │   │   │   └── IngestionJobQueue              # In-memory job queue
│   │   │   ├── DcaExecutionService                # Core buy logic
│   │   │   ├── MultiplierCalculator               # Pure static multiplier math
│   │   │   ├── PriceDataService                   # Price fetching & caching
│   │   │   ├── ConfigurationService               # DCA config CRUD
│   │   │   └── InterestCalculator                 # Fixed deposit interest accrual
│   │   └── Specifications/             # Ardalis.Specification query objects
│   ├── BuildingBlocks/                 # Reusable infrastructure
│   │   ├── Pubsub/
│   │   │   ├── Outbox/                 # Transactional outbox pattern
│   │   │   ├── Dapr/                   # Dapr pub/sub integration
│   │   │   └── Abstraction/            # IMessageBroker, IEventPublisher
│   │   ├── DistributedLocks/           # PostgreSQL advisory locks
│   │   ├── BaseEntity.cs               # UUIDv7 base entity
│   │   ├── AuditedEntity.cs            # CreatedAt/UpdatedAt tracking
│   │   ├── AggregateRoot.cs            # Domain event collection
│   │   └── IDomainEvent.cs             # Domain event marker interface
│   ├── Configuration/                  # Strongly-typed options with validation
│   ├── Endpoints/                      # Minimal API endpoints (8 groups)
│   ├── Infrastructure/
│   │   ├── Data/                       # EF Core DbContext & migrations
│   │   ├── Hyperliquid/                # Exchange client & EIP-712 signing
│   │   ├── CoinGecko/                  # Historical price data client
│   │   ├── PriceFeeds/                 # Live price providers
│   │   │   ├── Crypto/                 # CoinGecko live prices
│   │   │   ├── Etf/                    # VNDirect ETF prices
│   │   │   └── ExchangeRate/           # USD/VND exchange rate
│   │   ├── Firebase/                   # FCM push notifications
│   │   ├── Locking/                    # Distributed lock wrappers
│   │   └── Telegram/                   # Telegram bot service
│   └── Models/                         # Domain entities & value objects
│       ├── Ids/                        # Vogen strongly-typed IDs
│       ├── Values/                     # Vogen value objects (Price, Quantity, etc.)
│       ├── Purchase.cs                 # Buy order aggregate root
│       ├── DailyPrice.cs               # Historical OHLCV data
│       ├── PortfolioAsset.cs           # Multi-asset portfolio aggregate
│       ├── AssetTransaction.cs         # Buy/sell transactions
│       ├── FixedDeposit.cs             # Bank fixed deposits
│       ├── DcaConfiguration.cs         # Runtime DCA config (single-row)
│       └── DeviceToken.cs              # FCM device registrations
├── TradingBot.AppHost/                 # Aspire orchestration
│   └── AppHost.cs                      # PostgreSQL, Redis, Dapr wiring
├── TradingBot.ServiceDefaults/         # Shared service config (OTel, resilience)
├── TradingBot.Dashboard/               # Nuxt 4 web dashboard
│   ├── app/
│   │   ├── components/                 # Vue components (dashboard, backtest, config)
│   │   ├── composables/                # Vue composables (API state management)
│   │   ├── pages/                      # Page routes
│   │   ├── plugins/                    # Chart.js registration
│   │   └── types/                      # TypeScript interfaces
│   └── server/
│       ├── api/                        # Server proxy routes (x-api-key injection)
│       └── utils/                      # Auth helper
├── TradingBot.Mobile/                  # Flutter iOS app
│   └── lib/
│       ├── app/                        # Router (GoRouter) & theme (glassmorphism)
│       ├── core/                       # API client (Dio), FCM service, shared widgets
│       ├── features/                   # Feature modules (home, chart, history, config, portfolio)
│       │   ├── home/                   # Dashboard overview with glass cards
│       │   ├── chart/                  # Gradient glow price chart
│       │   ├── history/                # Infinite-scroll purchase history
│       │   ├── config/                 # DCA config editor
│       │   └── portfolio/              # Multi-asset tracker with donut chart
│       └── shared/                     # Navigation shell
├── tests/
│   └── TradingBot.ApiService.Tests/    # 103 automated tests
└── .planning/                          # Architecture & planning docs
```

### Event Flow

```
Purchase Execution
    ↓
Domain Event (IDomainEvent) — raised by aggregate root
    ↓
DomainEventOutboxInterceptor — saves OutboxMessage in same DB transaction
    ↓
OutboxMessageBackgroundService — polls DB every 5 seconds (batch of 100)
    ↓
OutboxMessageProcessor — publishes via DaprMessageBroker (3 retries, then dead-letter)
    ↓
Dapr sidecar — routes to subscriber HTTP endpoint
    ↓
MediatR handler — sends Telegram message + FCM push notification
```

### Key Patterns

| Pattern | Implementation |
|---|---|
| **Transactional Outbox** | Domain events saved to `OutboxMessages` table in the same transaction as the entity change, then published asynchronously by a background service |
| **Strongly-Typed IDs** | Vogen source-generated `record struct` types (`PurchaseId`, `PortfolioAssetId`, etc.) with EF Core value converters |
| **Value Objects** | Vogen types for domain primitives (`Price`, `Quantity`, `UsdAmount`, `VndAmount`, `Multiplier`, `Percentage`, `Symbol`) |
| **Aggregate Roots** | `Purchase`, `PortfolioAsset`, `FixedDeposit` with domain event collection and factory methods |
| **Result Pattern** | `ErrorOr<T>` for domain validation (e.g., `DcaConfiguration` updates) |
| **Specification Pattern** | Ardalis.Specification for composable EF Core queries (7 specs) |
| **Distributed Locking** | PostgreSQL advisory locks to prevent duplicate DCA executions |
| **Stale-While-Revalidate** | Redis cache returns stale data immediately while refreshing in the background (price feeds) |

### Database Schema

```
purchases                                  daily_prices (composite PK: date + symbol)
├── id (uuid, PK, UUIDv7)                ├── date (date, indexed)
├── executed_at (timestamptz, indexed)    ├── symbol (varchar 20)
├── price (decimal 18,8)                  ├── open (decimal 18,8)
├── quantity (decimal 18,8)               ├── high (decimal 18,8)
├── cost (decimal 18,2)                   ├── low (decimal 18,8)
├── multiplier (decimal 4,2)              ├── close (decimal 18,8)
├── status (varchar 20)                   └── volume (decimal 18,8)
├── is_dry_run (boolean)
├── order_id (varchar 100)                ingestion_jobs
├── raw_response (text)                   ├── id (uuid, PK)
├── failure_reason (varchar 500)          ├── status (varchar 30, indexed)
├── multiplier_tier (varchar 50)          ├── created_at (timestamptz, indexed)
├── drop_percentage (decimal 8,4)         ├── started_at / completed_at
├── high_30_day (decimal 18,8)            ├── start_date / end_date
├── ma_200_day (decimal 18,8)             ├── force (boolean)
├── created_at (timestamptz)              ├── records_fetched / gaps_detected
└── updated_at (timestamptz)              └── error_message (varchar 2000)

portfolio_assets                           asset_transactions
├── id (uuid, PK)                         ├── id (uuid, PK)
├── name (varchar 100)                    ├── portfolio_asset_id (uuid, FK, indexed)
├── ticker (varchar 20)                   ├── date (date, indexed)
├── asset_type (varchar 20)               ├── quantity (decimal 18,8)
├── native_currency (varchar 5)           ├── price_per_unit (decimal 18,8)
├── created_at (timestamptz)              ├── fee (decimal 18,2)
└── updated_at (timestamptz)              ├── currency (varchar 5)
                                          ├── type (varchar 10) — Buy/Sell
fixed_deposits                            ├── source (varchar 10) — Manual/DcaBot
├── id (uuid, PK)                         ├── source_purchase_id (uuid, unique, nullable)
├── bank_name (varchar 100)               ├── created_at (timestamptz)
├── principal (decimal 18,0)              └── updated_at (timestamptz)
├── annual_interest_rate (decimal 8,6)
├── start_date / maturity_date            dca_configurations (single-row constraint)
├── compounding_frequency (varchar 20)    ├── id (uuid, PK, fixed value)
├── status (varchar 10, indexed)          ├── base_daily_amount (decimal 18,2)
├── created_at (timestamptz)              ├── daily_buy_hour / daily_buy_minute
└── updated_at (timestamptz)              ├── multiplier_tiers (jsonb)
                                          └── bear/MA/cap settings

outbox_messages                            dead_letter_messages
├── id (uuid, PK)                         ├── id (uuid, PK)
├── event_name (indexed)                  ├── event_name (indexed)
├── payload (text)                        ├── payload (text)
├── status — Pending/Processing/Published ├── error_message
├── retry_count                           └── failed_at (indexed)
└── created_at

device_tokens
├── id (uuid, PK)
├── token (varchar 512, unique)
├── platform (varchar 10)
├── created_at (timestamptz)
└── updated_at (timestamptz)
```

## Mobile App

The Flutter iOS app provides full feature parity with the web dashboard plus portfolio tracking capabilities.

**5-Tab Navigation:**

| Tab | Features |
|---|---|
| **Home** | Hero balance card, mini allocation donut, recent activity, quick action buttons, staggered entrance animations |
| **Chart** | Gradient glow price chart with animated draw-in, purchase marker dots with radial glow, frosted glass tooltip, 6 timeframe presets |
| **History** | Infinite-scroll purchase list with date/tier filtering via bottom sheet |
| **Config** | View/edit DCA configuration with Zod-equivalent validation |
| **Portfolio** | Multi-asset tracker with allocation donut, per-asset P&L, USD/VND toggle, fixed deposit interest tracking |

**Design System:**
- Premium glassmorphism design with frosted glass cards (`BackdropFilter` + tint + border)
- Ambient gradient background with three radial color orbs
- Dark Material 3 theme seeded from Bitcoin orange (`#F7931A`)
- `GlassVariant.scrollItem` (no blur) for list items to prevent Impeller jank
- iOS Reduce Transparency and Reduce Motion honored
- Tabular figures for aligned monetary values

**Architecture:**
- Feature-first module organization (`features/{name}/data/` + `features/{name}/presentation/`)
- Riverpod for state management with code generation (`@riverpod`)
- GoRouter with `StatefulShellRoute.indexedStack` (preserves navigator stacks)
- Dio HTTP client with `x-api-key` interceptor
- Firebase Cloud Messaging for push notifications with deep-link navigation
- 30-second auto-refresh polling via self-invalidating Riverpod providers
- Build-time configuration via `--dart-define` for API URL and API key

## Web Dashboard

The Nuxt 4 web dashboard provides real-time monitoring of the DCA bot plus backtesting tools.

**Tabs:**
- **Dashboard** — Portfolio stats (total BTC, cost basis, P&L), interactive price chart with purchase markers and cost basis line, live bot status with health badge and countdown timer, paginated purchase history with infinite scroll
- **Configuration** — Full DCA config editor with server-side validation, multiplier tier management, and reset to defaults
- **Backtest** — Single backtest with equity curve visualization, parameter sweep with ranked results table, side-by-side comparison of up to 3 runs

**Architecture:**
- Nuxt server routes proxy all API calls to the .NET backend, injecting the `x-api-key` header server-side
- Browser never sees the API key — calls go to Nuxt server at `/api/**`, which forwards to the backend
- 10-second auto-refresh polling for portfolio and status data
- Chart.js with annotation plugin for purchase markers and cost basis line
- Vue composables manage all API state (`useDashboard`, `usePurchaseHistory`, `useBacktest`, `useConfig`)

## Backtesting Engine

The backtesting engine simulates the smart DCA strategy against historical price data using the same pure `MultiplierCalculator` that runs in production.

**Capabilities:**
- Day-by-day simulation with drop-from-high, tier matching, and bear market boost
- Compares smart DCA vs same-base fixed DCA and match-total fixed DCA
- Metrics: cost basis, total BTC, max drawdown, return %, efficiency ratio, tier breakdown
- Parameter sweep with cartesian product grid search and ranked results
- Walk-forward validation with 70/30 train/test split for overfitting detection
- Pre-built sweep presets (conservative, aggressive, etc.)

**Run a Backtest:**

```bash
curl -X POST http://localhost:5000/api/backtest \
  -H "Content-Type: application/json" \
  -d '{
    "startDate": "2024-01-01",
    "endDate": "2025-01-01",
    "baseDailyAmount": 10,
    "tiers": [
      { "dropPercentage": 5, "multiplier": 1.5 },
      { "dropPercentage": 10, "multiplier": 2.0 },
      { "dropPercentage": 20, "multiplier": 3.0 }
    ]
  }'
```

**Ingest Historical Data:**

```bash
# Start data ingestion (fetches 4 years from CoinGecko)
curl -X POST http://localhost:5000/api/backtest/data/ingest

# Check data coverage
curl http://localhost:5000/api/backtest/data/status
```

## API Endpoints

All endpoints prefixed with `/api/` require the `x-api-key` header unless noted.

### Dashboard

| Method | Path | Description |
|---|---|---|
| GET | `/api/dashboard/portfolio` | Portfolio overview (total BTC, cost basis, current price, P&L) |
| GET | `/api/dashboard/purchases` | Cursor-paginated purchase history (`cursor`, `pageSize`, `startDate`, `endDate`, `tier`) |
| GET | `/api/dashboard/status` | Bot health, next buy time, last purchase info |
| GET | `/api/dashboard/chart` | Price chart data with purchase markers (`timeframe`: 7D/1M/3M/6M/1Y/All) |
| GET | `/api/dashboard/config` | Current DCA configuration (read-only) |

### Portfolio

| Method | Path | Description |
|---|---|---|
| GET | `/api/portfolio/summary` | Total portfolio value, P&L, allocation breakdown |
| POST | `/api/portfolio/assets` | Create a new portfolio asset |
| GET | `/api/portfolio/assets/{id}/transactions` | Get transactions for an asset (with date/type filters) |
| POST | `/api/portfolio/assets/{id}/transactions` | Record a buy/sell transaction |
| GET | `/api/portfolio/fixed-deposits` | List all fixed deposits |
| POST | `/api/portfolio/fixed-deposits` | Create a new fixed deposit |
| PUT | `/api/portfolio/fixed-deposits/{id}` | Update a fixed deposit |
| DELETE | `/api/portfolio/fixed-deposits/{id}` | Delete a fixed deposit |

### Configuration

| Method | Path | Description |
|---|---|---|
| GET | `/api/config` | Current DCA configuration |
| PUT | `/api/config` | Update DCA configuration (server-validated) |
| GET | `/api/config/defaults` | Default values from appsettings.json |

### Backtesting

| Method | Path | Description |
|---|---|---|
| POST | `/api/backtest` | Run single backtest with config overrides |
| POST | `/api/backtest/sweep` | Parameter sweep with ranking and walk-forward validation |
| GET | `/api/backtest/presets/{name}` | Inspect a sweep preset |

### Historical Data

| Method | Path | Description |
|---|---|---|
| POST | `/api/backtest/data/ingest` | Trigger CoinGecko data ingestion |
| GET | `/api/backtest/data/status` | Check data coverage, gaps, freshness |
| GET | `/api/backtest/data/ingest/{jobId}` | Poll ingestion job progress |

### Devices

| Method | Path | Description |
|---|---|---|
| POST | `/api/devices/register` | Register an FCM device token for push notifications |

### Infrastructure

| Method | Path | Description |
|---|---|---|
| GET | `/` | Service health check message |
| GET | `/health` | ASP.NET Core health check endpoint |

## Testing

```bash
# Run all 103 tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~MultiplierCalculatorTests"

# Run with verbose output
dotnet test --verbosity normal
```

### Test Suite (103 tests)

| Category | Tests | Type | Description |
|---|---|---|---|
| MultiplierCalculator | 24 | Unit (theory) | Tier matching, bear boost, cap enforcement, edge cases |
| BacktestSimulator | 28 | Unit + Snapshot | Day-by-day simulation, golden file validation via Snapper |
| InterestCalculator | 14 | Unit | Fixed deposit compound interest accrual |
| Price Feed Providers | 14 | Unit | CoinGecko, VNDirect, OpenER API response parsing with mock HTTP |
| Endpoint Integration | 13 | Integration | Portfolio + FixedDeposit endpoints via WebApplicationFactory + Testcontainers |
| Specification Queries | 9 | Integration | EF Core specs against real PostgreSQL via Testcontainers |
| Placeholder | 1 | — | — |

### Test Structure

```
tests/TradingBot.ApiService.Tests/
├── Application/
│   ├── Services/
│   │   ├── MultiplierCalculatorTests.cs        # Pure logic, theory-driven
│   │   ├── BacktestSimulatorTests.cs           # Snapshot testing with Snapper
│   │   ├── InterestCalculatorTests.cs          # Compound interest formulas
│   │   └── _snapshots/                         # Golden snapshot JSON files
│   └── Specifications/
│       ├── PostgresFixture.cs                  # Testcontainers shared fixture
│       ├── DailyPrices/DailyPriceSpecsTests.cs
│       ├── Portfolio/PortfolioEntityTests.cs
│       └── Purchases/PurchaseSpecsTests.cs
├── Endpoints/
│   ├── CustomWebApplicationFactory.cs          # InMemory EF + no background services
│   ├── FixedDepositEndpointsTests.cs
│   └── PortfolioEndpointsTests.cs
└── Infrastructure/PriceFeeds/
    ├── MockHttpMessageHandler.cs
    ├── CoinGeckoPriceProviderTests.cs
    ├── OpenErApiProviderTests.cs
    └── VNDirectPriceProviderTests.cs
```

### Test Stack

| Tool | Purpose |
|---|---|
| xUnit 2.9.3 | Test framework |
| FluentAssertions 7.0.0 | Readable assertions |
| NSubstitute 5.3.0 | Mocking |
| Snapper 2.4.1 | Snapshot testing (golden file comparison) |
| Testcontainers.PostgreSql 4.10.0 | Real PostgreSQL in Docker for integration tests |
| Microsoft.AspNetCore.Mvc.Testing | WebApplicationFactory for endpoint tests |
| Microsoft.EntityFrameworkCore.InMemory | In-memory DB for endpoint tests |

### CI/CD

GitHub Actions (`.github/workflows/test.yml`) runs on every push and pull request:
- Ubuntu runner with .NET 10.0
- `dotnet restore` → `dotnet build --configuration Release` → `dotnet test` with TRX reporting
- Test results uploaded as artifacts and rendered via `dorny/test-reporter`

## Available Commands

| Command | Description |
|---|---|
| `dotnet build TradingBot.slnx` | Build the entire solution |
| `cd TradingBot.AppHost && dotnet run` | Start all services via Aspire |
| `cd TradingBot.ApiService && dotnet run` | Run API service only |
| `cd TradingBot.Dashboard && npm run dev` | Run web dashboard in dev mode |
| `cd TradingBot.Mobile && flutter run` | Run Flutter mobile app |
| `dotnet test` | Run all 103 tests |
| `dotnet test --filter "FullyQualifiedName~<TestClass>"` | Run specific test class |
| `cd TradingBot.ApiService && dotnet ef migrations add <Name>` | Create EF Core migration |
| `cd TradingBot.ApiService && dotnet user-secrets list` | List configured secrets |
| `cd TradingBot.ApiService && dotnet user-secrets set "<Key>" "<Value>"` | Set a secret |

## Troubleshooting

### Database Connection Issues

**Error:** `Npgsql.NpgsqlException: Failed to connect`

**Solution:**
1. Ensure Docker Desktop is running
2. Run via Aspire (`cd TradingBot.AppHost && dotnet run`) — it starts PostgreSQL automatically
3. If running manually, verify PostgreSQL is accessible on port 5432

### Dapr Sidecar Not Running

**Error:** Events not being published, Telegram/FCM notifications not sending

**Solution:** Run via Aspire — it manages the Dapr sidecar automatically. If running without Aspire, ensure Dapr CLI is installed and the sidecar is started with the correct pub-sub component configuration.

### Hyperliquid API Errors

**Error:** `401 Unauthorized` or signing failures

**Solution:**
1. Verify your private key is set: `cd TradingBot.ApiService && dotnet user-secrets list`
2. Check `IsTestnet` matches your key (testnet keys don't work on mainnet and vice versa)
3. Ensure wallet address matches the private key

### Dashboard/Mobile Not Authenticating

**Error:** `401 Unauthorized` or `403 Forbidden` from API

**Solution:**
1. Ensure `Dashboard:ApiKey` is set in the API service user secrets
2. For Aspire: the `dashboardApiKey` parameter must be configured (set via Aspire dashboard or environment)
3. For mobile: pass the correct API key via `--dart-define=API_KEY=<key>`
4. For web dashboard: set `NUXT_API_KEY` environment variable

### EF Core Migrations

**Error:** `The entity type 'X' was not found` or schema mismatch

**Solution:**
```bash
cd TradingBot.ApiService
dotnet ef migrations add <MigrationName>
```

Migrations run automatically on startup (`MigrateAsync` in Program.cs), but you may need to create new ones after model changes.

### Flutter Build Issues

**Error:** Build failures or dependency errors

**Solution:**
```bash
cd TradingBot.Mobile
flutter clean
flutter pub get
# If using code generation (Riverpod):
dart run build_runner build --delete-conflicting-outputs
```

### Missing Notifications

**Error:** Telegram or push notifications not arriving

**Solution:**
1. Verify Telegram secrets are set (`BotToken` and `ChatId`)
2. For FCM: ensure Firebase is configured and the device has registered its token via `POST /api/devices/register`
3. Check that the Dapr sidecar is running (events flow through Dapr pub-sub)
4. Check the `dead_letter_messages` table for failed event deliveries

## Development Roadmap

| Milestone | Description | Phases | Status |
|---|---|---|---|
| **v1.0** | Daily BTC Smart DCA | 1–4 | Shipped |
| **v1.1** | Backtesting Engine | 5–8 | Shipped |
| **v1.2** | Web Dashboard | 9–12 | Shipped |
| **v2.0** | DDD Foundation (Vogen, ErrorOr, Specs) | 13–19 | Shipped |
| **v3.0** | Flutter Mobile App + FCM | 20–25 | Shipped |
| **v4.0** | Multi-Asset Portfolio Tracker | 26–32 | Shipped |
| **v5.0** | Stunning Mobile UI (Glassmorphism) | 33–38 | In Progress |

**Codebase Stats:** ~12,000+ lines C#, ~5,500+ lines Dart, ~4,100 lines TypeScript/Vue/CSS, 103 automated tests.

Detailed architecture and planning documents are in `.planning/`.

## License

Private — All rights reserved
