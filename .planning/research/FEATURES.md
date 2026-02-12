# Feature Landscape: Smart DCA Bot

**Domain:** Recurring cryptocurrency buy bot (DCA strategy)
**Researched:** 2026-02-12
**Confidence:** MEDIUM (based on training knowledge of DCA bot patterns, no web search access)

## Table Stakes

Features users expect from ANY DCA bot. Missing these = bot is unusable or feels incomplete.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| **Configurable recurring schedule** | Core DCA concept - must buy on regular intervals | Low | Daily/weekly/monthly common. Project specifies daily + time-of-day |
| **Fixed base buy amount** | User needs predictable spending | Low | Denominated in USD/stablecoin, not BTC |
| **Automatic order execution** | Manual execution defeats DCA purpose | Medium | Requires reliable background scheduler + exchange API |
| **Purchase history tracking** | User must see what was bought and when | Low | Store: timestamp, amount, price, total cost |
| **Basic notifications** | User needs confirmation buys happened | Low | Telegram or email. Project specifies Telegram |
| **Exchange API integration** | Cannot buy without exchange connection | High | Project specifies Hyperliquid - need research on their API |
| **Error handling & retry** | Network/exchange failures will happen | Medium | Retry logic, exponential backoff, alert on persistent failure |
| **Configuration management** | Must adjust strategy without code changes | Low | Base amount, schedule, API credentials |
| **Transaction logging** | Audit trail for tax and verification | Low | Detailed logs of every decision and execution |
| **Balance checking** | Don't attempt buys with insufficient funds | Low | Check account balance before order placement |

## Differentiators

Features that make this a SMART DCA bot, not just basic recurring buys. These justify building custom vs using exchange native DCA.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| **Dip multipliers (% drop tiers)** | Buy more during dips = better average cost | Medium | Project specifies 1x/1.5x/2x/3x based on drop from 30-day high |
| **30-day high tracking** | Reference point for "dip" calculation | Low | Rolling window calculation, update daily |
| **Multiple tier system** | Graduated response to different dip sizes | Low | 4 tiers specified: 0-5% (1x), 5-10% (1.5x), 10-20% (2x), 20%+ (3x) |
| **200-day MA bear boost** | Extra aggression in macro downtrends | Medium | Requires 200 days of price history, 1.5x multiplier when below MA |
| **Rich Telegram notifications** | Full context on each buy (multipliers used, running totals, reasoning) | Medium | Beyond basic "buy executed" - explain WHY this amount |
| **Multiplier tracking in history** | Know which buys were enhanced by strategy | Low | Store multiplier used with each purchase |
| **Running totals in notifications** | Total BTC accumulated, average cost basis | Low | Aggregate metrics sent with each buy notification |
| **Detailed decision logging** | Full transparency into strategy logic | Low | Log: current price, 30d high, % drop, MA position, multipliers applied |
| **Dry-run mode** | Test strategy without spending money | Low | Execute logic, skip order placement, log what WOULD happen |
| **Configurable multiplier tiers** | User can tune aggressiveness | Low | Don't hardcode tier boundaries or multipliers |

## Anti-Features

Features to explicitly NOT build. Common in general trading bots but inappropriate for a simple recurring buy bot.

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| **Sell/take-profit logic** | This is accumulation-only, not trading | Acknowledge this is buy-and-hold strategy |
| **Stop-loss functionality** | DCA is inherently long-term, selling defeats purpose | Trust in dip-buying to lower average cost |
| **Monthly spending caps** | Daily amount + multipliers provide sufficient control | Let user adjust base amount if spending too much |
| **Multiple asset support** | BTC-only keeps implementation simple | Focus on doing one thing well |
| **Futures/perps trading** | Spot accumulation only - no leverage risk | Use spot market exclusively |
| **Web dashboard** | Logs + Telegram sufficient for monitoring | Avoid scope creep, premature UI work |
| **Backtesting engine** | Build working bot first, validate later | Note as future improvement, not MVP |
| **Portfolio rebalancing** | Single asset = no rebalancing needed | N/A |
| **Market timing/pause logic** | Defeats DCA purpose ("time in market > timing market") | Buy every scheduled interval regardless of conditions |
| **Variable schedules** | Adds complexity without clear benefit | Fixed daily schedule is sufficient |
| **Multiple exchange support** | Hyperliquid-only keeps API integration simple | Document as possible future enhancement |
| **Manual buy triggers** | Bot should be fully autonomous | Scheduled buys only (user can adjust schedule) |

## Feature Dependencies

```
Core Purchase Flow:
  Scheduled Trigger → Balance Check → Order Execution → Transaction Logging → Notification

Smart DCA Enhancement Layer:
  Price Data Fetching → 30-Day High Calculation → Drop % Calculation → Tier Determination
                    ↓
                200-Day MA Calculation → Bear Market Check → Bear Boost Application
                    ↓
              Final Multiplier → Adjusted Buy Amount → Order Execution

Historical Data Requirements:
  30-day high tracking requires: 30+ days of daily price data
  200-day MA requires: 200+ days of daily close prices
  Running totals require: Persistent purchase history
```

**Critical Path Dependencies:**
1. **Exchange API integration** blocks everything (cannot execute without API)
2. **Price data fetching** blocks smart DCA features (multipliers need current price + history)
3. **Purchase history** must exist before running totals can be calculated
4. **Configuration system** should be built early (everything depends on settings)

**Suggested Build Order:**
1. Configuration management (base amount, schedule, API keys)
2. Hyperliquid API integration (authentication, market data, order placement)
3. Basic DCA (scheduled trigger, fixed amount buy, transaction logging)
4. Notifications (Telegram alerts for each buy)
5. Price history tracking (30-day high, 200-day MA)
6. Smart DCA multipliers (dip tiers + bear boost)
7. Enhanced notifications (multipliers, reasoning, running totals)
8. Dry-run mode

## MVP Recommendation

**For MVP, prioritize (in order):**

1. **Configuration management** - Base amount, daily schedule, Hyperliquid credentials
2. **Hyperliquid API basics** - Authentication, account balance, market data, spot order placement
3. **Background scheduler** - Daily execution using existing TimeBackgroundService pattern
4. **Fixed DCA execution** - Buy base amount at scheduled time (no multipliers yet)
5. **Transaction logging** - Persist every purchase (timestamp, amount, price, cost)
6. **Basic Telegram alerts** - "Bought X BTC at Y price for Z USD"
7. **Error handling** - Retry logic, failure alerts, graceful degradation

**This creates a functional recurring buy bot.** User gets value immediately.

**Post-MVP (smart features):**

8. **Price history tracking** - Fetch and store daily BTC prices
9. **30-day high calculation** - Rolling window, updated daily
10. **Dip tier multipliers** - 4 tiers based on % drop from high
11. **200-day MA calculation** - Requires 200+ days of history
12. **Bear market boost** - 1.5x multiplier when price < 200 MA
13. **Enhanced notifications** - Explain multipliers used, show running totals
14. **Dry-run mode** - Test without executing orders

**This transforms it into a smart DCA bot.** User gets better average cost.

**Defer to later:**

- Backtesting (validate strategy after collecting real data)
- Web dashboard (logs + Telegram sufficient for v1)
- Multi-asset support (BTC-only is enough)
- Advanced analytics (focus on core functionality first)

## Complexity Assessment

| Feature Category | Overall Complexity | Blockers/Risks |
|------------------|-------------------|----------------|
| **Basic DCA** | Low-Medium | Hyperliquid API research needed (new exchange) |
| **Smart Multipliers** | Low | Straightforward calculations, main challenge is data pipeline |
| **Price History** | Medium | Need reliable data source, persistent storage, daily updates |
| **Notifications** | Low | Infrastructure already exists (Telegram service) |
| **Scheduling** | Low | Existing TimeBackgroundService pattern handles this |
| **Configuration** | Low | Standard .NET configuration system |

**Highest Risk Items:**
1. **Hyperliquid API** - New exchange, need to research REST + WebSocket APIs, authentication, rate limits
2. **Price history bootstrap** - Need to backfill 200+ days for MA calculation
3. **Reliable scheduling** - Must execute daily even if app restarts (persist schedule state)

## Feature Sizing Estimates

**Small (1-2 days):**
- Configuration management
- Basic Telegram notifications
- Transaction logging
- 30-day high calculation (once data available)
- Dip tier logic
- Dry-run mode

**Medium (3-5 days):**
- Hyperliquid API integration (research + implementation)
- Background scheduler with persistence
- Price history tracking system
- Error handling & retry logic
- 200-day MA calculation
- Enhanced notifications with context

**Large (5-10 days):**
- Complete price data pipeline (fetch, store, update, backfill)
- Full smart DCA system (all multipliers working together)
- Comprehensive testing & validation

## Open Questions for Implementation

1. **Hyperliquid API specifics:**
   - Authentication method (API key? JWT?)
   - Rate limits for market data and order placement
   - WebSocket vs REST for price data
   - Spot trading endpoints (different from perps?)

2. **Price data source:**
   - Use Hyperliquid's own historical data API?
   - Or external source (CoinGecko, CoinMarketCap) for 200-day history?
   - How to backfill initial 200 days?

3. **Scheduling persistence:**
   - What happens if app restarts mid-day?
   - Track "last buy timestamp" to prevent double-buys?
   - Or use idempotency keys?

4. **Configuration flexibility:**
   - Should tier boundaries be configurable (5%, 10%, 20%)?
   - Should multipliers be configurable (1x, 1.5x, 2x, 3x)?
   - Should bear boost be togglable?

5. **Failure scenarios:**
   - What if Hyperliquid is down at scheduled time?
   - Retry same day? Skip and wait for next day?
   - Alert user immediately or batch daily summary?

## Sources

**Confidence Note:** This analysis is based on training knowledge of DCA bot patterns and examination of the existing codebase structure. No web search was available to verify current market practices or Hyperliquid-specific features.

**Recommendations:**
- Research Hyperliquid API documentation before implementation
- Survey existing Hyperliquid trading bots (if any) for common patterns
- Validate that multiplier tiers (1x/1.5x/2x/3x) are reasonable for BTC volatility
- Consider user testing of notification format (too much vs too little info)

**Training knowledge sources:**
- General DCA bot feature patterns from crypto trading space
- Common recurring buy implementations (exchange native + third-party)
- Smart DCA concepts (dip buying, moving average filters)
- Trading bot notification best practices

**Codebase examination:**
- Existing TimeBackgroundService pattern for scheduling
- Event-driven architecture with MediatR
- Telegram notification infrastructure
- Outbox pattern for reliable messaging
