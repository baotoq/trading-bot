# Architecture

**Analysis Date:** 2026-02-12

## Pattern Overview

**Overall:** Modular .NET 10.0 web service with event-driven messaging and pub-sub patterns

**Key Characteristics:**
- Layered architecture with clear separation of concerns via BuildingBlocks
- Domain-driven design using domain events and integration events
- Async/await patterns throughout
- Distributed architecture support via Dapr runtime
- Outbox pattern for reliable message publishing

## Layers

**BuildingBlocks:**
- Purpose: Reusable infrastructure patterns and utilities for the entire application
- Location: `TradingBot.ApiService/BuildingBlocks/`
- Contains: Domain events, pub-sub abstractions, outbox pattern, distributed locks, background services
- Depends on: MediatR, Dapr, Entity Framework Core, Serilog
- Used by: All application services and domain models

**Domain Model Layer:**
- Purpose: Core business entities and value objects with domain events
- Location: `TradingBot.ApiService/BuildingBlocks/` (AuditedEntity, BaseEntity base classes)
- Contains: Audited entities with timestamps, domain event aggregates
- Depends on: BuildingBlocks abstractions
- Used by: Application services, database layer

**Service Defaults:**
- Purpose: Common cross-cutting concerns and Aspire cloud-native setup
- Location: `TradingBot.ServiceDefaults/`
- Contains: OpenTelemetry configuration, service discovery, health checks, resilience patterns
- Depends on: ASP.NET Core, OpenTelemetry
- Used by: All projects during startup

**Pub-Sub Layer:**
- Purpose: Event publishing and message brokering with Dapr integration
- Location: `TradingBot.ApiService/BuildingBlocks/Pubsub/`
- Contains: Abstractions, Dapr implementation, outbox pattern with Entity Framework
- Depends on: Dapr.Client, Entity Framework Core, MediatR
- Used by: Domain event handlers, background services

**API Service:**
- Purpose: HTTP endpoints and request handling
- Location: `TradingBot.ApiService/Program.cs`
- Contains: Web application setup, middleware configuration, endpoint routing
- Depends on: All other layers
- Used by: External clients

## Data Flow

**Outbox Pattern with Dapr:**

1. Domain event is generated in application logic
2. Event is serialized to `OutboxMessage` entity in database
3. Application transaction commits with outbox record
4. `OutboxMessageBackgroundService` polls database every 5 seconds
5. Unprocessed messages are batched (max 100 at a time)
6. `OutboxMessageProcessor` publishes each message via `DaprMessageBroker`
7. Message status transitions: Pending → Processing → Published
8. Failed messages retry up to 3 times before marking as Failed

**Dapr Pub-Sub Subscription Flow:**

1. `MapPubSub()` registers all subscriptions with Dapr endpoints
2. `/dapr/subscribe` returns subscription list (topics and routes)
3. Dapr invokes HTTP POST to subscription route when message arrives
4. Payload deserializes to `IntegrationEvent` type
5. MediatR publishes event to registered handlers
6. Handler processes event and returns 200 OK

**State Management:**
- Application state: Stored in PostgreSQL (Aspire orchestrated)
- Cache: Redis distributed cache via `Aspire.StackExchange.Redis.DistributedCaching`
- Message state: Outbox messages tracked in `OutboxMessages` table
- Locks: Dapr distributed locks via Redis store

## Key Abstractions

**IDomainEvent:**
- Purpose: Marker interface for domain events that must be published
- Examples: Strategies generate `IDomainEvent` implementations
- Pattern: Implements MediatR `INotification` for in-process pub-sub

**IntegrationEvent:**
- Purpose: Cross-service event communication via Dapr pub-sub
- Examples: Event handlers receive `IntegrationEvent` subtypes
- Pattern: Abstract record type with auto-generated IDs (UUIDv7) and timestamps

**IEventPublisher:**
- Purpose: Abstract event publication mechanism
- Examples: `OutboxEventPublisher` (transactional), `DaprEventPublisher` (async)
- Pattern: Swappable implementations for outbox or direct Dapr publishing

**IMessageBroker:**
- Purpose: Abstract message publishing to Dapr
- Examples: `DaprMessageBroker` wraps Dapr.Client for topic publishing
- Pattern: Single implementation, used by outbox processor

**IOutboxStore:**
- Purpose: Abstract outbox message persistence
- Examples: `EfCoreOutboxStore` uses Entity Framework Core with PostgreSQL
- Pattern: Query/update interface for outbox lifecycle

**IOutboxMessageProcessor:**
- Purpose: Abstract outbox message processing logic
- Examples: `OutboxMessageProcessor` with retry handling and status tracking
- Pattern: Handles message serialization, broker interaction, error recovery

## Entry Points

**Program.cs (API Service):**
- Location: `TradingBot.ApiService/Program.cs`
- Triggers: Application startup via `dotnet run` or container entrypoint
- Responsibilities:
  - Configure Serilog logging with templates and scopes
  - Register service collection extensions (CORS, health checks, service discovery)
  - Add distributed caching with Redis
  - Add pub-sub infrastructure
  - Map default endpoints (health checks)
  - Map pub-sub subscription routes

**MapPubSub():**
- Location: `TradingBot.ApiService/BuildingBlocks/Pubsub/Dapr/WebApplicationExtensions.cs`
- Triggers: During app build phase in Program.cs
- Responsibilities:
  - Expose `/dapr/subscribe` endpoint for Dapr discovery
  - Dynamically map POST routes for each subscription
  - Deserialize and publish events via MediatR

**OutboxMessageBackgroundService:**
- Location: `TradingBot.ApiService/BuildingBlocks/Pubsub/Outbox/OutboxMessageBackgroundService.cs`
- Triggers: Hosted service runs throughout application lifetime
- Responsibilities:
  - Poll database every 5 seconds for unprocessed messages
  - Batch process up to 100 messages per iteration
  - Delegate to `OutboxMessageProcessor` for each message

## Error Handling

**Strategy:** Graceful degradation with retry logic and logging

**Patterns:**
- Background services catch exceptions per iteration, log, and continue
- Outbox processor retries failed messages up to 3 times before failing
- Dapr event subscription endpoints return 200 OK even on handler errors (logged)
- Health checks only block startup if critical services are unavailable
- MediatR handlers can implement custom exception handling per domain event

## Cross-Cutting Concerns

**Logging:** Serilog with structured templates, context scopes, and OpenTelemetry integration
- Console output with timestamp, level, trace/span IDs, and formatted message
- Log context includes BackgroundService name, interval, and operation details
- Override rules suppress verbose frameworks (AspNetCore.Mvc, Routing, Hosting)

**Validation:** Domain events and integration events are type-checked at deserialization
- Outbox processor validates message payload against event type
- Dapr subscription validates JSON matches IntegrationEvent structure
- Invalid messages are logged and skipped without blocking service

**Authentication:** Not implemented in current codebase (future phase)

**Resilience:** Dapr sidecar provides built-in resilience
- HTTP client defaults configured with standard resilience handler
- Service discovery enables load balancing across replicas
- Health checks verify service readiness before accepting traffic

---

*Architecture analysis: 2026-02-12*
