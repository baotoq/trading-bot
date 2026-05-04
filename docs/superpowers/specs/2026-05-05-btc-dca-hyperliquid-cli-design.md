# BTC DCA on Hyperliquid — CLI Design

**Date:** 2026-05-05
**Status:** Approved (design); pending implementation plan
**Author:** Bao To (with Claude)

## Summary

A small Python CLI tool that performs Dollar Cost Averaging into spot BTC
(`UBTC/USDC`) on the Hyperliquid exchange. Two run modes share the same core
buy logic: a one-shot subcommand suitable for cron/launchd, and a long-running
daemon with an internal cron-driven scheduler. Each cycle places one fixed-USDC
market buy of BTC, capped by a slippage tolerance, and logs a structured
result. No persistent state.

## Goals

- Reliable, unattended weekly/daily BTC accumulation on Hyperliquid spot.
- Two ergonomic entry points: external scheduler (cron) and internal daemon.
- Strong key-handling posture: no withdrawal-capable key on the host.
- Small enough to read end-to-end in one sitting.

## Non-goals

- Perpetual futures, leverage, funding-rate strategies.
- "Smart" DCA variants (value-averaging, technical-indicator gating).
- Multi-asset support (BTC only in v1).
- Persistent trade history, double-buy idempotency, recovery after crash mid-buy.
  *Trade-off accepted:* a daemon crash between order submit and confirmation
  can produce a duplicate buy on the next cycle. With small fixed amounts this
  is bounded and acceptable.
- Withdrawals, transfers, position management.

## Decisions

| Decision | Choice | Rationale |
|---|---|---|
| Venue | Hyperliquid **spot** (`UBTC/USDC`) | Real BTC accumulation; no funding/liquidation. |
| Run modes | Both **one-shot CLI** and **daemon** | One-shot fits cron; daemon for OSes/users without one. Same core call. |
| Sizing | **Fixed USDC per cycle** | Canonical DCA. `--mode percent` etc. can be added later without redesign. |
| Order type | **IOC limit at `mid + slippage_bps`** | Hard cap on what we'll pay; immediate fill expected; no retries. |
| Auth | **API wallet** (agent key, no withdraw) | Compromised key cannot drain funds. Revocable from Hyperliquid UI. |
| Persistence | **Stateless** (stdout JSON logs) | Simplicity over crash-safety. Trade-off accepted. |
| SDK | `hyperliquid-python-sdk` (official) | Maintained, handles EIP-712 signing, network selection. |
| CLI framework | `typer` | Type-hint-driven, modern, low boilerplate. |
| Config | `pydantic-settings` | Env vars + optional TOML, clean validation, `Decimal` parsing. |
| Schedule format | Cron expression (`croniter`) | Standard, expressive, daemon-friendly. |
| Money type | `Decimal` end-to-end | No float drift on currency math. |
| Python version | 3.12+ | Modern typing, performance, datetime UTC helpers. |

## Architecture

```
trading-bot/
├── pyproject.toml          # uv- or hatch-managed, Python 3.12+
├── README.md
├── .env.example            # HL_API_WALLET_KEY, HL_ACCOUNT_ADDRESS, HL_NETWORK
└── dca/
    ├── __init__.py
    ├── cli.py              # typer commands: buy, run, balance
    ├── config.py           # pydantic-settings: env + ~/.dca/config.toml
    ├── exchange.py         # HyperliquidClient (wraps hyperliquid-python-sdk)
    ├── scheduler.py        # croniter-driven loop for `dca run`
    └── dca.py              # one_buy() — the core use case
```

Each module targets ~80 lines or fewer.

### Components

**`config.py` — `Settings`** (pydantic-settings model). Loads from env vars and an optional `~/.dca/config.toml`. Env wins on conflict.

| Field | Type | Default | Notes |
|---|---|---|---|
| `api_wallet_key` | `SecretStr` | required | Agent key (no withdraw). |
| `account_address` | `str` | required | Main wallet that holds funds. |
| `network` | `Literal["mainnet","testnet"]` | `"mainnet"` | Selects SDK endpoint. |
| `default_amount_usdc` | `Decimal \| None` | `None` | Used if `--amount` omitted. |
| `default_schedule` | `str \| None` | `None` | Cron expr; used by `dca run` if `--schedule` omitted. |
| `slippage_bps` | `int` | `50` | Cap on tolerable slippage (0.5%). Aborts cycle if exceeded. |

**`exchange.py` — `HyperliquidClient`**. Thin wrapper. Four methods:
- `usdc_balance() -> Decimal`
- `btc_mid_price() -> Decimal`
- `btc_lot_size() -> Decimal` — from cached `meta`.
- `place_btc_buy(size: Decimal, limit_price: Decimal) -> FillResult` — sends IOC limit buy. No conversion logic here.

USD-to-size conversion, slippage cap, and lot rounding live in `dca.one_buy`,
where they are unit-testable against `FakeHyperliquidClient`.

`FillResult` is a small dataclass: `submitted_size`, `filled_size`, `avg_price`, `status`.

Lot/precision is queried once per process from Hyperliquid's `meta` endpoint and cached. `dca.one_buy` rounds size *down* so we never overshoot the budget.

**`dca.py` — `one_buy(client, settings, amount_usdc, dry_run) -> FillResult`**. Pure use case: validates balance, calls the client, emits structured logs. No I/O beyond the injected client and stdlib `logging`.

**`scheduler.py`**. `run(settings, schedule, amount, stop_event)`:
```python
while not stop_event.is_set():
    next_t = croniter(schedule, now_local()).get_next(datetime)
    sleep_until(next_t, stop_event)   # interruptible
    try:
        one_buy(client, settings, amount, dry_run=False)
    except Exception:
        log.exception("cycle_failed")
        continue
```

Cron expressions are evaluated in the **host's local timezone** (matches the
behavior of system cron and typical user expectation of "9am" meaning local
9am). Log timestamps are emitted as UTC ISO-8601. SIGINT/SIGTERM set
`stop_event` and let any in-flight buy finish.

**`cli.py` — `typer` app**. Three commands:
- `dca buy --amount <USDC> [--dry-run]` — one-shot, exits with a meaningful code.
- `dca run --schedule "<cron>" --amount <USDC>` — daemon.
- `dca balance` — prints spot USDC + UBTC. Smoke-test command.

## Data flow (one cycle)

```
cli.buy(amount=50)
  └─ Settings.load()                            # env + config.toml
  └─ HyperliquidClient(settings)                # SDK Info + Exchange for chosen network
  └─ dca.one_buy(client, settings, 50, dry_run=False)
       ├─ usdc = client.usdc_balance()
       │    └─ if usdc < 50: raise InsufficientFunds → exit 75
       ├─ mid  = client.btc_mid_price()
       ├─ lot  = client.btc_lot_size()
       ├─ size = round_down_to_lot(50 / mid, lot)
       ├─ limit = mid * (1 + slippage_bps / 10_000)
       ├─ if dry_run: log "would buy {size} BTC @ ≤{limit}"; return
       ├─ fill = client.place_btc_buy(size, limit)    # IOC limit at the cap
       └─ log JSON line {ts, mid, size, filled_size, avg_price, status}
```

Daemon path is the same `one_buy` call wrapped in the cron loop.

### Notes

- **Decimal end-to-end.** Settings parse strings into `Decimal`. Format with
  `f"{x:.6f}"` only at the SDK boundary.
- **IOC-with-cap, not pure market.** A limit at `mid + slippage_bps` with `IOC`
  gives a hard guardrail on price while still typically taking immediately.
  Partial fills are logged and the cycle exits non-zero — operator
  investigates. We do not retry within a cycle.
- **Logging is structured JSON to stdout** (one line per event). Easy to grep,
  pipe into `jq`, or append to a file with `>>` from cron.

## Error handling

### One-shot (`dca buy`) — exit codes

| Condition | Exit | sysexits.h | Cron behavior |
|---|---|---|---|
| Missing/invalid env vars | 64 | `EX_USAGE` | Notify; permanent. |
| API wallet not authorized | 77 | `EX_NOPERM` | Notify; permanent. |
| Insufficient USDC balance | 75 | `EX_TEMPFAIL` | Transient; user may top up. |
| Slippage cap exceeded (no/partial fill) | 75 | `EX_TEMPFAIL` | Try again next cycle. |
| Network/HTTP timeout or 5xx (1 retry, 2s backoff) | 75 | `EX_TEMPFAIL` | Transient. |
| Signature/auth 4xx | 77 | `EX_NOPERM` | Permanent until reconfigured. |
| Unknown exception | 70 | `EX_SOFTWARE` | Bug; print traceback. |
| Success | 0 | — | — |

Distinct codes let an outer wrapper or cron MTA differentiate transient vs.
permanent failure.

### Daemon (`dca run`)

- Transient failures (anything that would be 75): WARN, continue.
- Config/auth failures (64/77): ERROR, exit 77 — won't fix itself.
- Unknown exception: ERROR + traceback, **continue**. Daemon is more useful alive than dead.
- SIGINT/SIGTERM: finish in-flight buy, exit 0.

### Defensive, not paranoid

- One order attempt per cycle. No in-cycle retry — retries are how you double-buy.
- HTTP timeout is a hard 10s. No 30-minute hangs.
- The bot only buys. It does not transfer, withdraw, or close positions, ever.

### Out of scope

- Hyperliquid downtime for hours. Cycle gets skipped; user catches up manually. A queue would be more dangerous than helpful.
- Clock drift on the host. NTP is the user's job.

## Testing

### Unit tests (`pytest`) — the bulk

- `dca.one_buy` against a `FakeHyperliquidClient` (small protocol):
  happy path, insufficient balance, dry-run, slippage cap exceeded, partial
  fill, lot-rounding edges (e.g., `$50 / $63,123.45` quantized to BTC `szDecimals`).
- `config.Settings`: env-only, TOML-only, env-overrides-TOML, missing required
  field raises with a useful message.
- `scheduler.run`: next-fire computation with a fake clock; SIGTERM stops the
  loop; an exception in `one_buy` is logged and the loop continues. No real
  `sleep`.

### Integration test — testnet only, opt-in

- One marked test (`@pytest.mark.testnet`) that hits Hyperliquid testnet with
  $1. Skipped unless `HL_TESTNET_KEY` is set. Asserts: order submitted, fill
  returned, balances moved.
- The only test that touches the network. CI runs unit tests; integration is
  run manually before each release.

### Smoke checks via the CLI itself

- `dca balance` — first thing the README tells the user to run.
- `dca buy --dry-run --amount 1` — full pricing/rounding path without sending.

### Out of test scope (deliberate)

- Hyperliquid SDK internals.
- Recorded-HTTP fixtures. Brittle, slow to maintain, and they don't catch the bugs that actually bite (signing, schema drift).
- Daemon soak tests. Loop is small and unit-tested. Add later only if real-world drift shows up.

### Layout

```
tests/
├── test_config.py
├── test_dca.py
├── test_scheduler.py
└── test_integration.py     # @pytest.mark.testnet, opt-in
```

## Dependencies

| Package | Purpose |
|---|---|
| `hyperliquid-python-sdk` | Official Hyperliquid client. |
| `typer` | CLI. |
| `pydantic`, `pydantic-settings` | Config + validation. |
| `croniter` | Cron-expression scheduling for the daemon. |
| `pytest` | Tests. |

Standard library: `decimal`, `logging`, `signal`, `tomllib`, `dataclasses`, `datetime`, `threading.Event` (stop signal).

## Setup outline (informs the implementation plan)

Before any v1 functionality is reachable, the user must:

1. Have a funded Hyperliquid account holding USDC in the spot wallet.
2. Create an API wallet (agent) at `app.hyperliquid.xyz/api`, approve it for
   their account, save its private key.
3. Set env vars: `HL_API_WALLET_KEY`, `HL_ACCOUNT_ADDRESS`, optionally
   `HL_NETWORK=testnet` for first run.
4. `dca balance` to confirm wiring.
5. `dca buy --dry-run --amount 1` to confirm pricing.
6. `dca buy --amount 1` on testnet to confirm full path.

## Open items deferred to future iterations

- `--mode percent` (sizing as % of remaining USDC).
- Notifications (Telegram/Discord/email) on cycle outcomes.
- SQLite history + idempotency keys (revisit if double-buys ever bite).
- Multi-asset support behind a `--asset` flag (`UBTC`, `UETH`, …).
- "Patient" maker-only order type for fee savings.
