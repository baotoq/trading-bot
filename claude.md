# Trading Bot System - Claude Code Documentation

## System Overview

This is a professional crypto trading system built with .NET 10.0, supporting both Binance Futures (short-term scalping) and Spot trading (long-term investing). The system features multiple trading strategies, comprehensive risk management, and thorough validation tools.

## Architecture

### Core Components

1. **Trading Strategies** (Strategy Pattern)

   **Futures Scalping Strategies** (5m timeframe, high frequency):
   - [EmaMomentumScalperStrategy.cs](TradingBot.ApiService/Application/Strategies/EmaMomentumScalperStrategy.cs) - EMA crossover with momentum confirmation
   - [BollingerSqueezeStrategy.cs](TradingBot.ApiService/Application/Strategies/BollingerSqueezeStrategy.cs) - Bollinger Band squeeze + volume breakout
   - [RsiDivergenceStrategy.cs](TradingBot.ApiService/Application/Strategies/RsiDivergenceStrategy.cs) - RSI divergence at support/resistance
   - [VwapMeanReversionStrategy.cs](TradingBot.ApiService/Application/Strategies/VwapMeanReversionStrategy.cs) - VWAP mean reversion

   **BTC Spot Strategies** (4h timeframe, long-term):
   - [BtcSpotDcaStrategy.cs](TradingBot.ApiService/Application/Strategies/BtcSpotDcaStrategy.cs) - Dollar-cost averaging with enhanced dip buying (no stop loss)
   - [BtcSpotTrendStrategy.cs](TradingBot.ApiService/Application/Strategies/BtcSpotTrendStrategy.cs) - Swing trading with trend following (10% stop loss)

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

## BTC Spot Strategies (NEW)

### BTC Spot DCA Strategy
**Purpose:** Long-term BTC accumulation with systematic buying

**Key Features:**
- **Timeframe:** 4h candles
- **Direction:** LONG only (spot trading)
- **Stop Loss:** None (HODL through volatility)
- **Bull Market Filter:** Only buy when BTC > 200 EMA
- **Signal Types:**
  - Regular DCA: Buy in bull market (60% confidence, 10-15% position)
  - Good Buy: 2-3 confirmations (75% confidence, 15-20% position)
  - Strong Buy: 4+ confirmations (95% confidence, 20-25% position)
- **Enhanced Dip Buying:** Extra allocations when RSI < 40 or price at support
- **Exit:** Only on major trend reversal (price drops below 200 EMA)

**Use Case:** Long-term investors who believe in BTC and want to accumulate systematically

### BTC Spot Trend Strategy
**Purpose:** Active swing trading for medium-term gains

**Key Features:**
- **Timeframe:** 4h candles
- **Direction:** LONG only (spot trading)
- **Stop Loss:** 10% (or below swing low)
- **Entry:** EMA(9) x EMA(21) bullish crossover + confirmations
- **Take Profits:** 1.5R (15%), 3R (30%), 5R (50%)
- **Expected Performance:**
  - Win rate: 45-55%
  - Frequency: 2-6 trades/month
  - Risk/Reward: 2.5:1 to 5:1
- **Exit:** Trend reversal (EMA bearish crossover, price < 50 EMA, RSI < 45)

**Use Case:** Active traders who want defined entries/exits with stop losses

### Strategy Combination (Recommended)
- **70% capital:** DCA strategy (long-term holdings, no stop loss)
- **30% capital:** Trend strategy (active trading, with stop loss)
- This provides steady accumulation + active profit-taking

## Strategy Comparison

Use the backtest service to compare strategies:

**Futures Strategies:**
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

**BTC Spot Strategies:**
```json
POST /api/backtest/compare
{
  "symbol": "BTCUSDT",
  "strategies": [
    "BTC Spot DCA",
    "BTC Spot Trend"
  ],
  "startDate": "2024-01-01T00:00:00Z",
  "endDate": "2024-11-30T00:00:00Z",
  "initialCapital": 10000,
  "riskPercent": 2.0
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
4. Register in [ServiceCollectionExtensions.cs:32-40](TradingBot.ApiService/Application/ServiceCollectionExtensions.cs)
5. Add to backtest service strategy mapping in [BacktestService.cs:163-172](TradingBot.ApiService/Application/Services/BacktestService.cs)
6. Specify timeframe (4h for spot/swing, 5m for scalping) in backtest service

**Example: BTC Spot Strategies**
- Strategy classes implement IStrategy interface
- Use 4h candles for long-term analysis
- DCA strategy has no stop loss (spot accumulation)
- Trend strategy has 10% stop loss (swing trading)
- Both use 200 EMA for bull market filter
- Registered as both scoped service and IStrategy

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

## Strategy Validation & Testing

### Comprehensive Validation System (NEW)

**Documentation:**
- **[STRATEGY_VALIDATION_GUIDE.md](STRATEGY_VALIDATION_GUIDE.md)** - Complete validation manual with 6-phase process
- **[StrategyValidation.http](TradingBot.ApiService/StrategyValidation.http)** - 50+ ready-to-use test requests

### The 6-Phase Validation Process

**Phase 1: Backtesting** (Required)
```http
POST /api/backtest/run
{
  "symbol": "BTCUSDT",
  "strategy": "BTC Spot DCA",
  "startDate": "2024-06-01T00:00:00Z",
  "endDate": "2024-11-30T00:00:00Z",
  "initialCapital": 10000,
  "riskPercent": 2.0
}
```

**Phase 2: Metrics Analysis**
- Profit Factor > 1.5 ✅
- Max Drawdown < 25% ✅
- Sharpe Ratio > 1.0 ✅
- Win Rate appropriate for strategy type ✅

**Phase 3: Multiple Market Tests**
- Bull market (Q4 2023 - Q1 2024)
- Bear market (May-Dec 2022)
- Sideways market (Q1-Q3 2023)

**Phase 4: Paper Trading** (2-4 weeks)
```http
POST /realtime/monitor/start
{
  "symbol": "BTCUSDT",
  "interval": "4h",
  "strategy": "BTC Spot DCA",
  "autoTrade": false  // Paper trading mode
}
```

**Phase 5: Small Capital Test** ($100-500)
- Deploy with 1-5% of intended capital
- Monitor for 1-2 weeks
- Verify execution and performance

**Phase 6: Gradual Scale-Up**
- Increase by max 25% per month
- Continue monitoring all metrics

### Key Validation Metrics

**Must Pass Thresholds:**
- **Profit Factor:** > 1.5 (Good), > 2.0 (Excellent)
- **Max Drawdown:** < 25% (Acceptable), < 15% (Good)
- **Sharpe Ratio:** > 1.0 (Good), > 1.5 (Excellent)
- **R:R Ratio:** > 1.5:1 (Minimum), > 2:1 (Good)
- **Minimum Trades:** 20+ trades for statistical validity

### Manual Testing
1. Sync market data first
2. Use `/api/trade/analyze` to test signal generation
3. Review logs for indicator values
4. Compare with TradingView
5. Run paper trading before live

### Backtesting Best Practices
1. Test on minimum 12 months of data
2. Run multiple time period tests (bull/bear/sideways)
3. Compare multiple strategies
4. Analyze trade-by-trade results
5. Never skip validation phases

## Logging

All phases log to console via Serilog:
- Market analysis results
- Signal detection with confidence
- Position calculations
- Risk validations
- Order execution status
- Errors with full context

Check logs to debug issues or understand decisions.

## Recent Updates (December 2024)

### ✅ Completed
1. **BTC Spot Strategies** - Added DCA and Trend strategies for long-term BTC investing
2. **Strategy Validation System** - Comprehensive 6-phase validation guide
3. **Enhanced Backtesting** - Support for 4h timeframes and spot strategies
4. **Paper Trading Mode** - Real-time monitoring without execution (`autoTrade: false`)
5. **Validation Documentation** - Complete guide with metrics and thresholds
6. **Test Suite** - 50+ HTTP test examples for all strategies

### Strategy Portfolio
- **Futures/Scalping:** 4 strategies (EMA Momentum, Bollinger Squeeze, RSI Divergence, VWAP)
- **Spot/Long-term:** 2 strategies (BTC Spot DCA, BTC Spot Trend)
- **Total:** 6 strategies with different timeframes and risk profiles

### Documentation Files
- **[STRATEGY_VALIDATION_GUIDE.md](STRATEGY_VALIDATION_GUIDE.md)** - Complete validation manual
- **[StrategyValidation.http](TradingBot.ApiService/StrategyValidation.http)** - Ready-to-use test requests
- **[TRADING_SYSTEM_GUIDE.md](TRADING_SYSTEM_GUIDE.md)** - System documentation
- **[REALTIME_MONITORING_GUIDE.md](REALTIME_MONITORING_GUIDE.md)** - Real-time monitoring setup
- **[claude.md](claude.md)** - This file (technical documentation)

## Next Steps

### Short-term Improvements
1. ~~Add BTC spot strategies~~ ✅ Completed
2. ~~Implement paper trading mode~~ ✅ Completed
3. ~~Create validation system~~ ✅ Completed
4. Add real-time position monitoring dashboard
5. Implement automated trailing stop adjustments
6. Add multi-symbol support for DCA strategy

### Medium-term Goals
1. Create web dashboard for performance metrics
2. Add webhook notifications for critical events
3. Implement portfolio rebalancing logic
4. Add support for other cryptocurrencies (ETH, SOL)
5. Create automated report generation
6. Add support for multiple exchanges

### Advanced Features
1. Machine learning signal enhancement
2. Sentiment analysis integration
3. On-chain metrics incorporation
4. Dynamic position sizing based on volatility
5. Multi-timeframe confirmation system
6. Advanced portfolio optimization

## Quick Reference

### Getting Started with BTC Spot Trading

1. **Validate Strategy (Required First Step):**
   ```
   Open: TradingBot.ApiService/StrategyValidation.http
   Run: ⭐ TEST 1 (DCA backtest)
   Run: ⭐ TEST 2 (Trend backtest)
   Run: ⭐ TEST 3 (Compare both)
   ```

2. **Check Current Signal:**
   ```http
   POST /api/trade/analyze
   {"symbol": "BTCUSDT", "strategy": "BTC Spot DCA"}
   ```

3. **Start Paper Trading:**
   ```http
   POST /realtime/monitor/start
   {"symbol": "BTCUSDT", "interval": "4h", "strategy": "BTC Spot DCA", "autoTrade": false}
   ```

4. **Review Results:**
   - Read STRATEGY_VALIDATION_GUIDE.md for metric interpretation
   - Compare backtest results against thresholds
   - Paper trade for 2-4 weeks minimum
   - Start with small capital ($100-500)

### Strategy Selection Guide

**Choose DCA Strategy if you want:**
- Long-term BTC accumulation
- No stop losses (HODL mentality)
- Systematic buying with enhanced dip opportunities
- Low maintenance (check weekly)

**Choose Trend Strategy if you want:**
- Active swing trading
- Defined stop losses (10%)
- Medium-term trades (days to weeks)
- Take profits at targets

**Choose Both (70/30 split) if you want:**
- Diversified approach
- Long-term holdings + active trading
- Steady accumulation + profit taking

### Common Commands

**Backtest a strategy:**
```bash
POST /api/backtest/run
{"symbol": "BTCUSDT", "strategy": "BTC Spot DCA", "startDate": "2024-06-01", "endDate": "2024-11-30"}
```

**Compare strategies:**
```bash
POST /api/backtest/compare
{"symbol": "BTCUSDT", "strategies": ["BTC Spot DCA", "BTC Spot Trend"]}
```

**Get current signal:**
```bash
POST /api/trade/analyze
{"symbol": "BTCUSDT", "strategy": "BTC Spot DCA"}
```

**Start monitoring:**
```bash
POST /realtime/monitor/start
{"symbol": "BTCUSDT", "interval": "4h", "strategy": "BTC Spot DCA", "autoTrade": false}
```

### File Locations

**Strategies:**
- Spot: `TradingBot.ApiService/Application/Strategies/BtcSpot*.cs`
- Futures: `TradingBot.ApiService/Application/Strategies/*Strategy.cs`

**Documentation:**
- Validation: `STRATEGY_VALIDATION_GUIDE.md`
- Tests: `TradingBot.ApiService/StrategyValidation.http`
- System: `TRADING_SYSTEM_GUIDE.md`

**Configuration:**
- API Settings: `TradingBot.ApiService/appsettings.json`
- DI Registration: `TradingBot.ApiService/Application/ServiceCollectionExtensions.cs`

---

**Last Updated:** December 2024
**Current Version:** v2.0 (with BTC Spot Strategies & Validation System)
