# Codebase Structure

**Analysis Date:** 2026-02-12

## Directory Layout

```
trading-bot/
├── TradingBot.ApiService/              # Main API service (.NET 10.0)
│   ├── BuildingBlocks/                 # Reusable infrastructure patterns
│   │   ├── Pubsub/                     # Event pub-sub implementation
│   │   │   ├── Abstraction/            # IEventPublisher, IMessageBroker
│   │   │   ├── Dapr/                   # Dapr integration
│   │   │   └── Outbox/                 # Transactional outbox pattern
│   │   ├── DistributedLocks/           # Dapr distributed locking
│   │   ├── AuditedEntity.cs            # Base class with audit timestamps
│   │   ├── BaseEntity.cs               # Base class with UUIDv7 ID generation
│   │   ├── IDomainEvent.cs             # Domain event marker interface
│   │   └── TimeBackgroundService.cs    # Periodic background service base
│   ├── Program.cs                      # Application entry point
│   ├── appsettings.json                # Serilog configuration
│   ├── appsettings.Development.json    # Development overrides
│   └── TradingBot.ApiService.csproj    # Project file with dependencies
├── TradingBot.AppHost/                 # Aspire orchestration host
│   └── AppHost.cs                      # Docker container orchestration
├── TradingBot.ServiceDefaults/         # Common cloud-native setup
│   └── Extensions.cs                   # OpenTelemetry, health checks, resilience
├── tests/                              # Test projects
│   └── TradingBot.ApiService.Tests/    # API service unit tests
│       ├── Tests.cs                    # Placeholder test structure
│       └── TradingBot.ApiService.Tests.csproj
└── .planning/                          # GSD planning documents
    └── codebase/                       # Architecture and structure docs
```

## Directory Purposes

**TradingBot.ApiService:**
- Purpose: HTTP API service for trading bot operations
- Contains: Domain logic, services, endpoints, background jobs
- Key files: `Program.cs` (startup), `appsettings.json` (configuration)

**BuildingBlocks:**
- Purpose: Reusable infrastructure patterns shared across layers
- Contains: Domain event abstraction, pub-sub implementation, outbox pattern, base entity types
- Key files: `IDomainEvent.cs`, `BaseEntity.cs`, `AuditedEntity.cs`

**BuildingBlocks/Pubsub:**
- Purpose: Event publication and subscription with Dapr
- Contains: Event abstractions, message brokers, outbox with Entity Framework
- Key files: `IEventPublisher.cs`, `IntegrationEvent.cs`, `OutboxMessage.cs`

**BuildingBlocks/Pubsub/Dapr:**
- Purpose: Dapr runtime integration for pub-sub messaging
- Contains: Event publisher, message broker, registry, subscription routing
- Key files: `DaprEventPublisher.cs`, `DaprMessageBroker.cs`, `WebApplicationExtensions.cs`

**BuildingBlocks/Pubsub/Outbox:**
- Purpose: Transactional outbox pattern for reliable message publishing
- Contains: Outbox message model, processor, background service, EF Core store
- Key files: `OutboxMessage.cs`, `OutboxMessageProcessor.cs`, `OutboxMessageBackgroundService.cs`

**BuildingBlocks/DistributedLocks:**
- Purpose: Distributed locking via Dapr using Redis backend
- Contains: Lock interface, Dapr implementation
- Key files: `DistributedLock.cs`, `ServiceCollectionExtensions.cs`

**TradingBot.ServiceDefaults:**
- Purpose: Aspire-managed cloud-native defaults
- Contains: OpenTelemetry, service discovery, health checks, resilience handlers
- Key files: `Extensions.cs`

**TradingBot.AppHost:**
- Purpose: Local Aspire orchestration for development
- Contains: Docker container definitions, dependencies, volume mappings
- Key files: `AppHost.cs`

**tests/TradingBot.ApiService.Tests:**
- Purpose: Unit test suite for API service
- Contains: Test classes using xunit, FluentAssertions, NSubstitute
- Key files: `Tests.cs`

## Key File Locations

**Solution File**
- `TradingBot.slnx`

**Entry Points:**
- `TradingBot.ApiService/Program.cs`: Application startup, dependency injection, middleware setup
- `TradingBot.AppHost/AppHost.cs`: Local development orchestration with Postgres, Redis, Dapr

**Configuration:**
- `TradingBot.ApiService/appsettings.json`: Serilog logging configuration
- `TradingBot.ServiceDefaults/Extensions.cs`: Cloud-native middleware configuration

**Core Domain Model:**
- `TradingBot.ApiService/BuildingBlocks/BaseEntity.cs`: ID generation (UUIDv7)
- `TradingBot.ApiService/BuildingBlocks/AuditedEntity.cs`: Audit timestamps
- `TradingBot.ApiService/BuildingBlocks/IDomainEvent.cs`: Domain event marker

**Event Publishing Abstractions:**
- `TradingBot.ApiService/BuildingBlocks/Pubsub/Abstraction/IEventPublisher.cs`: Publish interface
- `TradingBot.ApiService/BuildingBlocks/Pubsub/Abstraction/IntegrationEvent.cs`: Event base class
- `TradingBot.ApiService/BuildingBlocks/Pubsub/Abstraction/IMessageBroker.cs`: Broker interface

**Dapr Integration:**
- `TradingBot.ApiService/BuildingBlocks/Pubsub/Dapr/DaprEventPublisher.cs`: Event publishing
- `TradingBot.ApiService/BuildingBlocks/Pubsub/Dapr/DaprMessageBroker.cs`: Dapr.Client wrapper
- `TradingBot.ApiService/BuildingBlocks/Pubsub/Dapr/WebApplicationExtensions.cs`: Subscription routing

**Outbox Pattern:**
- `TradingBot.ApiService/BuildingBlocks/Pubsub/Outbox/OutboxMessage.cs`: Persisted event
- `TradingBot.ApiService/BuildingBlocks/Pubsub/Outbox/OutboxMessageProcessor.cs`: Processing logic
- `TradingBot.ApiService/BuildingBlocks/Pubsub/Outbox/OutboxMessageBackgroundService.cs`: Poll scheduler
- `TradingBot.ApiService/BuildingBlocks/Pubsub/Outbox/EfCore/EfCoreOutboxStore.cs`: EF Core persistence

**Background Services:**
- `TradingBot.ApiService/BuildingBlocks/TimeBackgroundService.cs`: Periodic task base class
- `TradingBot.ApiService/BuildingBlocks/DistributedLocks/DistributedLock.cs`: Lock abstraction

## Naming Conventions

**Files:**
- Entity classes: PascalCase, plural/singular based on usage (e.g., `OutboxMessage`, `BaseEntity`)
- Service classes: PascalCase with `Service` suffix (e.g., `OutboxMessageProcessor`)
- Abstraction interfaces: PascalCase with `I` prefix (e.g., `IEventPublisher`, `IOutboxStore`)
- Extension classes: PascalCase with `Extensions` suffix (e.g., `ServiceCollectionExtensions`)
- Background services: PascalCase with `BackgroundService` suffix (e.g., `OutboxMessageBackgroundService`)

**Directories:**
- Functional areas: PascalCase (e.g., `Pubsub`, `DistributedLocks`, `Outbox`)
- Abstraction layers: `Abstraction/` subdirectory pattern
- Provider implementations: Named after provider (e.g., `Dapr/`, `EfCore/`)

**Namespaces:**
- Format: `TradingBot.ApiService.[Feature].[SubFeature]`
- Examples:
  - `TradingBot.ApiService.BuildingBlocks`
  - `TradingBot.ApiService.BuildingBlocks.Pubsub`
  - `TradingBot.ApiService.BuildingBlocks.Pubsub.Dapr`
  - `TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox.EfCore`

## Where to Add New Code

**New Feature (Trading Strategy, Indicator, etc.):**
- Primary code: Create feature directory in `TradingBot.ApiService/` with its own subdirectories
  - Example: `TradingBot.ApiService/Application/Strategies/`
- Tests: Mirror structure in `tests/TradingBot.ApiService.Tests/Application/Strategies/`
- Domain events: Place in `TradingBot.ApiService/Application/[Feature]/DomainEvents/`
- Namespace: `TradingBot.ApiService.Application.[Feature]`

**New Service/Business Logic:**
- Implementation: `TradingBot.ApiService/Application/Services/`
- Interfaces: `TradingBot.ApiService/Application/Services/Abstractions/`
- Namespace: `TradingBot.ApiService.Application.Services`

**New API Endpoint:**
- Minimal API: Add to `Program.cs` route mapping section
- Or create feature file: `TradingBot.ApiService/Application/[Feature]/Routes.cs`
- Handler pattern: Use MediatR commands/queries or direct service injection

**New Utility/Helper:**
- Shared helpers: `TradingBot.ApiService/Common/`
- Domain-specific: Place in feature folder under `Utilities/` subdirectory
- Namespace: `TradingBot.ApiService.Common` or `TradingBot.ApiService.[Feature].Utilities`

**New Event Handler:**
- Location: `TradingBot.ApiService/Application/[DomainArea]/DomainEventHandlers/`
- Implementation: Implement `INotificationHandler<TEvent>` from MediatR
- Namespace: `TradingBot.ApiService.Application.[DomainArea].DomainEventHandlers`

**New Background Job:**
- Location: `TradingBot.ApiService/Application/BackgroundJobs/`
- Base class: Inherit from `TimeBackgroundService`
- Namespace: `TradingBot.ApiService.Application.BackgroundJobs`
- Registration: Add `AddHostedService<YourService>()` in `Program.cs`

## Special Directories

**bin/**
- Purpose: Compiled output
- Generated: Yes
- Committed: No (in .gitignore)

**obj/**
- Purpose: Build artifacts, temporary files
- Generated: Yes
- Committed: No (in .gitignore)

**Properties/**
- Purpose: Project metadata, assembly info
- Generated: Partial (launchSettings.json auto-generated)
- Committed: Yes
- Key file: `launchSettings.json` defines development profiles

**BuildingBlocks/**
- Purpose: Reusable, framework-level patterns
- Generated: No
- Committed: Yes
- Note: Changes here affect entire system; test carefully

**.planning/**
- Purpose: GSD orchestrator planning and analysis documents
- Generated: Yes (by mapping commands)
- Committed: Yes (documents committed, not working copies)

---

*Structure analysis: 2026-02-12*
