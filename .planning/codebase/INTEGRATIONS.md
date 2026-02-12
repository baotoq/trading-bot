# External Integrations

**Analysis Date:** 2026-02-12

## APIs & External Services

**Cryptocurrency Exchange:**
- Binance - Spot and Futures trading
  - SDK/Client: `Binance.Net` 11.11.0
  - Supports: Account info, market data, order execution, WebSocket real-time candles
  - Integration: `TradingBot.ApiService/Application/Services/BinanceService.cs` (per CLAUDE.md)
  - Used for: Trading signal execution, market analysis, position management

**Messaging & Notifications:**
- Telegram - Trading alerts and notifications
  - SDK/Client: `Telegram.Bot` 22.1.0
  - Integration: `TradingBot.ApiService/Application/Services/TelegramNotificationService.cs` (per CLAUDE.md)
  - Used for: Signal notifications, position opened/closed alerts, risk warnings, trade confirmations

## Data Storage

**Databases:**
- PostgreSQL (primary relational database)
  - Provider: `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL` 13.0.1
  - Connection: Aspire-managed via AppHost, database named `tradingbotdb`
  - Client: Entity Framework Core 10.0 with DbContext
  - Tables (per CLAUDE.md): Position, TradeLog, Candle, OutboxMessages
  - Used for: Trade history, position tracking, indicator values, domain event outbox

**Caching:**
- Redis (distributed cache and Dapr pub/sub backend)
  - Client: StackExchange.Redis via `Microsoft.Extensions.Caching.StackExchangeRedis` 10.0.0
  - Aspire Provider: `Aspire.StackExchange.Redis.DistributedCaching` 13.0.2
  - Connection: Aspire-managed, persistent volume in Docker
  - Used for: Distributed caching, session state, real-time candle buffer

**File Storage:**
- Local filesystem only - No external blob storage
- Trade logs and historical data stored in PostgreSQL

## Authentication & Identity

**Auth Provider:**
- None (API is open) - No authentication middleware detected
- Binance API: Uses API key/secret for exchange authentication
  - Credentials passed to Binance.Net client for account access
  - Telegram: Uses bot token for authentication

**Configuration Location:**
- Binance credentials: Likely in `appsettings.json` or User Secrets
- Telegram bot token: Configured via `Telegram` section in settings
  - Environment variables: `Telegram:BotToken`, `Telegram:ChatId`

## Monitoring & Observability

**Distributed Tracing & Metrics:**
- OpenTelemetry 1.13.x
  - Exporters: OTLP protocol (`OpenTelemetry.Exporter.OpenTelemetryProtocol` 1.13.1)
  - Activation: OTEL_EXPORTER_OTLP_ENDPOINT environment variable
  - Instrumentation: AspNetCore, HTTP client, runtime metrics
  - Used for: Performance monitoring, request tracing, resource utilization

**Logs:**
- Serilog 10.0.0 - Structured logging framework
- Output: Console with template formatting (trace/span IDs included)
- Configuration: `appsettings.json` with log level overrides for AspNetCore components
- Used for: Application events, trading activity, error tracking

**Error Tracking:**
- Console via Serilog - No external error tracking service (Sentry, etc.)
- Fatal errors logged during bootstrap

## CI/CD & Deployment

**Hosting:**
- Docker containers via .NET Aspire
- Platform: AppHost orchestrates PostgreSQL, Redis, and API service

**CI Pipeline:**
- GitHub Actions (`.github/workflows/Run Tests.yml`)
- Workflow:
  1. Checkout code on push/pull request
  2. Setup .NET 10.0.x SDK
  3. Restore dependencies
  4. Build solution (Release configuration)
  5. Run tests with xUnit/MsTest
  6. Upload test results as artifacts
  7. Test report via dorny/test-reporter

**Build Output:**
- Artifacts: Test results in TRX format

## Environment Configuration

**Required Environment Variables:**
- `OTEL_EXPORTER_OTLP_ENDPOINT` - Optional, enables OpenTelemetry OTLP export
- `TZ` - Set to UTC for consistent timezone handling
- Binance API credentials (API key, secret) - Configured in appsettings
- Telegram bot credentials:
  - `Telegram:BotToken` - Bot authentication token
  - `Telegram:ChatId` - Target chat for notifications

**Secrets Location:**
- User Secrets: Development environment
  - ApiService: `073debf5-1921-4c8f-8b64-bdf2d3063075`
  - AppHost: `d1b993d5-ae8d-43ff-821d-86492bb0b53f`
- Environment variables: Production deployment
- Configuration files: `appsettings.json` (non-secrets only)

## Event-Driven Architecture

**Pub/Sub Infrastructure:**
- Dapr 1.16.1 - Message broker abstraction
  - Backend: Redis via Dapr pub/sub component
  - Used for: Domain event publishing and subscription
  - Outbox Pattern: EF Core-based transactional outbox in `TradingBot.ApiService/BuildingBlocks/Pubsub/Outbox/` (per CLAUDE.md)

**Domain Events (per CLAUDE.md):**
- Signal Events: `TradingSignalGeneratedDomainEvent`
- Position Events: `PositionOpenedDomainEvent`, `PositionClosedDomainEvent`, `PositionPartiallyFilledDomainEvent`
- Order Events: `OrderFilledDomainEvent`, `OrderFailedDomainEvent`
- Risk Events: `RiskViolationDetectedDomainEvent`, `TradeRejectedDomainEvent`
- Candle Events: `CandleClosedDomainEvent`, `CandleCapturedDomainEvent`

**Event Handlers:**
- Each domain event has dedicated MediatR handlers
- Handlers trigger:
  - Telegram notifications
  - Database record updates
  - Follow-up business logic
  - Event logging

## Distributed Locking

**Implementation:**
- Dapr DistributedLock 1.16.1
- Backend: Redis (via Dapr component)
- Used for: Concurrent position management and order execution synchronization

**Service Integration:**
- `TradingBot.ApiService/BuildingBlocks/DistributedLocks/` provides `IDistributedLock` interface
- Prevents race conditions during multi-threaded trading operations

## Webhooks & Callbacks

**Incoming Webhooks:**
- None detected - No webhook endpoints in configuration

**Outgoing:**
- Telegram - Notifications sent to configured chat
- Binance WebSocket - Real-time candle data via WebSocket (not traditional webhook)
  - Integration: `TradingBot.ApiService/Services/RealtimeCandleService.cs` (per CLAUDE.md)
  - Used for: Live 4h candle monitoring for spot strategies

## Data Flow Integration

**Real-time Trading Flow:**
1. Binance WebSocket → RealtimeCandleService → `CandleClosedDomainEvent`
2. Domain Event → Strategy Analysis (MediatR)
3. TradingSignalGeneratedDomainEvent → Risk Validation (RiskManagementService)
4. Position Calculation → Binance API (order execution)
5. Order Execution → PositionOpenedDomainEvent
6. Event Handler → Telegram Notification + Database Logging

**Backtest/Analysis Flow:**
1. Historical candles from PostgreSQL → TechnicalIndicatorService
2. Indicator values → Strategy.AnalyzeAsync()
3. TradingSignal → PositionCalculatorService
4. Position details → RiskManagementService validation

---

*Integration audit: 2026-02-12*
