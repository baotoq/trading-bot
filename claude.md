# Trading Bot System - Claude Code Documentation

## System Overview

This is a professional Binance Futures day trading system built with .NET 10.0, designed with multiple trading strategies and comprehensive risk management.

## Architecture

### Core Components

1. **Trading Strategies** (Strategy Pattern)
   - [EmaMomentumScalperStrategy.cs](TradingBot.ApiService/Application/Strategies/EmaMomentumScalperStrategy.cs) - EMA crossover with momentum confirmation
   - [BollingerSqueezeStrategy.cs](TradingBot.ApiService/Application/Strategies/BollingerSqueezeStrategy.cs) - Bollinger Band squeeze + volume breakout
   - [RsiDivergenceStrategy.cs](TradingBot.ApiService/Application/Strategies/RsiDivergenceStrategy.cs) - RSI divergence at support/resistance
   - [VwapMeanReversionStrategy.cs](TradingBot.ApiService/Application/Strategies/VwapMeanReversionStrategy.cs) - VWAP mean reversion

2. **Services**
   - [TechnicalIndicatorService.cs](TradingBot.ApiService/Application/Services/TechnicalIndicatorService.cs) - Calculates all technical indicators
   - [MarketAnalysisService.cs](TradingBot.ApiService/Application/Services/MarketAnalysisService.cs) - Pre-market condition analysis
   - [PositionCalculatorService.cs](TradingBot.ApiService/Application/Services/PositionCalculatorService.cs) - Entry/SL/TP calculations
   - [RiskManagementService.cs](TradingBot.ApiService/Application/Services/RiskManagementService.cs) - Enforces risk limits
   - [BinanceService.cs](TradingBot.ApiService/Application/Services/BinanceService.cs) - Binance API integration
   - [BacktestService.cs](TradingBot.ApiService/Application/Services/BacktestService.cs) - Historical strategy testing

3. **Domain Models**
   - [TradingSignal.cs](TradingBot.ApiService/Domain/TradingSignal.cs) - Trading signals with confidence
   - [Position.cs](TradingBot.ApiService/Domain/Position.cs) - Active position tracking
   - [TradeLog.cs](TradingBot.ApiService/Domain/TradeLog.cs) - Complete trade history
   - [MarketCondition.cs](TradingBot.ApiService/Domain/MarketCondition.cs) - Market regime analysis

## Risk Management Rules

### Hard Limits (Enforced by [RiskManagementService.cs](TradingBot.ApiService/Application/Services/RiskManagementService.cs))

- **Risk per trade**: 2% to 4% of account equity (configurable)
- **Max consecutive losses**: 3 trades
- **Daily drawdown limit**: 6% of starting balance
- **Max trades per day**: 5 trades
- **Max concurrent positions**: 3 positions
- **Min reward-to-risk ratio**: 2:1

### Position Sizing

Formula: `Position Size = (Account × Risk%) / (SL Distance %)`

Example with $10,000 account, 2.5% risk:
- Risk amount = $250
- If SL is 1.5% from entry: Position size = $250 / 1.5% = $16,666
- With 10x leverage: $1,666 margin used

## Trading Workflow

### Phase 1: Pre-Market Analysis
[MarketAnalysisService.cs:76](TradingBot.ApiService/Application/Services/MarketAnalysisService.cs#L76)
- Analyze market regime (trending/ranging/volatile)
- Check ATR volatility
- Verify funding rate not extreme
- Determine if market is tradeable

### Phase 2: Signal Generation
[ExecuteTradeCommand.cs:89](TradingBot.ApiService/Application/Commands/ExecuteTradeCommand.cs#L89)
- Strategy analyzes current market conditions
- Generates Buy/Sell/StrongBuy/StrongSell/Hold signal
- Provides confidence score (0-1)
- Returns reason for decision

### Phase 3: Position Calculation
[ExecuteTradeCommand.cs:107](TradingBot.ApiService/Application/Commands/ExecuteTradeCommand.cs#L107)
- Calculate entry price (market ± 0.02%)
- Calculate stop-loss (1.5× ATR or swing point)
- Calculate take-profits:
  - TP1: 2R (50% position)
  - TP2: 3R (30% position)
  - TP3: 5R trailing (20% position)
- Calculate position size and leverage

### Phase 4: Risk Validation
[ExecuteTradeCommand.cs:128](TradingBot.ApiService/Application/Commands/ExecuteTradeCommand.cs#L128)
- Validate against all risk rules
- Check for existing positions
- Verify daily drawdown limits
- Confirm consecutive loss limits

### Phase 5: Order Execution
[ExecuteTradeCommand.cs:154](TradingBot.ApiService/Application/Commands/ExecuteTradeCommand.cs#L154)
1. Set leverage on Binance
2. Place stop-loss order (StopMarket)
3. Place entry order (Market)
4. Place take-profit orders (Limit)
5. Handle failures with rollback

### Phase 6: Database Logging
[ExecuteTradeCommand.cs:235](TradingBot.ApiService/Application/Commands/ExecuteTradeCommand.cs#L235)
- Save Position record
- Save TradeLog record
- Store all indicator values at entry
- Link Binance order IDs

## API Endpoints

### Trading Endpoints
- `POST /api/trade/execute` - Execute a trade
- `GET /api/trade/analyze/{symbol}` - Analyze market without trading

### Market Endpoints
- `GET /api/market/condition/{symbol}` - Get current market condition

### Backtest Endpoints
- `POST /api/backtest/run` - Run single strategy backtest
- `POST /api/backtest/compare` - Compare multiple strategies

## Technical Indicators Used

### Trend Indicators
- **EMA (9, 21, 50)** - Trend direction and crossovers
- **MACD (12, 26, 9)** - Momentum and trend confirmation
- **Supertrend (10, 3)** - Trailing stop and trend filter

### Momentum Indicators
- **RSI (14)** - Overbought/oversold conditions
- **Volume** - Confirmation of price moves

### Volatility Indicators
- **ATR (14)** - Position sizing and stop-loss
- **Bollinger Bands (20, 2)** - Volatility and squeeze detection

### Market Indicators
- **Funding Rate** - Sentiment and positioning
- **VWAP** - Institutional price levels

## Strategy Comparison

Use the backtest service to compare strategies:

```json
POST /api/backtest/compare
{
  "symbol": "BTCUSDT",
  "strategies": [
    "EMA Momentum Scalper",
    "Bollinger Squeeze",
    "RSI Divergence",
    "VWAP Mean Reversion"
  ],
  "startDate": "2024-01-01T00:00:00Z",
  "endDate": "2024-03-01T00:00:00Z",
  "initialCapital": 10000,
  "riskPercent": 2.5
}
```

Results include:
- Win rate
- Profit factor
- Sharpe ratio
- Max drawdown
- Total return
- Number of trades

## Database Schema

### Position Table
- Tracks all open positions
- Links to Binance order IDs
- Calculates real-time P&L
- Manages trailing stops

### TradeLog Table
- Complete trade history
- Entry/exit prices and times
- All indicator values at entry
- Performance metrics
- Strategy used and reason

### Candle Table
- OHLCV data for all timeframes
- Updated via market data sync
- Used for indicator calculations

## Development Notes

### Adding New Strategies

1. Create class implementing `IStrategy` interface
2. Implement `AnalyzeAsync` method
3. Return `TradingSignal` with confidence
4. Register in [ServiceCollectionExtensions.cs](TradingBot.ApiService/Application/ServiceCollectionExtensions.cs)
5. Add to backtest service strategy mapping

### Risk Management Customization

Edit constants in [RiskManagementService.cs](TradingBot.ApiService/Application/Services/RiskManagementService.cs):
```csharp
private const decimal MinRiskPerTradePercent = 2m;
private const decimal MaxRiskPerTradePercent = 4m;
private const int MaxConsecutiveLosses = 3;
private const decimal MaxDailyDrawdownPercent = 6m;
```

### Indicator Tuning

Modify periods in strategy classes or [TechnicalIndicatorService.cs](TradingBot.ApiService/Application/Services/TechnicalIndicatorService.cs)

## Testing

### Manual Testing
1. Sync market data first
2. Use `/api/trade/analyze/{symbol}` to test signal generation
3. Review logs for indicator values
4. Verify against TradingView

### Backtesting
1. Ensure sufficient historical candle data
2. Run backtest for specific date range
3. Compare multiple strategies
4. Analyze metrics and adjust parameters

## Logging

All phases log to console via Serilog:
- Market analysis results
- Signal detection with confidence
- Position calculations
- Risk validations
- Order execution status
- Errors with full context

Check logs to debug issues or understand decisions.

## Next Steps

1. Add more strategies for comparison
2. Implement position monitoring service
3. Add trailing stop logic
4. Create performance dashboard
5. Add webhook notifications
6. Implement paper trading mode
