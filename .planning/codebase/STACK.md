# Technology Stack

**Analysis Date:** 2026-02-12

## Languages

**Primary:**
- C# - Full codebase for trading bot API service and infrastructure

## Runtime

**Environment:**
- .NET 10.0 - Cross-platform runtime for both ApiService and AppHost

**Package Manager:**
- NuGet - .NET standard package manager
- Lockfile: `packages.lock.json` (managed by Visual Studio/dotnet CLI)

## Frameworks

**Core:**
- ASP.NET Core 10.0 - Web API framework
- Entity Framework Core 10.0 - ORM for database access (`Microsoft.EntityFrameworkCore.Design`)

**Distributed Systems:**
- Dapr 1.16.1 - Distributed application runtime for pub/sub and distributed locks
  - `Dapr.AspNetCore` 1.16.1 - ASP.NET integration
  - `Dapr.DistributedLock` 1.16.1 - Distributed locking support

**Event/Message Processing:**
- MediatR 13.1.0 - In-process event dispatcher for domain events and command handling

**Cryptography/Trading:**
- Binance.Net 11.11.0 - Binance API client for futures and spot trading
- CryptoExchange.Net 9.13.0 - Base SDK for cryptocurrency exchange integration

**Logging & Diagnostics:**
- Serilog.AspNetCore 10.0.0 - Structured logging framework
- Serilog.Expressions 5.0.0 - Expression templates for log formatting
- OpenTelemetry 1.13.x - Distributed tracing and metrics collection
  - OpenTelemetry.Exporter.OpenTelemetryProtocol 1.13.1
  - OpenTelemetry.Extensions.Hosting 1.13.1
  - OpenTelemetry.Instrumentation.AspNetCore 1.13.0
  - OpenTelemetry.Instrumentation.Http 1.13.0
  - OpenTelemetry.Instrumentation.Runtime 1.13.0

**Notifications:**
- Telegram.Bot 22.1.0 - Telegram bot API client for trading alerts

**API Documentation:**
- Microsoft.AspNetCore.OpenApi 10.0.0 - OpenAPI/Swagger support

## Key Dependencies

**Critical:**
- Binance.Net 11.11.0 - Why it matters: Core trading functionality depends on Binance Futures and Spot market data, order execution, and account info
- Dapr 1.16.1 - Why it matters: Provides event pub/sub infrastructure and distributed locking for concurrent position management
- Entity Framework Core 10.0 - Why it matters: Database persistence layer for positions, trades, and candle history
- MediatR 13.1.0 - Why it matters: Decoupled event handling for trading signals and position lifecycle events

**Infrastructure:**
- StackExchange.Redis (via Aspire) 13.0.2 - Distributed caching and Dapr pub/sub backend
- Npgsql EntityFrameworkCore.PostgreSQL (via Aspire) 13.0.1 - PostgreSQL database provider
- Microsoft.Extensions.Caching.StackExchangeRedis 10.0.0 - Redis distributed cache integration

## Configuration

**Environment:**
- User Secrets configured for development
  - ApiService: UserSecretsId `073debf5-1921-4c8f-8b64-bdf2d3063075`
  - AppHost: UserSecretsId `d1b993d5-ae8d-43ff-821d-86492bb0b53f`
- Serilog configuration in `appsettings.json`
- OTEL_EXPORTER_OTLP_ENDPOINT environment variable enables OpenTelemetry export

**Build:**
- Standard .csproj format with implicit usings and nullable reference types enabled
- Solution file: `TradingBot.slnx`
- Editor config: `.editorconfig` (9.8 KB with comprehensive formatting rules)

## Platform Requirements

**Development:**
- .NET 10.0 SDK
- C# language support
- Visual Studio or VS Code with C# extension

**Production:**
- Deployment target: Docker containers via Aspire AppHost (`TradingBot.AppHost.csproj`)
- Container services: PostgreSQL (persistent), Redis (persistent), PgAdmin (port 5050), RedisInsight (port 5051)
- Dapr sidecar for distributed pub/sub and locking

## Project Structure

**Multi-project solution:**
1. `TradingBot.ApiService` - Main trading bot API service
2. `TradingBot.AppHost` - Aspire host configuration for containerized deployment
3. `TradingBot.ServiceDefaults` - Shared infrastructure defaults (OpenTelemetry, health checks, service discovery)
4. `tests/TradingBot.ApiService.Tests` - Unit and integration tests

## Aspire Configuration

**Container Setup (AppHost.cs):**
- PostgreSQL with persistent volume and PgAdmin UI
- Redis with persistent volume and RedisInsight UI
- Dapr pub/sub infrastructure with Redis backend
- Health checks on `/health` endpoint

---

*Stack analysis: 2026-02-12*
