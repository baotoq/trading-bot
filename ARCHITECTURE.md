# Architecture Documentation

## Overview

This trading bot application follows Clean Architecture principles with CQRS pattern using MediatR for handling requests.

## Architecture Layers

```
┌─────────────────────────────────────────────────────────┐
│                    Presentation Layer                    │
│  (Blazor UI + API Endpoints)                            │
└─────────────────────┬───────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────┐
│                   Application Layer                      │
│  (MediatR Handlers - Features)                          │
└─────────────────────┬───────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────┐
│                    Domain Layer                          │
│  (Services, Models, Business Logic)                     │
└─────────────────────┬───────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────┐
│                 Infrastructure Layer                     │
│  (Binance.Net, External APIs)                           │
└─────────────────────────────────────────────────────────┘
```

## Project Structure

```
TradingBot.ApiService/
├── Features/                    # CQRS Features (Application Layer)
│   └── Binance/
│       ├── PingBinance.cs      # Query + Handler
│       ├── GetTicker.cs        # Query + Handler
│       ├── GetAllTickers.cs    # Query + Handler
│       ├── GetOrderBook.cs     # Query + Handler
│       └── GetAccountInfo.cs   # Query + Handler
│
├── Endpoints/                   # API Endpoints (Presentation Layer)
│   └── BinanceEndpoints.cs     # Minimal API endpoints
│
├── Services/                    # Domain Services
│   ├── IBinanceService.cs      # Service interface
│   └── BinanceService.cs       # Service implementation
│
├── Models/                      # Domain Models
│   ├── BinanceTickerData.cs
│   ├── BinanceOrderBookData.cs
│   └── BinanceAccountInfo.cs
│
└── Program.cs                   # Application entry point

TradingBot.Web/
├── Components/
│   ├── Pages/
│   │   └── Binance.razor       # Blazor UI component
│   └── Layout/
│       └── NavMenu.razor
├── BinanceApiClient.cs          # HTTP client for API
└── Program.cs

TradingBot.AppHost/
└── AppHost.cs                   # Aspire orchestration

TradingBot.ServiceDefaults/
└── Extensions.cs                # Shared configuration
```

## CQRS with MediatR

### What is CQRS?

**CQRS (Command Query Responsibility Segregation)** separates read operations (Queries) from write operations (Commands).

- **Queries**: Read data without modifying state
- **Commands**: Modify state but typically don't return data

### Why MediatR?

**Benefits:**
1. ✅ **Decoupling**: Separates request from handler logic
2. ✅ **Single Responsibility**: Each handler does one thing
3. ✅ **Testability**: Easy to unit test handlers in isolation
4. ✅ **Maintainability**: Clear structure and organization
5. ✅ **Extensibility**: Easy to add behaviors (logging, validation, caching)
6. ✅ **Cross-cutting Concerns**: Pipeline behaviors for common logic

### Feature Structure

Each feature follows this pattern:

```csharp
public static class GetTicker
{
    // Request (Query or Command)
    public record Query(string Symbol) : IRequest<BinanceTickerData?>;

    // Handler
    public class Handler : IRequestHandler<Query, BinanceTickerData?>
    {
        private readonly IBinanceService _binanceService;

        public Handler(IBinanceService binanceService)
        {
            _binanceService = binanceService;
        }

        public async Task<BinanceTickerData?> Handle(Query request, CancellationToken cancellationToken)
        {
            return await _binanceService.GetTickerAsync(request.Symbol, cancellationToken);
        }
    }
}
```

### Request Flow

```
1. API Endpoint receives HTTP request
   ↓
2. Endpoint creates MediatR Query/Command
   ↓
3. MediatR dispatches to appropriate Handler
   ↓
4. Handler executes business logic via Services
   ↓
5. Handler returns result
   ↓
6. Endpoint transforms result to HTTP response
```

**Example:**

```
GET /binance/ticker/BTCUSDT
  ↓
BinanceEndpoints.GetBinanceTicker(symbol, mediator)
  ↓
mediator.Send(new GetTicker.Query("BTCUSDT"))
  ↓
GetTicker.Handler.Handle(query, cancellationToken)
  ↓
_binanceService.GetTickerAsync("BTCUSDT")
  ↓
Binance.Net API call
  ↓
Return BinanceTickerData
```

## Design Patterns Used

### 1. Mediator Pattern
- **Location**: MediatR throughout application
- **Purpose**: Decouples components by centralizing communication
- **Example**: Endpoints send requests through IMediator

### 2. Repository Pattern (Implicit)
- **Location**: BinanceService
- **Purpose**: Abstracts data access from external API
- **Example**: IBinanceService interface

### 3. Dependency Injection
- **Location**: Program.cs service registration
- **Purpose**: Loose coupling and testability
- **Example**: Constructor injection in handlers

### 4. Factory Pattern
- **Location**: BinanceRestClient creation
- **Purpose**: Complex object creation
- **Example**: Configuring client with API keys

### 5. Vertical Slice Architecture
- **Location**: Features folder
- **Purpose**: Group related functionality together
- **Example**: Each feature contains query + handler

## Current Features (Queries)

### 1. PingBinance
- **Purpose**: Test Binance API connectivity
- **Request**: `PingBinance.Query`
- **Response**: `{ Connected: bool, Timestamp: DateTime }`
- **Endpoint**: `GET /binance/ping`

### 2. GetTicker
- **Purpose**: Get 24h price statistics for a symbol
- **Request**: `GetTicker.Query(Symbol)`
- **Response**: `BinanceTickerData?`
- **Endpoint**: `GET /binance/ticker/{symbol}`

### 3. GetAllTickers
- **Purpose**: Get all ticker data
- **Request**: `GetAllTickers.Query`
- **Response**: `IEnumerable<BinanceTickerData>`
- **Endpoint**: `GET /binance/tickers`

### 4. GetOrderBook
- **Purpose**: Get order book for a symbol
- **Request**: `GetOrderBook.Query(Symbol, Limit)`
- **Response**: `BinanceOrderBookData?`
- **Endpoint**: `GET /binance/orderbook/{symbol}`

### 5. GetAccountInfo
- **Purpose**: Get account information and balances
- **Request**: `GetAccountInfo.Query`
- **Response**: `BinanceAccountInfo?`
- **Endpoint**: `GET /binance/account`

## Future Enhancements

### Commands (Write Operations)

When adding trading functionality, implement Commands:

```csharp
public static class PlaceOrder
{
    public record Command(
        string Symbol,
        OrderSide Side,
        OrderType Type,
        decimal Quantity,
        decimal? Price
    ) : IRequest<OrderResult>;

    public class Handler : IRequestHandler<Command, OrderResult>
    {
        // Implementation
    }
}
```

### Pipeline Behaviors

Add cross-cutting concerns as MediatR behaviors:

```csharp
// Logging Behavior
public class LoggingBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling {RequestName}", typeof(TRequest).Name);
        var response = await next();
        _logger.LogInformation("Handled {RequestName}", typeof(TRequest).Name);
        return response;
    }
}

// Validation Behavior with FluentValidation
public class ValidationBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
{
    // Validates request before handling
}

// Caching Behavior
public class CachingBehavior<TRequest, TResponse> 
    : IPipelineBehavior<TRequest, TResponse>
{
    // Cache query results
}
```

### Example: Adding a New Feature

To add a new feature (e.g., Get Historical Data):

1. **Create Feature File**:
   ```
   Features/Binance/GetHistoricalData.cs
   ```

2. **Define Query and Handler**:
   ```csharp
   public static class GetHistoricalData
   {
       public record Query(string Symbol, DateTime Start, DateTime End) 
           : IRequest<IEnumerable<Candle>>;

       public class Handler : IRequestHandler<Query, IEnumerable<Candle>>
       {
           // Implementation
       }
   }
   ```

3. **Add Endpoint**:
   ```csharp
   group.MapGet("/history/{symbol}", async (string symbol, DateTime start, DateTime end, IMediator mediator) =>
   {
       var data = await mediator.Send(new GetHistoricalData.Query(symbol, start, end));
       return Results.Ok(data);
   });
   ```

4. **Done!** MediatR automatically discovers and registers the handler.

## Testing Strategy

### Unit Testing Handlers

```csharp
public class GetTickerHandlerTests
{
    [Fact]
    public async Task Handle_ValidSymbol_ReturnsTickerData()
    {
        // Arrange
        var mockService = new Mock<IBinanceService>();
        mockService
            .Setup(s => s.GetTickerAsync("BTCUSDT", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BinanceTickerData { Symbol = "BTCUSDT", LastPrice = 50000 });
        
        var handler = new GetTicker.Handler(mockService.Object);
        var query = new GetTicker.Query("BTCUSDT");

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("BTCUSDT", result.Symbol);
        Assert.Equal(50000, result.LastPrice);
    }
}
```

### Integration Testing

```csharp
public class BinanceEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task GetTicker_ValidSymbol_ReturnsOk()
    {
        // Use WebApplicationFactory to test full request pipeline
    }
}
```

## Performance Considerations

1. **Async/Await**: All handlers are async for better scalability
2. **CancellationToken**: Properly propagated for request cancellation
3. **Scoped Services**: Handlers are transient, services are scoped
4. **Connection Pooling**: HttpClient and API clients use connection pooling

## Security

1. **API Keys**: Stored in User Secrets / Key Vault
2. **Rate Limiting**: Should be added as a pipeline behavior
3. **Authorization**: Can be added as a handler decorator
4. **Input Validation**: Can be added with FluentValidation

## Monitoring & Observability

With .NET Aspire:
- Built-in telemetry for all requests
- Distributed tracing through MediatR pipeline
- Health checks for Binance service
- Metrics and logs in Aspire dashboard

## Best Practices

1. ✅ **One Handler per Request**: Each query/command has exactly one handler
2. ✅ **Immutable Requests**: Use `record` types for requests
3. ✅ **Thin Endpoints**: Endpoints only dispatch to MediatR
4. ✅ **Fat Handlers**: Business logic lives in handlers
5. ✅ **Service Layer**: Complex logic delegated to services
6. ✅ **Feature Folders**: Group related code together
7. ✅ **Explicit Dependencies**: Clear constructor injection

## Resources

- [MediatR Documentation](https://github.com/jbogard/MediatR)
- [CQRS Pattern](https://martinfowler.com/bliki/CQRS.html)
- [Vertical Slice Architecture](https://www.jimmybogard.com/vertical-slice-architecture/)
- [Clean Architecture](https://blog.cleancoder.com/uncle-bob/2012/08/13/the-clean-architecture.html)


