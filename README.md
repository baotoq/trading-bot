# BTC Smart DCA Bot

A smart Dollar-Cost-Averaging bot that automatically accumulates Bitcoin on the Hyperliquid spot market with intelligent buying logic. Increases position size during price dips using configurable multiplier tiers, boosts buying in bear markets using the 200-day moving average, and includes a full backtesting engine for strategy validation.

## Table of Contents

- [Key Features](#key-features)
- [Tech Stack](#tech-stack)
- [Prerequisites](#prerequisites)
- [Getting Started](#getting-started)
- [Configuration](#configuration)
- [Architecture](#architecture)
- [API Endpoints](#api-endpoints)
- [Dashboard](#dashboard)
- [Backtesting Engine](#backtesting-engine)
- [Testing](#testing)
- [Available Commands](#available-commands)
- [Troubleshooting](#troubleshooting)
- [Development Roadmap](#development-roadmap)
- [License](#license)

## Key Features

- **Daily Fixed-Amount Purchases** - Executes scheduled USD purchases of BTC on Hyperliquid spot market
- **Smart Multipliers** - Increases position size during price dips based on configurable tiers (1.5x at 5% drop, 2x at 10%, 3x at 20%)
- **30-Day High Tracking** - Calculates drop-from-high percentages for multiplier decisions
- **Bear Market Boost** - Additive +1.5x boost when BTC is below its 200-day moving average
- **Backtesting Engine** - Day-by-day simulation comparing smart DCA vs fixed DCA with parameter sweeps and walk-forward validation
- **Historical Data Pipeline** - CoinGecko integration with incremental ingestion and gap detection
- **Web Dashboard** - Nuxt 4 frontend with portfolio overview, purchase history, price charts, and live bot status
- **Telegram Notifications** - Alerts on each purchase with multiplier reasoning and weekly summaries
- **Dry-Run Mode** - Simulate purchases without placing real orders
- **Health Monitoring** - Missed purchase detection, health check endpoint, and bot status tracking

## Tech Stack

| Category | Technology |
|----------|-----------|
| Runtime | .NET 10.0 / C# |
| Framework | ASP.NET Core Minimal APIs |
| Database | PostgreSQL 16 (EF Core) |
| Cache | Redis (StackExchange.Redis) |
| Message Bus | Dapr pub/sub |
| Orchestration | .NET Aspire |
| Crypto Signing | Nethereum (EIP-712) |
| Price Data | Binance.Net (prices), CoinGecko (historical) |
| Notifications | Telegram.Bot |
| Logging | Serilog (structured, expression templates) |
| Serialization | MessagePack (Redis caching) |
| Frontend | Nuxt 4, Vue 3, @nuxt/ui v4, Chart.js |

## Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for PostgreSQL, Redis, Dapr via Aspire)
- [Node.js 20+](https://nodejs.org/) (for the dashboard)

## Getting Started

### 1. Clone the Repository

```bash
git clone <repository-url>
cd trading-bot
```

### 2. Configure Secrets

Store your private keys and API tokens using .NET User Secrets:

```bash
cd TradingBot.ApiService

# Required: Hyperliquid private key for signing orders
dotnet user-secrets set "Hyperliquid:PrivateKey" "<your-private-key>"

# Required: Hyperliquid wallet address
dotnet user-secrets set "Hyperliquid:WalletAddress" "<your-wallet-address>"

# Optional: Telegram bot for notifications
dotnet user-secrets set "Telegram:BotToken" "<your-bot-token>"
dotnet user-secrets set "Telegram:ChatId" "<your-chat-id>"

# Optional: CoinGecko API key for historical data (free tier works)
dotnet user-secrets set "CoinGecko:ApiKey" "<your-api-key>"

# Required for dashboard: API key for dashboard-to-backend auth
dotnet user-secrets set "Dashboard:ApiKey" "<any-secret-string>"
```

### 3. Install Dashboard Dependencies

```bash
cd TradingBot.Dashboard
npm install
```

### 4. Run with Aspire (Recommended)

```bash
cd TradingBot.AppHost
dotnet run
```

This automatically starts:

| Service | Port | Description |
|---------|------|-------------|
| API Service | 5000 | Main .NET API |
| Dashboard | 3000 | Nuxt 4 frontend |
| PostgreSQL | 5432 | Primary database |
| Redis | 6379 | Distributed cache |
| PgAdmin | 5050 | Database management UI |
| RedisInsight | 5051 | Redis management UI |
| Dapr Sidecar | — | Pub/sub messaging |

Open the Aspire dashboard to see all services, health checks, and logs.

### 5. Run Without Aspire

If you prefer to manage infrastructure manually, ensure PostgreSQL and Redis are running, then:

```bash
cd TradingBot.ApiService
dotnet run
```

You'll need to set connection strings via environment variables or `appsettings.Development.json`.

## Configuration

Edit `TradingBot.ApiService/appsettings.json` to configure the DCA strategy:

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
      { "DropPercentage": 5, "Multiplier": 1.5 },
      { "DropPercentage": 10, "Multiplier": 2.0 },
      { "DropPercentage": 20, "Multiplier": 3.0 }
    ]
  },
  "Hyperliquid": {
    "IsTestnet": true,
    "WalletAddress": ""
  }
}
```

### Configuration Reference

| Parameter | Description | Default |
|-----------|-------------|---------|
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

## Architecture

### Project Structure

```
trading-bot/
├── TradingBot.ApiService/           # Main API service
│   ├── Application/
│   │   ├── BackgroundJobs/          # Scheduled services
│   │   │   ├── DcaSchedulerBackgroundService    # Daily buy execution
│   │   │   ├── PriceDataRefreshService          # Daily price data refresh
│   │   │   ├── WeeklySummaryService             # Sunday Telegram summary
│   │   │   └── MissedPurchaseVerificationService # Purchase monitoring
│   │   ├── Events/                  # Domain events (MediatR)
│   │   ├── Handlers/               # Event handlers (Telegram notifications)
│   │   ├── Health/                  # Health check implementations
│   │   └── Services/
│   │       ├── Backtest/            # Backtesting engine
│   │       │   ├── BacktestSimulator            # Day-by-day simulation
│   │       │   ├── ParameterSweepService        # Grid search optimization
│   │       │   ├── WalkForwardValidator         # Overfitting detection
│   │       │   └── SweepPresets                 # Pre-built parameter sets
│   │       ├── HistoricalData/      # CoinGecko data pipeline
│   │       │   ├── DataIngestionService         # Fetch & store prices
│   │       │   ├── GapDetectionService          # Find missing dates
│   │       │   └── IngestionJobQueue            # Bounded channel queue
│   │       ├── DcaExecutionService              # Core buy logic
│   │       ├── MultiplierCalculator             # Pure static multiplier math
│   │       └── PriceDataService                 # Price fetching & caching
│   ├── BuildingBlocks/              # Reusable infrastructure
│   │   ├── Pubsub/                  # Event publishing
│   │   │   ├── Outbox/             # Transactional outbox pattern
│   │   │   └── Dapr/              # Dapr pub/sub integration
│   │   ├── DistributedLocks/       # PostgreSQL advisory locks
│   │   ├── BaseEntity.cs           # UUIDv7 base entity
│   │   ├── AuditedEntity.cs        # CreatedAt/UpdatedAt tracking
│   │   ├── IDomainEvent.cs         # Domain event interface
│   │   └── TimeBackgroundService.cs # Scheduled service base class
│   ├── Configuration/              # Strongly-typed options
│   │   ├── DcaOptions.cs           # DCA strategy configuration
│   │   ├── HyperliquidOptions.cs   # Exchange API settings
│   │   └── TelegramOptions.cs      # Notification settings
│   ├── Endpoints/                  # Minimal API endpoints
│   │   ├── BacktestEndpoints.cs    # Backtest & sweep APIs
│   │   ├── DashboardEndpoints.cs   # Portfolio, purchases, status
│   │   └── DataEndpoints.cs        # Historical data ingestion
│   ├── Infrastructure/
│   │   ├── Data/                   # EF Core DbContext & migrations
│   │   ├── Hyperliquid/           # Exchange client & EIP-712 signing
│   │   ├── CoinGecko/            # Historical price data client
│   │   ├── Locking/              # Distributed lock wrappers
│   │   └── Telegram/             # Telegram bot service
│   └── Models/                    # Domain entities
│       ├── Purchase.cs            # Buy order records
│       ├── DailyPrice.cs          # Historical OHLCV data
│       └── IngestionJob.cs        # Data ingestion tracking
├── TradingBot.AppHost/             # Aspire orchestration
│   └── AppHost.cs                 # PostgreSQL, Redis, Dapr, Dashboard
├── TradingBot.Dashboard/           # Nuxt 4 web dashboard
│   ├── app/
│   │   ├── components/dashboard/  # Vue components
│   │   ├── composables/           # Vue composables (API calls, state)
│   │   ├── plugins/               # Chart.js plugin
│   │   └── types/                 # TypeScript types
│   └── server/
│       ├── api/dashboard/         # Server proxy routes
│       └── utils/                 # Auth helpers
├── TradingBot.ServiceDefaults/     # Shared service configuration
├── tests/
│   └── TradingBot.ApiService.Tests/
│       └── Application/Services/  # Unit tests with snapshot testing
└── .planning/                      # Architecture & planning docs
```

### Event Flow

```
Purchase Execution
    ↓
Domain Event (IDomainEvent)
    ↓
OutboxMessage (saved to DB in same transaction)
    ↓
OutboxMessageProcessor (background service polls DB)
    ↓
DaprMessageBroker (publishes to Dapr pub/sub)
    ↓
Dapr sidecar (routes to subscriber)
    ↓
HTTP endpoint (Dapr subscription)
    ↓
MediatR handler (sends Telegram notification)
```

### Database Schema

```
purchases
├── id (uuid, PK, UUIDv7)
├── executed_at (timestamptz, indexed)
├── price (decimal 18,8)
├── quantity (decimal 18,8)
├── cost (decimal 18,2)
├── multiplier (decimal 4,2)
├── status (varchar 20) — Pending | Filled | PartiallyFilled | Failed | Cancelled
├── is_dry_run (boolean, default false)
├── order_id (varchar 100)
├── raw_response (text)
├── failure_reason (varchar 500)
├── multiplier_tier (varchar 50)
├── drop_percentage (decimal 8,4)
├── high_30_day (decimal 18,8)
├── ma_200_day (decimal 18,8)
├── created_at (timestamptz)
└── updated_at (timestamptz)

daily_prices (composite PK: date + symbol)
├── date (date, indexed)
├── symbol (varchar 20)
├── open (decimal 18,8)
├── high (decimal 18,8)
├── low (decimal 18,8)
├── close (decimal 18,8)
└── volume (decimal 18,8)

ingestion_jobs
├── id (uuid, PK)
├── status (varchar 30, indexed) — Pending | Running | Completed | CompletedWithGaps | Failed
├── created_at (timestamptz, indexed)
├── started_at (timestamptz)
├── completed_at (timestamptz)
├── start_date (date)
├── end_date (date)
├── force (boolean)
├── records_fetched (int)
├── gaps_detected (int)
└── error_message (varchar 2000)
```

## API Endpoints

### Dashboard (requires `x-api-key` header)

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/dashboard/portfolio` | Portfolio overview (total BTC, cost basis, P&L) |
| GET | `/api/dashboard/purchases` | Paginated purchase history with cursor-based pagination |
| GET | `/api/dashboard/status` | Bot health, next buy time, last purchase info |
| GET | `/api/dashboard/chart` | Price chart data with purchase markers and cost basis line |

**Purchase History Query Params:** `cursor`, `pageSize`, `startDate`, `endDate`, `tier`

**Chart Query Params:** `timeframe` (7D, 1M, 3M, 6M, 1Y, All)

### Backtesting

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/backtest` | Run single backtest with config overrides |
| POST | `/api/backtest/sweep` | Parameter sweep with ranking and walk-forward validation |
| GET | `/api/backtest/presets/{name}` | Inspect a sweep preset |

### Historical Data

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/backtest/data/ingest` | Trigger CoinGecko data ingestion (4 years) |
| GET | `/api/backtest/data/status` | Check data coverage, gaps, freshness |
| GET | `/api/backtest/data/ingest/{jobId}` | Poll ingestion job progress |

### Infrastructure

| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | Service health check message |
| GET | `/health` | ASP.NET Core health check endpoint |

## Dashboard

The web dashboard is a Nuxt 4 application that provides real-time monitoring of the bot.

**Components:**
- **Portfolio Stats** - Total BTC, total cost, average cost basis, current price, unrealized P&L
- **Price Chart** - BTC price history with purchase markers overlaid and average cost basis line
- **Purchase History** - Paginated list with date, price, amount, multiplier tier, and drop percentage
- **Live Status** - Bot health indicator, next buy countdown timer, last purchase details

**Architecture:**
- Nuxt server routes proxy API calls to the .NET backend with `x-api-key` header
- Browser calls Nuxt server (no API key needed), Nuxt server calls .NET API (with API key)
- Auto-refresh with 10-second polling interval for portfolio and status data
- Vue composables manage API state (`useDashboard`, `usePurchaseHistory`, `useCountdownTimer`)

## Backtesting Engine

The backtesting engine simulates the smart DCA strategy against historical price data.

**Capabilities:**
- Day-by-day simulation matching the live bot's `MultiplierCalculator` logic
- Compares smart DCA vs same-base fixed DCA and match-total fixed DCA
- Metrics: cost basis, total BTC, max drawdown, return %, efficiency ratio, tier breakdown
- Parameter sweep with cartesian product grid search and ranked results
- Walk-forward validation with 70/30 train/test split for overfitting detection
- Pre-built sweep presets (conservative, aggressive, etc.)

**Example: Run a Backtest**

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

**Example: Ingest Historical Data**

```bash
# Start data ingestion (fetches 4 years from CoinGecko)
curl -X POST http://localhost:5000/api/backtest/data/ingest

# Check data coverage
curl http://localhost:5000/api/backtest/data/status
```

## Testing

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~MultiplierCalculatorTests"

# Run specific test class
dotnet test --filter "FullyQualifiedName~BacktestSimulatorTests"
```

### Test Structure

```
tests/TradingBot.ApiService.Tests/
├── Application/
│   └── Services/
│       ├── MultiplierCalculatorTests.cs    # 24 tests — multiplier logic
│       ├── BacktestSimulatorTests.cs       # 28 tests — simulation engine
│       └── _snapshots/                     # Golden snapshot files
└── Tests.cs                                # Integration tests
```

Tests use xUnit with FluentAssertions and snapshot testing for deterministic backtest validation.

## Available Commands

| Command | Description |
|---------|-------------|
| `dotnet build TradingBot.sln` | Build the entire solution |
| `cd TradingBot.AppHost && dotnet run` | Start all services via Aspire |
| `cd TradingBot.ApiService && dotnet run` | Run API service only |
| `cd TradingBot.Dashboard && npm run dev` | Run dashboard in dev mode |
| `dotnet test` | Run all tests |
| `cd TradingBot.ApiService && dotnet ef migrations add <Name>` | Create EF Core migration |
| `cd TradingBot.ApiService && dotnet user-secrets list` | List configured secrets |

## Troubleshooting

### Database Connection Issues

**Error:** `Npgsql.NpgsqlException: Failed to connect`

**Solution:**
1. Ensure Docker Desktop is running
2. Run via Aspire (`cd TradingBot.AppHost && dotnet run`) — it starts PostgreSQL automatically
3. If running manually, verify PostgreSQL is accessible on port 5432

### Dapr Sidecar Not Running

**Error:** Events not being published, Telegram notifications not sending

**Solution:** Run via Aspire — it manages the Dapr sidecar automatically. If running without Aspire, ensure Dapr is installed and the sidecar is started.

### Hyperliquid API Errors

**Error:** `401 Unauthorized` or signing failures

**Solution:**
1. Verify your private key is set: `dotnet user-secrets list`
2. Check `IsTestnet` matches your key (testnet keys don't work on mainnet)
3. Ensure wallet address matches the private key

### Dashboard Not Loading

**Error:** Dashboard shows connection errors or blank page

**Solution:**
1. Ensure the API service is running and healthy
2. Check that `Dashboard:ApiKey` is configured in both the API service and Aspire parameters
3. Verify npm dependencies are installed: `cd TradingBot.Dashboard && npm install`

### EF Core Migrations

**Error:** `The entity type 'X' was not found` or schema mismatch

**Solution:**
```bash
cd TradingBot.ApiService
dotnet ef migrations add <MigrationName>
```

Migrations run automatically on startup, but you may need to create new ones after model changes.

## Development Roadmap

| Milestone | Phases | Status |
|-----------|--------|--------|
| v1.0 — Daily BTC Smart DCA | Foundation, DCA Engine, Smart Multipliers, Notifications | Shipped |
| v1.1 — Backtesting Engine | MultiplierCalculator extraction, Simulation, Data Pipeline, Parameter Sweep | Shipped |
| v1.2 — Web Dashboard | Aspire Integration, Dashboard Core, Backtest Visualization, Config Management | In Progress |

Detailed architecture and planning documents are in `.planning/`.

## License

Private — All rights reserved
