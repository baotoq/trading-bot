# Hyperliquid Spot DCA Bot — Design (v1)

**Date:** 2026-05-04
**Author:** baotoq
**Status:** Draft for review

## Summary

A small, single-purpose Rust CLI (`hl-dca`) that places **one** spot market buy on Hyperliquid per invocation. Scheduled externally (cron / systemd timer / launchd) to implement dollar-cost averaging on UBTC.

Designed for **safety first**: default-deny network selection (no network → error; mainnet must be explicitly opted into via `--mainnet`), agent-wallet auth (no withdrawal capability), per-run USD cap, and a `--dry-run` mode for development.

## Goals

- Buy a configurable USD amount of UBTC on Hyperliquid spot, on demand.
- Be safe to schedule unattended via cron without a path to "drain my account."
- Be small enough to read end-to-end and audit by eye (~200 LOC core).
- Be a clean substrate for future wrappers (TUI monitor, Claude Code skill, MCP server) without baking any of them in now.

## Non-goals (explicitly out of scope for v1)

- Strategy logic beyond fixed-amount recurring buys.
- Internal scheduling — the OS handles that.
- Local persistence (DB, fill log, position tracking). On-chain state + cron's stdout capture is the record.
- Internal retry on API failure — the next cron tick is the retry.
- Mocking the Hyperliquid SDK in tests.
- Multiple assets per invocation, partial fills handling beyond logging, maker-rebate order modes.

## Decisions (locked in during brainstorming)

| Decision | Choice |
|---|---|
| Run model | One-shot CLI, externally scheduled |
| Asset | UBTC (Hyperliquid spot BTC); single asset in v1 |
| Order type | Market IOC |
| Auth | Agent wallet (API wallet) — cannot withdraw |
| Key supply | `HL_PRIVATE_KEY` env var |
| Risk controls | Per-run USD cap (`--max-spend-usd`) + `--dry-run` flag |
| Networks | Both testnet & mainnet supported; **mainnet requires `--mainnet`**, no default |
| Implementation | Use `hyperliquid_rust_sdk` directly (no abstraction layer) |
| Persistence | None — stateless |

## Architecture

```
┌─────────────┐
│  cron/timer │  ── invokes ──>  hl-dca --usd 25 --asset UBTC [--mainnet] [--dry-run]
└─────────────┘
                                       │
                                       ▼
                            ┌──────────────────────┐
                            │   hl-dca binary      │
                            │  ┌────────────────┐  │
                            │  │ cli (clap)     │  │
                            │  │ config         │  │
                            │  │ risk           │  │
                            │  │ exchange (SDK) │  │
                            │  │ logging        │  │
                            │  └────────────────┘  │
                            └──────────────────────┘
                                       │
                                       ▼
                          Hyperliquid API (testnet | mainnet)
```

Key properties:
- **Stateless.** No local DB, no lockfile. Each run is independent.
- **Default-deny network.** No network selected → error. `--mainnet` is the only way to touch real money.
- **One responsibility per invocation:** "buy $X of UBTC, or fail loudly."

## Components

| Module | Responsibility | Depends on |
|---|---|---|
| `cli` | Parse args with `clap`. Flags: `--usd <amount>` (required), `--max-spend-usd <amount>` (required), `--asset <symbol>` (default `UBTC`; v1 rejects anything else), `--mainnet`, `--testnet`, `--dry-run`. Reject if neither network flag is set, or if both are set. | clap |
| `config` | Resolve final config from CLI args + env (`HL_PRIVATE_KEY`). Validate (positive amounts, key format, USD ≤ cap). Returns a frozen `Config` struct or fails loudly. | cli output, std env |
| `risk` | Pre-flight checks. v1: assert `usd_amount <= max_spend_usd`. Surface: `check(&Config) -> Result<()>` so future checks drop in without touching call sites. | config |
| `exchange` | Thin wrapper over `hyperliquid_rust_sdk`. One function: `place_market_buy(asset, usd_amount, network, key) -> Fill`. Owns the network-URL switch and the SDK client. | hyperliquid_rust_sdk |
| `main` | Orchestrate: cli → config → risk → (exchange OR dry-run print) → log result. ~30 lines. | all of the above |

**Design rationale:**
- `risk` is its own module from day one even with one check, so future additions land without rearrangement.
- `exchange` hides the SDK behind one function so the v1 "use the SDK directly" choice doesn't leak into `main`/`risk`.
- `config` is the only place that reads env vars — keeps the rest of the code easy to test.

**File layout:**

```
hl-dca/
├── Cargo.toml
└── src/
    ├── main.rs       # orchestration
    ├── cli.rs        # clap definitions
    ├── config.rs     # Config struct + validation
    ├── risk.rs       # pre-flight checks
    └── exchange.rs   # SDK wrapper
```

## Data flow

A single invocation, top to bottom:

1. Parse CLI args (clap).
2. Load `HL_PRIVATE_KEY` from env.
3. Build & validate `Config`:
   - Network resolved (mainnet | testnet, error if neither).
   - USD amount parsed.
   - Private key parsed into wallet.
4. `risk::check(&config)`:
   - `usd_amount ≤ max_spend_usd`? Else exit non-zero with no API call.
5. If `--dry-run`: log "would buy $X UBTC on \<network\>", exit 0.
6. Else `exchange::place_market_buy(...)`:
   - SDK signs EIP-712 order with agent wallet key.
   - POST `https://api.hyperliquid[-testnet].xyz/exchange`.
   - Parse response: filled qty, avg price, fee, oid.
7. Log structured fill line to stdout (single line, key=value):
   ```
   ts=2026-05-04T12:00:00Z network=testnet asset=UBTC usd=25 qty=0.000389 avg_px=64254.10 fee=0.0125 oid=12345
   ```
8. Exit 0.

On failure at any step: log a single `error=<kind> detail="..."` line to stderr and exit non-zero. **No retries** — cron is the retry mechanism.

**Crucial property:** the binary never reads or writes local state. The on-chain fill + the stdout log line are the entire record.

## Error handling

**Principle: fail loud, fail fast, never retry.**

| Stage | Failure example | Behavior |
|---|---|---|
| CLI parse | unknown flag, missing `--usd` | clap prints usage, exits 2 |
| Network not chosen | neither `--mainnet` nor `--testnet` | error to stderr, exit 1 |
| Env / config | `HL_PRIVATE_KEY` missing or malformed | error to stderr, exit 1 |
| Risk check | `--usd` exceeds `--max-spend-usd` | error to stderr, exit 1, **no API call** |
| Exchange — pre-flight | can't reach API, auth rejected, asset not found | error to stderr, exit 1 |
| Exchange — order send | order rejected, insufficient balance, timeout | error to stderr, exit 1 |
| Exchange — order partial / unknown | request sent, response missing or ambiguous | error to stderr, exit 1, **flag prominently** |

**Ambiguous-send safety rules:**
1. Bounded HTTP timeout (10s default) so the binary never hangs.
2. If the SDK returns an error after sending: log `status=unknown order_send_state=ambiguous`. Do **not** retry inside the binary.
3. A single invocation makes **at most one** `place_order` call. Ever.

**Logging conventions:**
- Success: structured key=value line on stdout (per Data Flow §7).
- Error: single line on stderr, `error=<short_kind> detail="<message>"`.
- No multi-line stack traces. Cron + journalctl handle aggregation.

**Crate choices:** `anyhow` in `main` for propagation; `thiserror` in `exchange` for typed errors so `main` can map them to log kinds.

## Testing

Three layers, each justified.

**1. Unit tests (`cargo test`, no network)** — pure-function tests for non-I/O modules:
- `cli`: arg parsing — `--mainnet --testnet` rejected, `--usd 0` rejected, defaults applied correctly.
- `config`: env-var resolution — missing key fails clean, malformed key fails clean.
- `risk`: cap math — `usd > cap` rejects, `usd == cap` allows, `usd < cap` allows.

**2. Testnet smoke test (manual, documented in README)** — the test that proves the bot works:
1. Fund testnet wallet, generate an agent wallet, set `HL_PRIVATE_KEY` to the agent key.
2. `hl-dca --testnet --usd 10 --asset UBTC --dry-run` → assert log line shows intent.
3. Same without `--dry-run` → assert fill line, verify on Hyperliquid testnet UI.
4. `hl-dca --testnet --max-spend-usd 5 --usd 10` → assert exit non-zero, no order.

**3. Mainnet first-run protocol** — a checklist, not a test:
- First mainnet run: `--mainnet --usd 5 --max-spend-usd 5`, smallest possible buy.
- Verify fill on Hyperliquid UI. Only then increase USD amount and add to cron.

**Out of scope for v1:** SDK mocks (the SDK is the boundary), property tests, fuzzing.

## Operational notes

**Scheduling examples (user-provided, not part of the binary):**

```cron
# Buy $25 of BTC every Monday at 09:00 (mainnet)
0 9 * * 1 HL_PRIVATE_KEY=... /usr/local/bin/hl-dca --mainnet --usd 25 --asset UBTC --max-spend-usd 25 >> /var/log/hl-dca.log 2>&1
```

**Setup steps for the operator:**
1. Build: `cargo build --release`.
2. Generate agent wallet on Hyperliquid (UI → API → "Generate API Wallet"); record its private key.
3. Approve the agent wallet from your master wallet (Hyperliquid UI flow).
4. Fund the master wallet with USDC on Hyperliquid spot.
5. Set `HL_PRIVATE_KEY` to the **agent** key (never the master key).
6. Test on testnet first (see Testing §2).
7. Mainnet first-run protocol (see Testing §3).
8. Add to cron.

## Future work / out of scope

These are explicitly deferred. Each is small enough to add later without redesigning v1:

- **TUI monitor** — separate `hl-dca-monitor` crate (ratatui), read-only, tails fill log + queries Hyperliquid for current state. Workspace restructure when added.
- **Machine-friendly output** — `--json` flag for structured fill receipts; specific exit codes (`2` config, `3` risk-cap, `4` API). Required substrate for any Claude/MCP wrapper.
- **More risk checks** — slippage cap (compare IOC fill price to mid-price), 24h cumulative cap (needs local state), min-interval guard.
- **Maker-rebate order modes** — passive limit at best bid with cancel/retry loop.
- **Multi-asset / configurable spot symbols** — promote `--asset` from "validates UBTC only" to "validates against Hyperliquid spot asset list."
- **Natural-language frontend** — three viable paths (Claude Code Bash invocation, `/dca-buy` skill, MCP server). Pick one when needed; v1 imposes no constraint.

## Open questions

None at time of writing. (Add here if any arise during implementation planning.)
