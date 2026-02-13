# BTC Smart DCA Bot

A smart Dollar-Cost-Averaging bot that automatically accumulates Bitcoin on the Hyperliquid spot market with intelligent buying logic.

## Features

- **Daily Fixed-Amount Purchases**: Executes scheduled USD purchases of BTC
- **Smart Multipliers**: Increases position size during price dips based on configurable tiers
- **30-Day High Tracking**: Calculates drop-from-high percentages for multiplier decisions
- **Bear Market Boost**: Enhances buying when BTC is below its 200-day moving average
- **Telegram Notifications**: Sends alerts on each purchase
- **Purchase History**: Maintains detailed records with full audit trail

## Tech Stack

| Category | Technology |
|----------|-----------|
| Runtime | .NET 10.0 |
| Framework | ASP.NET Core |
| Database | PostgreSQL (EF Core) |
| Cache | Redis |
| Message Queue | Dapr pub/sub |
| Orchestration | .NET Aspire |
| Crypto Signing | Nethereum (EIP-712) |
| Notifications | Telegram.Bot |

## Prerequisites

- .NET 10.0 SDK
- Docker Desktop (for PostgreSQL, Redis, Dapr)

## Getting Started

### 1. Clone the repository

```bash
git clone <repository-url>
cd trading-bot
```

### 2. Configure secrets

Store your Hyperliquid private key using .NET User Secrets:

```bash
cd TradingBot.ApiService
dotnet user-secrets set "Hyperliquid:PrivateKey" "<your-private-key>"
```

### 3. Run with Aspire (recommended)

```bash
cd TradingBot.AppHost
dotnet run
```

This automatically starts:
- PostgreSQL on port 5432
- Redis on port 6379
- PgAdmin on port 5050
- RedisInsight on port 5051
- API service on port 5000

### 4. Run without Aspire

Ensure PostgreSQL and Redis are running externally, then:

```bash
cd TradingBot.ApiService
dotnet run
```

## Configuration

Edit `TradingBot.ApiService/appsettings.json`:

```json
{
  "Dca": {
    "DailyAmountUsd": 10,
    "ScheduleHourUtc": 12,
    "MultiplierTiers": [
      { "DropPercent": 5, "Multiplier": 1.5 },
      { "DropPercent": 10, "Multiplier": 2.0 },
      { "DropPercent": 20, "Multiplier": 3.0 }
    ],
    "BearMarketMultiplier": 1.5
  },
  "Hyperliquid": {
    "BaseUrl": "https://api.hyperliquid-testnet.xyz",
    "IsTestnet": true,
    "WalletAddress": "<your-wallet-address>"
  }
}
```

## Project Structure

```
trading-bot/
├── TradingBot.ApiService/        # Main API service
│   ├── BuildingBlocks/           # Reusable infrastructure
│   ├── Configuration/            # Options classes
│   ├── Infrastructure/           # Database, Hyperliquid client
│   └── Models/                   # Domain entities
├── TradingBot.AppHost/           # Aspire orchestration
├── TradingBot.ServiceDefaults/   # Cloud-native defaults
├── tests/                        # Unit tests
└── .planning/                    # Architecture documentation
```

## Running Tests

```bash
dotnet test
```

## Add Agent Skills

```bash
npx skills add baotoq/agent-skills
```

## Development Roadmap

| Phase | Description | Status |
|-------|-------------|--------|
| 1 | Foundation & Hyperliquid Client | Complete |
| 2 | Daily Scheduler & Telegram | In Progress |
| 3 | Smart Multipliers & MA Calculations | Planned |
| 4 | Health Checks & Dry-Run Mode | Planned |

## License

Private - All rights reserved
