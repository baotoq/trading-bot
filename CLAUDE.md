# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with this repository.

## Project Overview

BTC Smart DCA Bot - A .NET 10.0 application that automatically accumulates Bitcoin on Hyperliquid spot market using intelligent dollar-cost-averaging with multipliers based on price drops and market conditions.

## Build & Run Commands

```bash
# Build the solution
dotnet build TradingBot.slnx

# Run with Aspire (starts PostgreSQL, Redis, Dapr, Dashboard automatically)
cd TradingBot.AppHost && dotnet run

# Run API service directly (requires external dependencies)
cd TradingBot.ApiService && dotnet run

# Run dashboard (Nuxt 4) independently
cd TradingBot.Dashboard && npm install && npm run dev

# Run tests
dotnet test

# Add EF Core migration (must run from ApiService directory)
cd TradingBot.ApiService && dotnet ef migrations add <MigrationName>
```

## Architecture

**Layered Structure:**
- `Application/` - Business logic (background jobs, services, event handlers, health checks)
- `BuildingBlocks/` - Reusable infrastructure (pub-sub, outbox, distributed locks, base entities)
- `Configuration/` - Strongly-typed options classes bound from appsettings.json
- `Endpoints/` - Minimal API endpoint definitions (backtest, data, dashboard)
- `Infrastructure/` - Database context, Hyperliquid client, external integrations
- `Models/` - Domain entities with domain events

**Projects:**
- `TradingBot.ApiService` - Main API service (.NET 10.0)
- `TradingBot.AppHost` - Aspire orchestration (PostgreSQL, Redis, Dapr, Dashboard)
- `TradingBot.ServiceDefaults` - Shared service configuration extensions
- `TradingBot.Dashboard` - Nuxt 4 web dashboard (Vue 3, @nuxt/ui v4, Chart.js)

**Key Patterns:**
- Event-driven architecture with MediatR (in-process) and Dapr (cross-service)
- Transactional outbox pattern for reliable event publishing
- PostgreSQL advisory locks for distributed locking
- EIP-712 signing for Hyperliquid API authentication

**Event Flow:**
Domain Event → OutboxMessage (DB) → OutboxMessageProcessor → DaprMessageBroker → Dapr pub-sub → HTTP endpoint → MediatR handler

## Code Conventions

**Naming:**
- Entities: PascalCase, singular nouns (`Purchase`, `DailyPrice`, `IngestionJob`)
- Options classes: Suffix with `Options` (`DcaOptions`, `HyperliquidOptions`)
- Interfaces: Prefix with `I` (`IHyperliquidClient`, `IMessageBroker`)
- Background services: Suffix with `Service` or `BackgroundService`

**Entity Design:**
- Inherit from `AuditedEntity` for automatic CreatedAt/UpdatedAt
- Use UUIDv7 for IDs (via `BaseEntity`)
- Domain events via `IDomainEvent` interface
- Configuration validated at startup with `IValidateOptions<T>` (see `DcaOptionsValidator`)

**Error Handling:**
- Exceptions for unexpected/infrastructure failures
- Graceful degradation in background services (failures logged, don't crash)
- Structured logging with Serilog (include correlation IDs)

**Logging Templates:**
```csharp
// Good - structured with named placeholders
_logger.LogInformation("Placing order for {Symbol} at {Price}", symbol, price);

// Bad - string interpolation
_logger.LogInformation($"Placing order for {symbol} at {price}");
```

## Key Dependencies

- **Binance.Net** - Price data source (not for trading)
- **CoinGecko** (direct HttpClient) - Historical OHLCV data for backtesting
- **Telegram.Bot** - Notifications and weekly summaries
- **Nethereum** - EIP-712 signing for Hyperliquid authentication
- **Dapr** - Cross-service pub-sub messaging
- **MediatR** - In-process domain event handling
- **EFCore.BulkExtensions** - Bulk upsert for historical data ingestion
- **MessagePack** - Binary serialization for Redis caching
- **Snapper** - Snapshot testing for deterministic backtest validation

## Key Files

- `TradingBot.ApiService/Program.cs` - Startup, DI configuration
- `TradingBot.ApiService/Infrastructure/Data/TradingBotDbContext.cs` - EF Core context
- `TradingBot.ApiService/Infrastructure/Hyperliquid/HyperliquidClient.cs` - Exchange API client
- `TradingBot.ApiService/Infrastructure/Hyperliquid/HyperliquidSigner.cs` - EIP-712 signing
- `TradingBot.ApiService/Application/Services/MultiplierCalculator.cs` - Pure static multiplier logic (reused by backtest)
- `TradingBot.ApiService/Application/Services/BacktestSimulator.cs` - Day-by-day DCA simulation engine
- `TradingBot.ApiService/Endpoints/DashboardEndpoints.cs` - Dashboard API (portfolio, purchases, status, chart)
- `TradingBot.ApiService/appsettings.json` - Configuration (DCA params, Hyperliquid settings)
- `TradingBot.AppHost/AppHost.cs` - Aspire orchestration (Docker containers)
- `TradingBot.Dashboard/nuxt.config.ts` - Nuxt 4 dashboard configuration

## Configuration

**Secrets** - Use .NET User Secrets (run from `TradingBot.ApiService/`):
```bash
dotnet user-secrets set "Hyperliquid:PrivateKey" "<key>"
dotnet user-secrets set "Hyperliquid:WalletAddress" "<address>"
dotnet user-secrets set "Telegram:BotToken" "<token>"
dotnet user-secrets set "Telegram:ChatId" "<chat-id>"
dotnet user-secrets set "CoinGecko:ApiKey" "<key>"        # optional, free tier works
dotnet user-secrets set "Dashboard:ApiKey" "<any-secret>"  # required for dashboard auth
```

**Options Classes:**
- `DcaOptions` - Daily amount, schedule, multiplier tiers, bear market settings (validated via `DcaOptionsValidator`)
- `HyperliquidOptions` - API URL (auto-derived from IsTestnet), wallet address
- `TelegramOptions` - Bot token, chat ID
- `CoinGeckoOptions` - API key for historical data

## Testing

- xUnit for test framework
- FluentAssertions for assertions
- Snapper for snapshot testing (golden file comparison)
- NSubstitute for mocking
- Tests in `tests/TradingBot.ApiService.Tests/`
- 53 tests: 24 MultiplierCalculator, 28 BacktestSimulator, 1 integration
- CI: GitHub Actions (`.github/workflows/test.yml`) — runs on push and PR

```bash
# Run all tests
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~MultiplierCalculatorTests"
```

## Planning Documents

Detailed architecture and requirements are in `.planning/`:
- `PROJECT.md` - Vision and scope
- `REQUIREMENTS.md` - Functional requirements
- `ROADMAP.md` - Phase breakdown
- `STATE.md` - Current execution state
- `MILESTONES.md` - Milestone tracking
- `codebase/ARCHITECTURE.md` - Detailed architecture
- `codebase/CONVENTIONS.md` - Full coding standards

## Gotchas

- Dapr sidecar must be running for pub-sub events to work (Aspire handles this automatically)
- Hyperliquid testnet and mainnet use different API URLs — controlled via `HyperliquidOptions:IsTestnet`
- EF migrations must be run from `TradingBot.ApiService/` directory
- EF migrations auto-run on startup (`dbContext.Database.MigrateAsync()` in Program.cs)
- Advisory locks use PostgreSQL connection — if connection drops, lock is released automatically
- Dashboard endpoints require `x-api-key` header — `Dashboard:ApiKey` must be set in both API service secrets and Aspire parameters (`dashboardApiKey`)
- Dashboard uses `/proxy/api/**` route prefix to avoid Nuxt server API routing conflicts (`/api/**` is reserved by Nuxt)

## Other Notes

- Always use Context7 MCP when I need library/API documentation, code generation, setup or configuration steps without me having to explicitly ask.
