# Trading Bot

A modern cryptocurrency trading bot built with .NET 10, Aspire, and Blazor, integrated with the Binance API.

## Features

- 🚀 **Modern .NET Stack**: Built with .NET 10 and .NET Aspire for cloud-native distributed applications
- 💹 **Binance Integration**: Full integration with Binance REST API for real-time market data
- 📊 **Real-time Data**: Live ticker prices, order books, and market statistics
- 🎨 **Beautiful UI**: Interactive Blazor web interface for monitoring markets
- 🔧 **Extensible Architecture**: Clean service-based architecture with interfaces
- 🔒 **Secure**: Proper API key management with support for user secrets
- 📈 **Production Ready**: Built with Aspire for observability, health checks, and service discovery

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- (Optional) Binance API keys for private endpoints

### Running the Application

1. Clone the repository:
   ```bash
   git clone <your-repo-url>
   cd trading-bot
   ```

2. Build the solution:
   ```bash
   dotnet build
   ```

3. Run the application:
   ```bash
   dotnet run --project TradingBot.AppHost
   ```

4. Open the Aspire dashboard (URL will be shown in console) and navigate to the web frontend

5. Click on "Binance" in the navigation menu to view market data

### Configuration (Optional)

For accessing private endpoints (account info, trading), configure your Binance API keys:

```bash
cd TradingBot.ApiService
dotnet user-secrets set "Binance:ApiKey" "your-api-key"
dotnet user-secrets set "Binance:ApiSecret" "your-api-secret"
dotnet user-secrets set "Binance:TestMode" "true"
```

## Project Structure

```
trading-bot/
├── TradingBot.ApiService/       # REST API service
│   ├── Features/                # CQRS features with MediatR
│   ├── Services/                # Binance service implementation
│   ├── Models/                  # Data models
│   ├── Endpoints/               # API endpoints
│   └── Program.cs               # Application entry point
├── TradingBot.Web/              # Blazor web frontend
│   ├── Components/
│   │   └── Pages/
│   │       └── Binance.razor    # Market data UI
│   ├── Services/                # Refit API clients
│   │   ├── IBinanceApiClient.cs
│   │   └── BinanceApiClientWrapper.cs
│   └── Models/                  # DTOs
├── TradingBot.AppHost/          # Aspire orchestration
├── TradingBot.ServiceDefaults/  # Shared configuration
├── ARCHITECTURE.md              # Architecture documentation
└── BINANCE_INTEGRATION.md       # Detailed integration docs
```

## API Endpoints

### Public Endpoints (No API Key Required)

- `GET /binance/ping` - Test Binance API connectivity
- `GET /binance/ticker/{symbol}` - Get 24h price statistics for a symbol
- `GET /binance/tickers` - Get all tickers
- `GET /binance/orderbook/{symbol}` - Get order book data

### Private Endpoints (API Key Required)

- `GET /binance/account` - Get account information and balances

See [BINANCE_INTEGRATION.md](./BINANCE_INTEGRATION.md) for detailed API documentation.

## Technology Stack

- **Backend**: .NET 10, ASP.NET Core Minimal APIs
- **Architecture**: CQRS with MediatR
- **Frontend**: Blazor Server, Bootstrap 5, Refit for API calls
- **Orchestration**: .NET Aspire
- **Integration**: Binance.Net v11.11.0
- **Caching**: Redis (via Aspire)
- **Observability**: Built-in Aspire dashboard with telemetry

## Architecture

This application follows **Clean Architecture** principles with **CQRS pattern** using **MediatR**:

- **Vertical Slice Architecture**: Features organized by business capability
- **Mediator Pattern**: Decoupled request/handler communication
- **Dependency Injection**: Loose coupling and testability
- **Feature Folders**: `Features/Binance/` contains queries and handlers

See [ARCHITECTURE.md](./ARCHITECTURE.md) for detailed architecture documentation.

## Features in Detail

### Market Data Viewer

The web interface provides:
- Real-time price data for popular trading pairs (BTC, ETH, BNB, SOL, XRP, ADA)
- Symbol search functionality
- 24-hour price statistics (high, low, change %)
- Order book visualization with bid/ask spreads
- Connection health monitoring

### Binance Service

The `BinanceService` provides:
- Async/await based API calls
- Proper error handling and logging
- Support for both testnet and live environments
- Clean interface-based design for testability

## Development

### Adding New Features

1. Add new methods to `IBinanceService` interface
2. Implement in `BinanceService`
3. Create API endpoints in `Program.cs`
4. Update `BinanceApiClient` for web consumption
5. Add UI components in Blazor pages

### Testing

```bash
# Build the solution
dotnet build

# Run tests (when added)
dotnet test

# Run with watch mode for development
dotnet watch --project TradingBot.AppHost
```

## Security Best Practices

⚠️ **Important Security Notes:**

1. Never commit API keys to source control
2. Use User Secrets for local development
3. Use Azure Key Vault or similar for production
4. Enable IP whitelisting on Binance API keys
5. Restrict API key permissions to minimum required
6. Use testnet for development and testing

## Roadmap

Future features planned:
- [ ] WebSocket integration for real-time streaming
- [ ] Trading functionality (place/cancel orders)
- [ ] Strategy backtesting framework
- [ ] Portfolio management and P&L tracking
- [ ] Price alerts and notifications
- [ ] Multiple exchange support
- [ ] Automated trading strategies

## Resources

- [.NET Aspire Documentation](https://learn.microsoft.com/en-us/dotnet/aspire/)
- [Binance API Documentation](https://binance-docs.github.io/apidocs/spot/en/)
- [Binance.Net Library](https://github.com/JKorf/Binance.Net)

## License

[Your License Here]

## Contributing

Contributions are welcome! Please feel free to submit a Pull Request.