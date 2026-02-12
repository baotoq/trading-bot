# Project Research Summary

**Project:** BTC Smart DCA Bot (Hyperliquid)
**Domain:** Recurring cryptocurrency buy bot with smart multipliers
**Researched:** 2026-02-12
**Confidence:** MEDIUM

## Executive Summary

This project builds a **daily BTC spot accumulation bot** on Hyperliquid that enhances basic dollar-cost averaging with dip-buying multipliers (tiered by % drop from 30-day high) and a bear-market boost (1.5x when price is below the 200-day MA). The existing .NET 10.0 codebase provides strong infrastructure -- TimeBackgroundService for scheduling, MediatR domain events with transactional outbox, distributed locking, PostgreSQL/Redis, and Telegram notifications -- so the majority of new work is the Hyperliquid API client and the DCA-specific business logic.

The recommended approach is to build a thin Hyperliquid HTTP client using `HttpClient` + `Nethereum.Signer` for EIP-712 authentication (no viable community SDK exists), then layer the DCA execution engine on top using the existing BuildingBlocks infrastructure. The bot is intentionally simple: BTC-only, buy-only, no sell/stop-loss logic, no web dashboard. This scope discipline is critical to shipping quickly.

The highest-risk area is Hyperliquid integration. It uses EIP-712 typed data signing (not standard API keys), its spot API is less documented than its perps API, and the existing distributed lock implementation is **stubbed out** (`return new();`) -- meaning duplicate order protection is currently broken. These must be resolved before any real money touches the system. The second risk is silent failure: if the bot fails to buy for days without alerting, the user misses dip opportunities entirely.

## Key Findings

### Recommended Stack

No official .NET SDK for Hyperliquid exists. Build a direct HTTP client.

**Core technologies:**
- **Nethereum.Signer** -- EIP-712 typed data signing required for all Hyperliquid exchange actions
- **HttpClient + IHttpClientFactory** -- Direct REST calls to Hyperliquid info/exchange endpoints
- **Polly (via Aspire)** -- Retry and circuit breaker policies for API resilience
- **PostgreSQL + EF Core** -- Purchase history, daily price cache, outbox messages
- **Redis** -- Distributed cache for 30-day high and 200-day MA values

**Remove later:** `Binance.Net` and `CryptoExchange.Net` packages are no longer needed.

### Expected Features

**Must have (table stakes):**
- Configurable daily recurring buy at fixed base amount
- Automatic order execution via Hyperliquid spot API
- Purchase history tracking (timestamp, price, quantity, cost)
- Balance checking before order placement
- Basic Telegram notifications on buy/failure
- Error handling with retry and failure alerts

**Should have (differentiators):**
- Dip multiplier tiers: 0-5% drop = 1x, 5-10% = 1.5x, 10-20% = 2x, 20%+ = 3x
- 200-day MA bear market boost (1.5x additional multiplier)
- Rich Telegram messages with multiplier reasoning and running totals
- Dry-run mode for testing without real orders
- Configurable tier boundaries and multipliers

**Defer (v2+):**
- Backtesting engine, web dashboard, multi-asset support, sell logic

### Architecture Approach

Layered event-driven architecture using existing BuildingBlocks. DcaSchedulerBackgroundService triggers daily, acquires a distributed lock, then orchestrates: PriceDataService fetches current price + historical data, MultiplierCalculator computes the buy amount, HyperliquidApiClient executes the spot order, and domain events flow through the outbox to Telegram handlers.

**Major components:**
1. **DcaSchedulerBackgroundService** -- TimeBackgroundService subclass, daily trigger with distributed lock
2. **DcaExecutionService** -- Orchestrates the full buy cycle (price analysis -> multiplier -> order -> persist)
3. **PriceDataService** -- Fetches and caches BTC price data, calculates 30-day high and 200-day MA
4. **MultiplierCalculator** -- Pure stateless logic for drop-from-high tiers and bear boost
5. **HyperliquidApiClient** -- Thin HTTP client behind IExchangeClient interface, handles EIP-712 signing
6. **Domain Event Handlers** -- React to BuyOrderExecuted/Failed events for Telegram notifications

### Critical Pitfalls

1. **Duplicate order execution** -- Distributed lock is currently stubbed. Must implement real Redis-backed locking AND database-level idempotency (check if today's purchase already exists before every attempt).
2. **Silent failure for days** -- Background service swallows exceptions. Must implement multi-channel alerting plus a daily verification service that checks if today's purchase succeeded.
3. **Price data staleness** -- 200-day MA calculated from stale cache leads to wrong multipliers. Always fetch fresh candles before making buy decisions, validate timestamps.
4. **Partial fill mishandling** -- Spot orders may not fill completely. Must poll order status, store actual executed quantity (not requested), handle partial fills explicitly.
5. **API key exposure in logs** -- EIP-712 private key must never appear in Serilog output. Implement log redaction before writing any API integration code.

## Implications for Roadmap

### Phase 1: Foundation and Hyperliquid Client
**Rationale:** Everything depends on being able to talk to Hyperliquid. EIP-712 signing is non-trivial and must be validated against testnet before anything else.
**Delivers:** Working HyperliquidApiClient that can fetch prices, get balances, and place spot orders on testnet.
**Addresses:** Exchange API integration (table stakes), configuration management
**Avoids:** API key exposure (Pitfall 12), rate limit cascade (Pitfall 5)
**Includes:** Domain models (Purchase, DailyPrice, value objects), EF Core migrations, fix distributed lock stub

### Phase 2: Core DCA Engine
**Rationale:** Once API works, build the simplest end-to-end buy flow: schedule -> buy fixed amount -> persist -> notify.
**Delivers:** Bot that buys a fixed USD amount of BTC daily and sends a Telegram confirmation.
**Addresses:** Recurring schedule, automatic execution, purchase history, basic notifications, balance checking
**Avoids:** Duplicate orders (Pitfall 1), silent failures (Pitfall 2), partial fills (Pitfall 4), insufficient balance (Pitfall 8)

### Phase 3: Smart Multipliers
**Rationale:** After basic DCA works reliably, layer on the intelligence: dip-buying tiers and bear market boost.
**Delivers:** Multiplier-enhanced purchases that buy more during dips and bear markets.
**Addresses:** 30-day high tracking, drop tier multipliers, 200-day MA bear boost, multiplier tracking in history
**Avoids:** Price data staleness (Pitfall 3), incorrect drop calculation (Pitfall 6)

### Phase 4: Enhanced Notifications and Observability
**Rationale:** With smart DCA working, enrich notifications to show reasoning and add operational monitoring.
**Delivers:** Rich Telegram messages with multiplier explanation and running totals, health check endpoint, daily verification service.
**Addresses:** Rich notifications, running totals, decision logging, dry-run mode
**Avoids:** Verbose notifications (Pitfall 10), outbox accumulation (Pitfall 9)

### Phase Ordering Rationale

- Phases follow a strict dependency chain: API client -> basic buy -> smart buy -> observability
- Phase 1 isolates the highest-risk work (Hyperliquid integration) so failures are caught early
- Phase 2 delivers user value (working bot) as fast as possible -- even without multipliers, a daily fixed buy is useful
- Phase 3 is additive -- the bot works without multipliers, so this can be developed without breaking existing functionality
- Phase 4 is polish -- the bot can operate with basic notifications before rich ones are added

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 1:** Hyperliquid EIP-712 signing specifics, spot token indices, exact order request format, testnet setup. This is the least documented area.
- **Phase 2:** Order status polling behavior, partial fill handling on Hyperliquid spot specifically.

Phases with standard patterns (skip research):
- **Phase 3:** Multiplier calculation is pure math with well-defined inputs. 30-day high and 200-day MA are standard calculations.
- **Phase 4:** Telegram formatting, health checks, outbox cleanup are all established .NET patterns already in the codebase.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Nethereum is mature, HttpClient approach is proven, no viable alternatives |
| Features | MEDIUM | DCA patterns well understood, but no web search to validate Hyperliquid-specific capabilities |
| Architecture | HIGH | Leverages existing BuildingBlocks infrastructure with clear component boundaries |
| Pitfalls | HIGH for general patterns, LOW for Hyperliquid-specific | Exchange integration pitfalls are universal; Hyperliquid quirks need API doc validation |

**Overall confidence:** MEDIUM -- the architecture and DCA logic are solid, but Hyperliquid API specifics are the key unknown.

### Gaps to Address

- **Hyperliquid spot BTC availability:** Must confirm BTC spot trading is supported (not just perps)
- **EIP-712 signing format:** Exact typed data structure for spot orders needs Hyperliquid documentation verification
- **Minimum order sizes:** Unknown for Hyperliquid spot; must discover before setting base amounts
- **USDC requirement:** Hyperliquid spot settles in USDC; user must maintain USDC balance, not USDT
- **Distributed lock stub:** Current implementation is bypassed (`return new();`); must be fixed with real Redis/Dapr lock before Phase 2
- **Price history bootstrap:** Need 200+ days of daily candle data for MA calculation; Hyperliquid may not have that much history for spot markets
- **Authentication:** STACK.md confirms EIP-712, but PITFALLS.md mentions HMAC-SHA256 uncertainty -- EIP-712 is correct per API documentation structure

## Sources

### Primary (HIGH confidence)
- Existing codebase analysis -- BuildingBlocks infrastructure, TimeBackgroundService, outbox pattern, domain events
- Hyperliquid API structure -- REST endpoints, WebSocket, EIP-712 signing pattern

### Secondary (MEDIUM confidence)
- DCA bot feature patterns -- Training knowledge of recurring buy implementations
- Exchange integration patterns -- Common across Binance, Bybit, and similar exchanges

### Tertiary (LOW confidence)
- Hyperliquid spot API specifics -- Limited public documentation, needs verification against official docs
- Community SDK viability -- Assumed insufficient based on general assessment, not verified

---
*Research completed: 2026-02-12*
*Ready for roadmap: yes*
