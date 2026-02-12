# Requirements: BTC Smart DCA Bot

**Version:** v1.0
**Created:** 2026-02-12
**Source:** PROJECT.md + research synthesis

## Functional Requirements

### FR-1: Hyperliquid Spot API Integration
**Priority:** Must Have | **Phase:** 1
**Description:** Build a thin HTTP client for Hyperliquid's REST API with EIP-712 authentication using Nethereum.Signer. Must support: fetching spot prices, getting account balances, placing spot buy orders, and checking order status.
**Acceptance Criteria:**
- Can authenticate with Hyperliquid using ETH private key (EIP-712 signing)
- Can fetch current BTC spot price from Hyperliquid
- Can query USDC balance for the configured wallet
- Can place a spot market buy order on testnet
- Can poll order status and detect fills/partial fills
- Private key never appears in logs

### FR-2: Domain Models and Persistence
**Priority:** Must Have | **Phase:** 1
**Description:** Create domain entities for purchase history, daily price tracking, and DCA configuration. Set up EF Core migrations for PostgreSQL.
**Acceptance Criteria:**
- Purchase entity: timestamp, price, quantity, cost, multiplier, status
- DailyPrice entity: date, open, high, low, close for MA/high calculations
- DcaConfiguration entity or options pattern for all configurable values
- EF Core DbContext with migrations
- Repository/store abstractions following existing codebase patterns

### FR-3: Fix Distributed Lock
**Priority:** Must Have | **Phase:** 1
**Description:** The existing distributed lock implementation is stubbed out (returns success without acquiring). Fix it to use real Dapr/Redis-backed locking before any order execution code is written.
**Acceptance Criteria:**
- Distributed lock acquires real Redis-backed lock via Dapr
- Lock prevents concurrent DCA executions
- Lock has configurable timeout and TTL
- Existing lock interface/abstraction preserved

### FR-4: Daily Recurring Buy Schedule
**Priority:** Must Have | **Phase:** 2
**Description:** Background service that triggers BTC purchase at a configurable time daily, using the existing TimeBackgroundService base class.
**Acceptance Criteria:**
- Extends TimeBackgroundService with configurable interval
- Executes at user-configured time of day (e.g., 08:00 UTC)
- Acquires distributed lock before execution
- Checks if today's purchase already completed (idempotency)
- Gracefully handles app restarts (resumes schedule, doesn't duplicate)
- Logs each trigger with timestamp and decision

### FR-5: Core DCA Execution
**Priority:** Must Have | **Phase:** 2
**Description:** Execute a fixed-amount BTC spot purchase on Hyperliquid. Check balance, place order, handle fills, persist result.
**Acceptance Criteria:**
- Checks USDC balance before placing order
- Skips buy with alert if insufficient balance
- Places IOC (immediate or cancel) spot buy order
- Handles partial fills (stores actual filled quantity)
- Persists purchase record to database
- Publishes domain event on success/failure
- Retries up to 3 times on transient failures

### FR-6: Basic Telegram Notifications
**Priority:** Must Have | **Phase:** 2
**Description:** Send Telegram messages on purchase success, failure, and daily summary. Use existing Telegram infrastructure.
**Acceptance Criteria:**
- Notification on successful buy: price, quantity, cost
- Notification on failed buy: error reason, retry count
- Notification on insufficient balance
- Uses existing TelegramNotificationService pattern
- Triggered via MediatR domain event handlers

### FR-7: 30-Day High Tracking
**Priority:** Must Have | **Phase:** 3
**Description:** Track BTC 30-day rolling high price for drop-from-high multiplier calculation.
**Acceptance Criteria:**
- Fetches daily candle data from Hyperliquid
- Calculates rolling 30-day high from daily high prices
- Caches in Redis with daily refresh
- Falls back to database if Redis unavailable
- Validates data freshness before use (reject if >24h stale)

### FR-8: Dip Multiplier Tiers
**Priority:** Must Have | **Phase:** 3
**Description:** Calculate buy amount multiplier based on % drop from 30-day high. Default tiers: 0-5% = 1x, 5-10% = 1.5x, 10-20% = 2x, 20%+ = 3x.
**Acceptance Criteria:**
- Calculates % drop: (30d_high - current_price) / 30d_high * 100
- Applies correct multiplier based on drop tier
- Tier boundaries and multiplier values are configurable
- Final buy amount = base_amount * multiplier
- Multiplier value stored with each purchase record
- Decision logged: current price, 30d high, % drop, tier, multiplier

### FR-9: 200-Day MA Bear Market Boost
**Priority:** Must Have | **Phase:** 3
**Description:** Apply additional 1.5x multiplier when BTC price is below its 200-day simple moving average.
**Acceptance Criteria:**
- Calculates 200-day SMA from daily close prices
- Applies 1.5x boost when current price < 200-day MA
- Boost stacks with dip tier multiplier (e.g., 2x dip * 1.5x bear = 3x total)
- Bear boost factor is configurable
- Handles insufficient history gracefully (skip boost, log warning)
- MA value stored with each purchase for audit

### FR-10: Configuration Management
**Priority:** Must Have | **Phase:** 1
**Description:** All strategy parameters configurable via appsettings.json / environment variables without code changes.
**Acceptance Criteria:**
- Base daily buy amount (USD)
- Buy schedule (time of day, UTC)
- Multiplier tier boundaries (% drop thresholds)
- Multiplier values per tier
- Bear market boost factor
- Bear market MA period (default: 200)
- 30-day high lookback period
- Hyperliquid API settings (URL, testnet toggle)
- Dry-run mode toggle
- All values validated at startup with clear error messages

### FR-11: Rich Telegram Notifications
**Priority:** Should Have | **Phase:** 4
**Description:** Enhanced notifications showing multiplier reasoning, running totals, and daily/weekly summaries.
**Acceptance Criteria:**
- Buy notification includes: multiplier used, tier explanation, 30d high, current drop %, MA status
- Running totals: total BTC accumulated, total USD spent, average cost basis
- Weekly summary with: buys this week, total spent, average price, best/worst buy
- Formatted with markdown for readability

### FR-12: Health Check and Daily Verification
**Priority:** Should Have | **Phase:** 4
**Description:** Health endpoint and daily verification service to detect silent failures.
**Acceptance Criteria:**
- Health check endpoint reports DCA service status
- Daily verification checks if today's purchase succeeded
- Sends alert if no purchase recorded by end of scheduled window
- Reports service uptime and last successful purchase timestamp

### FR-13: Dry-Run Mode
**Priority:** Should Have | **Phase:** 4
**Description:** Execute all logic except actual order placement. Log and notify what would have happened.
**Acceptance Criteria:**
- Toggle via configuration (DryRun: true/false)
- Runs full calculation pipeline (price fetch, multiplier calc)
- Skips order placement, logs simulated result
- Telegram notification clearly marked as "[DRY RUN]"
- Stores simulated purchases separately for analysis

## Non-Functional Requirements

### NFR-1: Reliability
- Bot must not miss scheduled buys due to transient failures (retry logic)
- Silent failure detection within 1 hour of missed buy window
- Idempotent execution (same day = same result, no duplicates)

### NFR-2: Security
- ETH private key stored in .NET User Secrets or environment variables only
- Private key never logged, serialized, or exposed in any output
- Testnet/mainnet configuration clearly separated

### NFR-3: Observability
- Structured logging (Serilog) for all decisions and executions
- Every buy/skip decision logged with full context
- OpenTelemetry traces for API calls to Hyperliquid

### NFR-4: Maintainability
- Follow existing codebase conventions (PascalCase, file-scoped namespaces, primary constructors)
- Use existing BuildingBlocks patterns (domain events, outbox, background services)
- Interface-based design for testability (IExchangeClient, IPriceDataService, etc.)

## Traceability

| Requirement | Phase | PROJECT.md Source |
|-------------|-------|-------------------|
| FR-1 | 1 | Hyperliquid spot API integration |
| FR-2 | 1 | Purchase history tracking |
| FR-3 | 1 | (Critical pitfall from research) |
| FR-4 | 2 | Configurable daily schedule |
| FR-5 | 2 | Smart DCA engine |
| FR-6 | 2 | Telegram notifications on each buy |
| FR-7 | 3 | Drop-from-high calculation |
| FR-8 | 3 | Drop-from-high tier-based multipliers |
| FR-9 | 3 | 200-day MA bear market boost |
| FR-10 | 1 | Configuration management |
| FR-11 | 4 | Telegram notifications (enhanced) |
| FR-12 | 4 | Detailed logging of all decisions |
| FR-13 | 4 | (Research recommendation: dry-run) |

---
*Requirements defined: 2026-02-12*
