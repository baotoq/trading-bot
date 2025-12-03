# Trading Bot UI

A modern Next.js dashboard for managing and monitoring cryptocurrency trading bot operations.

## Features

- **Trading Panel**: Execute trades with real-time signal analysis
- **Market Analysis**: Analyze market conditions and trends
- **Backtesting**: Test trading strategies on historical data
- **Modern UI**: Built with Ant Design and Tailwind CSS
- **TypeScript**: Fully typed for better developer experience

## Tech Stack

- **Framework**: Next.js 15+ (App Router)
- **UI Library**: Ant Design 6.x
- **Styling**: Tailwind CSS 4.x
- **Language**: TypeScript
- **API Client**: Axios
- **Date Handling**: Day.js

## Getting Started

### Prerequisites

- Node.js 18+
- npm or yarn
- Trading Bot API running (default: http://localhost:5000)

### Installation

1. Install dependencies:
```bash
npm install
```

2. Create environment file:
```bash
cp .env.local.example .env.local
```

3. Update the API URL in `.env.local`:
```env
NEXT_PUBLIC_API_URL=http://localhost:5000
```

### Development

Run the development server:

```bash
npm run dev
```

Open [http://localhost:3000](http://localhost:3000) with your browser to see the dashboard.

### Build

Build for production:

```bash
npm run build
```

Run production build:

```bash
npm start
```

## Project Structure

```
trading-bot-ui/
├── src/
│   ├── app/              # Next.js app router pages
│   │   ├── layout.tsx    # Root layout with Ant Design config
│   │   ├── page.tsx      # Main dashboard page
│   │   └── globals.css   # Global styles with Tailwind
│   ├── components/       # React components
│   │   ├── TradingPanel.tsx      # Trading execution component
│   │   ├── MarketAnalysis.tsx    # Market analysis component
│   │   └── BacktestPanel.tsx     # Backtesting component
│   ├── lib/             # Utilities and API client
│   │   └── api.ts       # API integration layer
│   └── types/           # TypeScript type definitions
│       └── index.ts     # Shared types
├── public/              # Static assets
├── tailwind.config.ts   # Tailwind CSS configuration
├── tsconfig.json        # TypeScript configuration
└── next.config.ts       # Next.js configuration
```

## API Integration

The dashboard integrates with the following API endpoints:

- `POST /api/trade/execute` - Execute a trade
- `GET /api/trade/analyze/{symbol}` - Analyze trading signals
- `GET /api/market/condition/{symbol}` - Get market conditions
- `POST /api/backtest/run` - Run strategy backtest
- `POST /api/backtest/compare` - Compare multiple strategies

## Configuration

### Ant Design Theme

Customize the Ant Design theme in `src/app/layout.tsx`:

```tsx
<ConfigProvider
  theme={{
    token: {
      colorPrimary: "#1890ff",
      borderRadius: 6,
    },
  }}
>
```

### Tailwind CSS

Configure Tailwind in `tailwind.config.ts`. Note that `preflight` is disabled to avoid conflicts with Ant Design.

## License

ISC
