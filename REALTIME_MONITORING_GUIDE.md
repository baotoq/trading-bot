# Real-Time Monitoring & Telegram Notifications Guide

This guide explains how to set up and use the real-time Binance candle monitoring with automatic signal generation and Telegram notifications.

## Features

- **Real-time Candle Monitoring**: Connect to Binance WebSocket API for live candle data
- **Automatic Signal Generation**: Run trading strategies on incoming candles
- **Telegram Notifications**: Receive instant trading signals via Telegram
- **Multiple Symbol Support**: Monitor multiple trading pairs simultaneously
- **Configurable Strategies**: Choose which strategy to use for each symbol

## Setup

### 1. Create a Telegram Bot

1. Open Telegram and search for `@BotFather`
2. Send `/newbot` command
3. Follow the prompts to create your bot
4. Copy the **Bot Token** (looks like: `123456789:ABCdefGHIjklMNOpqrsTUVwxyz`)

### 2. Get Your Chat ID

1. Search for `@userinfobot` in Telegram
2. Start a conversation
3. Copy your **Chat ID** (numeric value)

Alternatively, you can:
1. Start a conversation with your bot
2. Send any message
3. Visit: `https://api.telegram.org/bot<YourBOTToken>/getUpdates`
4. Look for `"chat":{"id":123456789}` - that's your Chat ID

### 3. Configure the Application

Edit `appsettings.json` or `appsettings.Development.json`:

```json
{
  "Telegram": {
    "BotToken": "YOUR_BOT_TOKEN_HERE",
    "ChatId": "YOUR_CHAT_ID_HERE"
  },
  "Binance": {
    "ApiKey": "YOUR_BINANCE_API_KEY",
    "ApiSecret": "YOUR_BINANCE_API_SECRET",
    "TestMode": false
  }
}
```

**Note**: For production, use User Secrets or environment variables instead of storing sensitive data in appsettings.json.

Using User Secrets:
```bash
dotnet user-secrets set "Telegram:BotToken" "YOUR_BOT_TOKEN"
dotnet user-secrets set "Telegram:ChatId" "YOUR_CHAT_ID"
dotnet user-secrets set "Binance:ApiKey" "YOUR_API_KEY"
dotnet user-secrets set "Binance:ApiSecret" "YOUR_API_SECRET"
```

## API Endpoints

### Start Monitoring

Start real-time monitoring for a symbol with signal notifications:

**POST** `/api/realtime/start`

```json
{
  "symbol": "BTCUSDT",
  "interval": "5m",
  "strategy": "EmaMomentumScalper"
}
```

**Parameters:**
- `symbol` (required): Trading pair (e.g., "BTCUSDT", "ETHUSDT")
- `interval` (optional, default: "5m"): Candle interval ("1m", "3m", "5m", "15m", "30m", "1h", "4h", "1d")
- `strategy` (optional, default: "EmaMomentumScalper"): Strategy to use for signal generation

**Response:**
```json
{
  "success": true,
  "message": "Started monitoring BTCUSDT on 5m interval with EmaMomentumScalper strategy",
  "symbol": "BTCUSDT",
  "interval": "5m",
  "strategy": "EmaMomentumScalper"
}
```

### Stop Monitoring

Stop monitoring a symbol:

**POST** `/api/realtime/stop`

```json
{
  "symbol": "BTCUSDT",
  "interval": "5m"
}
```

### Get Monitoring Status

Check all active monitoring sessions:

**GET** `/api/realtime/status`

**Response:**
```json
{
  "totalActiveMonitors": 2,
  "monitors": [
    {
      "symbol": "BTCUSDT",
      "interval": "5m",
      "isMonitoring": true,
      "isNotificationEnabled": true,
      "strategy": "EmaMomentumScalper"
    },
    {
      "symbol": "ETHUSDT",
      "interval": "5m",
      "isMonitoring": true,
      "isNotificationEnabled": true,
      "strategy": "EmaMomentumScalper"
    }
  ]
}
```

### Test Telegram

Send a test message to verify Telegram is configured correctly:

**POST** `/api/realtime/test-telegram`

## How It Works

### 1. WebSocket Connection
When you start monitoring a symbol, the service:
- Establishes a WebSocket connection to Binance
- Subscribes to kline (candle) updates for the specified symbol and interval
- Receives real-time candle data as it forms

### 2. Candle Storage
Each completed candle is:
- Stored in the database
- Made available for technical analysis
- Used to maintain historical data

### 3. Signal Generation
When a candle completes:
- The configured strategy runs analysis on the latest data
- Technical indicators are calculated (EMA, RSI, MACD, Bollinger Bands, etc.)
- A trading signal is generated (Buy, Sell, StrongBuy, StrongSell, or Hold)

### 4. Telegram Notification
If a signal is actionable (not Hold):
- A formatted message is sent to your Telegram chat
- Includes signal type, confidence level, price, indicators, and reasoning
- Has a 5-minute cooldown to prevent spam

## Telegram Notification Format

```
🚀 Trading Signal Detected 🚀

Symbol: BTCUSDT
Signal: STRONG BUY
Strategy: EMA Momentum Scalper
Price: $45,250.00
Confidence: 95.0%

📊 Indicators:
  • RSI: 65.42
  • EMA 9: $45,100.25
  • EMA 21: $44,850.50
  • MACD: 0.0245

🛡️ Risk Management:
  • Entry: $45,250.00
  • Stop Loss: $44,800.00
  • Risk: $450.00 (0.99%)
  • TP1: $45,925.00 (1.5R)
  • TP2: $46,375.00 (2.5R)
  • TP3: $47,050.00 (4.0R)

📝 Reason:
✓ Bullish EMA crossover | ✓ MACD bullish | ✓ RSI momentum confirmed (65.42) | ✓ High volume (1.85x avg) | ✓ Breakout above swing high ($45,100.00) | ✓ Price above Bollinger middle band

⏰ 2025-12-03 12:34:56 UTC
```

## Usage Examples

### Example 1: Monitor Bitcoin on 5-minute chart

```bash
curl -X POST http://localhost:5000/api/realtime/start \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "BTCUSDT",
    "interval": "5m",
    "strategy": "EmaMomentumScalper"
  }'
```

### Example 2: Monitor Multiple Symbols

Monitor Bitcoin, Ethereum, and Solana:

```bash
# Bitcoin
curl -X POST http://localhost:5000/api/realtime/start \
  -H "Content-Type: application/json" \
  -d '{"symbol": "BTCUSDT", "interval": "5m"}'

# Ethereum
curl -X POST http://localhost:5000/api/realtime/start \
  -H "Content-Type: application/json" \
  -d '{"symbol": "ETHUSDT", "interval": "5m"}'

# Solana
curl -X POST http://localhost:5000/api/realtime/start \
  -H "Content-Type: application/json" \
  -d '{"symbol": "SOLUSDT", "interval": "5m"}'
```

### Example 3: Check Status

```bash
curl http://localhost:5000/api/realtime/status
```

### Example 4: Stop Monitoring

```bash
curl -X POST http://localhost:5000/api/realtime/stop \
  -H "Content-Type: application/json" \
  -d '{
    "symbol": "BTCUSDT",
    "interval": "5m"
  }'
```

### Example 5: Test Telegram

```bash
curl -X POST http://localhost:5000/api/realtime/test-telegram
```

## Signal Cooldown

To prevent notification spam, the system implements a 5-minute cooldown:
- If the same signal type is generated within 5 minutes, it won't be sent again
- Different signal types (e.g., changing from BUY to SELL) are always sent
- This ensures you receive timely updates without being overwhelmed

## Available Strategies

Currently available strategies:
- **EmaMomentumScalper**: EMA-based momentum strategy with multiple confirmations
  - Uses 9 and 21 EMAs for trend detection
  - RSI for momentum confirmation
  - MACD for trend strength
  - Volume and Bollinger Bands for additional confirmation

More strategies can be added by implementing the `IStrategy` interface.

## Risk Management

All signals include comprehensive risk management levels:

### Stop Loss Calculation
- **Long Positions**: Placed below the swing low or 2% below entry (whichever is closer to current price)
- **Short Positions**: Placed above recent swing or 2% above entry (whichever is closer to current price)

### Take Profit Levels
Three take profit targets are calculated using risk:reward ratios:
- **TP1**: 1.5R (1.5x the risk amount)
- **TP2**: 2.5R (2.5x the risk amount)
- **TP3**: 4.0R (4.0x the risk amount)

### Example
If entry is $45,250 and stop loss is $44,800:
- Risk = $450 (0.99%)
- TP1 = $45,250 + ($450 × 1.5) = $45,925 (1.5R)
- TP2 = $45,250 + ($450 × 2.5) = $46,375 (2.5R)
- TP3 = $45,250 + ($450 × 4.0) = $47,050 (4.0R)

### Position Sizing Recommendation
While the signal provides entry, SL, and TP levels, you should calculate your position size based on:
- Your account equity
- Maximum risk per trade (typically 1-2% of account)
- Distance from entry to stop loss

**Formula**: Position Size = (Account Equity × Risk %) / (Entry Price - Stop Loss)

## Troubleshooting

### Not Receiving Telegram Notifications

1. **Verify Configuration**: Check that BotToken and ChatId are correct
2. **Test Connection**: Use the `/api/realtime/test-telegram` endpoint
3. **Check Logs**: Look for Telegram-related errors in application logs
4. **Bot Permissions**: Make sure you've started a conversation with your bot

### WebSocket Connection Issues

1. **Check Binance Status**: Visit https://www.binance.com/en/support/announcement
2. **Network**: Ensure your server can access Binance WebSocket endpoints
3. **Symbol Format**: Use correct symbol format (e.g., "BTCUSDT" not "BTC/USDT")

### No Signals Being Generated

1. **Check Strategy Requirements**: EmaMomentumScalper needs significant price movement
2. **Insufficient Data**: Ensure enough historical candles exist in database
3. **Market Conditions**: Strategy may not generate signals in ranging/choppy markets
4. **Cooldown Period**: Check if signals are being suppressed due to cooldown

## Best Practices

1. **Start with One Symbol**: Test with a single symbol before scaling up
2. **Use Appropriate Intervals**: 5m or 15m work well for most scalping strategies
3. **Monitor Resource Usage**: Each WebSocket connection uses resources
4. **Historical Data**: Sync historical data first using existing sync endpoints
5. **Test Mode**: Use Binance testnet for testing before going live
6. **Secure Credentials**: Never commit API keys or bot tokens to version control

## Next Steps

- Implement additional strategies
- Add position size recommendations
- Create alerts for specific conditions
- Build a dashboard for monitoring
- Add support for stop-loss and take-profit automation

## Security Notes

⚠️ **Important Security Considerations:**

1. **Never share your Bot Token** - It gives full access to your bot
2. **Keep Chat ID private** - Anyone with it can send messages to you
3. **Use User Secrets** - Don't store credentials in appsettings.json in production
4. **Binance API Permissions** - Only enable necessary permissions (read/trade, no withdrawal)
5. **Rate Limiting** - Binance has rate limits; monitor your usage
6. **Testnet First** - Always test with Binance testnet before using real funds

## Support

For issues or questions:
- Check application logs for detailed error messages
- Review this guide for common solutions
- Ensure all prerequisites are met
