# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with this repository.

## Project Overview

BTC Smart DCA Bot - A .NET 10.0 application that automatically accumulates Bitcoin on Hyperliquid spot market using intelligent dollar-cost-averaging with multipliers based on price drops and market conditions.

## Build & Run Commands

```bash
# Build the solution
dotnet build TradingBot.sln

# Run with Aspire (starts PostgreSQL, Redis, Dapr automatically)
cd TradingBot.AppHost && dotnet run

# Run API service directly (requires external dependencies)
cd TradingBot.ApiService && dotnet run

# Run tests
dotnet test

# Add EF Core migration
cd TradingBot.ApiService && dotnet ef migrations add <MigrationName>
```

## Architecture

**Layered Structure:**
- `BuildingBlocks/` - Reusable infrastructure (pub-sub, outbox, distributed locks, base entities)
- `Configuration/` - Strongly-typed options classes bound from appsettings.json
- `Infrastructure/` - Database context, Hyperliquid client, external integrations
- `Models/` - Domain entities with domain events

**Key Patterns:**
- Event-driven architecture with MediatR (in-process) and Dapr (cross-service)
- Transactional outbox pattern for reliable event publishing
- PostgreSQL advisory locks for distributed locking
- EIP-712 signing for Hyperliquid API authentication

**Event Flow:**
Domain Event → OutboxMessage (DB) → OutboxMessageProcessor → DaprMessageBroker → Dapr pub-sub → HTTP endpoint → MediatR handler

## Code Conventions

**Naming:**
- Entities: PascalCase, singular nouns (`Purchase`, `PriceHistory`)
- Options classes: Suffix with `Options` (`DcaOptions`, `HyperliquidOptions`)
- Interfaces: Prefix with `I` (`IHyperliquidClient`, `IMessageBroker`)
- Background services: Suffix with `Service` or `BackgroundService`

**Entity Design:**
- Inherit from `AuditedEntity` for automatic CreatedAt/UpdatedAt
- Use UUIDv7 for IDs (via `BaseEntity`)
- Domain events via `IDomainEvent` interface
- Configuration validated at startup with DataAnnotations

**Error Handling:**
- Use `Result<T>` pattern for expected failures
- Exceptions for unexpected/infrastructure failures
- Structured logging with Serilog (include correlation IDs)

**Logging Templates:**
```csharp
// Good - structured with named placeholders
_logger.LogInformation("Placing order for {Symbol} at {Price}", symbol, price);

// Bad - string interpolation
_logger.LogInformation($"Placing order for {symbol} at {price}");
```

## Key Files

- `TradingBot.ApiService/Program.cs` - Startup, DI configuration
- `TradingBot.ApiService/Infrastructure/Data/TradingBotDbContext.cs` - EF Core context
- `TradingBot.ApiService/Infrastructure/Hyperliquid/HyperliquidClient.cs` - Exchange API client
- `TradingBot.ApiService/Infrastructure/Hyperliquid/HyperliquidSigner.cs` - EIP-712 signing
- `TradingBot.ApiService/appsettings.json` - Configuration (DCA params, Hyperliquid settings)
- `TradingBot.AppHost/AppHost.cs` - Aspire orchestration (Docker containers)

## Configuration

**Secrets** - Use .NET User Secrets for private keys:
```bash
dotnet user-secrets set "Hyperliquid:PrivateKey" "<key>"
```

**Options Classes:**
- `DcaOptions` - Daily amount, schedule, multiplier tiers, bear market settings
- `HyperliquidOptions` - API URL, testnet toggle, wallet address

## Testing

- xUnit for test framework
- FluentAssertions for assertions
- Tests in `tests/TradingBot.ApiService.Tests/`

## Planning Documents

Detailed architecture and requirements are in `.planning/`:
- `PROJECT.md` - Vision and scope
- `REQUIREMENTS.md` - Functional requirements
- `ROADMAP.md` - Phase breakdown
- `codebase/ARCHITECTURE.md` - Detailed architecture
- `codebase/CONVENTIONS.md` - Full coding standards
