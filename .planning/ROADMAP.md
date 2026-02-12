# Roadmap: BTC Smart DCA Bot

**Milestone:** v1.0 — Daily BTC Smart DCA on Hyperliquid
**Created:** 2026-02-12
**Phases:** 4

## Phase 1: Foundation & Hyperliquid Client

**Goal:** Working Hyperliquid API client that can fetch prices, get balances, and place spot orders on testnet. Domain models and persistence layer ready. Configuration system in place.

**Requirements:** FR-1, FR-2, FR-3, FR-10

**Why first:** Everything downstream depends on being able to talk to Hyperliquid. EIP-712 signing is the highest-risk unknown — validate it early against testnet.

**Success criteria:**
- [ ] HyperliquidClient can authenticate and place a spot buy on testnet
- [ ] Domain models (Purchase, DailyPrice) persisted via EF Core
- [ ] Distributed lock uses real Redis/Dapr locking (not stub)
- [ ] All DCA configuration loads from appsettings.json
- [ ] Private key never appears in any log output

**Research needed:** Yes — EIP-712 signing specifics, spot token indices, exact order format

**Plans:** 2 plans

Plans:

- [ ] 01-01-PLAN.md — Configuration, domain models, persistence, and distributed lock foundation
- [ ] 01-02-PLAN.md — Hyperliquid API client with EIP-712 signing and testnet verification

---

## Phase 2: Core DCA Engine

**Goal:** Bot buys a fixed USD amount of BTC daily on Hyperliquid, persists the result, and sends a Telegram confirmation. End-to-end working buy flow.

**Requirements:** FR-4, FR-5, FR-6

**Why second:** Delivers immediate user value. Even without smart multipliers, a reliable daily fixed-amount buy is useful. Also validates the full pipeline: schedule → lock → balance check → order → persist → notify.

**Success criteria:**
- [ ] DcaSchedulerBackgroundService triggers daily at configured time
- [ ] Bot buys configured base amount of BTC on Hyperliquid spot
- [ ] Purchase persisted with all metadata (price, qty, cost, status)
- [ ] Telegram notification sent on success and failure
- [ ] Idempotent: no duplicate purchases on same day
- [ ] Handles partial fills correctly
- [ ] Retries transient failures up to 3 times

**Research needed:** Yes — order status polling, partial fill behavior on Hyperliquid spot

---

## Phase 3: Smart Multipliers

**Goal:** Bot adjusts buy amount based on dip severity (30-day high tiers) and bear market conditions (200-day MA), buying more aggressively during dips and downtrends.

**Requirements:** FR-7, FR-8, FR-9

**Why third:** Additive enhancement — the bot already works without multipliers. This layers on intelligence without breaking existing functionality. Pure calculation logic, lower risk.

**Success criteria:**
- [ ] 30-day high tracked from daily candle data
- [ ] Dip tier multiplier correctly calculated (1x / 1.5x / 2x / 3x)
- [ ] 200-day MA calculated from historical daily closes
- [ ] Bear boost (1.5x) applied when price < 200-day MA
- [ ] Multipliers stack correctly (e.g., 2x dip * 1.5x bear = 3x)
- [ ] Each purchase records: multiplier, tier, 30d high, MA value, drop %
- [ ] Stale data rejected (>24h old triggers fresh fetch)

**Research needed:** No — standard calculations, well-defined inputs

---

## Phase 4: Enhanced Notifications & Observability

**Goal:** Rich Telegram messages explaining buy reasoning, running totals, health monitoring, and dry-run mode for testing.

**Requirements:** FR-11, FR-12, FR-13

**Why last:** Polish layer. The bot operates correctly with basic notifications. This phase adds operational confidence and testing capability.

**Success criteria:**
- [ ] Telegram buy messages include: multiplier reasoning, 30d high, drop %, MA status
- [ ] Running totals: total BTC, total USD spent, average cost
- [ ] Weekly summary notification
- [ ] Health check endpoint with DCA service status
- [ ] Daily verification detects missed purchases
- [ ] Dry-run mode executes full logic without placing orders
- [ ] Dry-run notifications marked clearly as simulated

**Research needed:** No — existing Telegram and health check patterns in codebase

---

## Phase Dependencies

```
Phase 1 (Foundation) → Phase 2 (Core DCA) → Phase 3 (Smart Multipliers) → Phase 4 (Observability)
```

Strictly sequential — each phase builds on the previous. No parallelization between phases.

## Coverage Matrix

| Requirement | Phase | Status |
|-------------|-------|--------|
| FR-1: Hyperliquid API | 1 | Pending |
| FR-2: Domain Models | 1 | Pending |
| FR-3: Fix Distributed Lock | 1 | Pending |
| FR-10: Configuration | 1 | Pending |
| FR-4: Daily Schedule | 2 | Pending |
| FR-5: Core DCA Execution | 2 | Pending |
| FR-6: Basic Notifications | 2 | Pending |
| FR-7: 30-Day High Tracking | 3 | Pending |
| FR-8: Dip Multiplier Tiers | 3 | Pending |
| FR-9: 200-Day MA Boost | 3 | Pending |
| FR-11: Rich Notifications | 4 | Pending |
| FR-12: Health Check | 4 | Pending |
| FR-13: Dry-Run Mode | 4 | Pending |

---
*Roadmap created: 2026-02-12*
