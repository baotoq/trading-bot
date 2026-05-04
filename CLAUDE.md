# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
cargo build                        # debug build
cargo build --release              # release build (for deployment)
cargo run                          # run the binary
cargo test                         # run all tests
cargo test <test_name>             # run a single test by name
cargo clippy                       # lint
cargo fmt                          # format
```

## Project

This is `trading-bot` — a one-shot Rust CLI that places a single spot market buy on Hyperliquid per invocation, designed to be scheduled via cron for dollar-cost averaging into UBTC.

The design spec lives at `docs/superpowers/specs/2026-05-04-hl-dca-bot-design.md` and is the authoritative reference for architecture decisions.

## Planned module layout

```
src/
├── main.rs       # orchestration (~30 lines): cli → config → risk → exchange → log
├── cli.rs        # clap arg definitions
├── config.rs     # Config struct, env var resolution (HL_PRIVATE_KEY), validation
├── risk.rs       # pre-flight checks (usd_amount ≤ max_spend_usd)
└── exchange.rs   # thin wrapper over hyperliquid_rust_sdk
```

Key planned dependencies: `clap`, `hyperliquid_rust_sdk`, `anyhow` (in main), `thiserror` (in exchange).

## Core design decisions

- **Default-deny network**: neither `--mainnet` nor `--testnet` → hard error. Both set → hard error. `--mainnet` is the only path to real money.
- **Stateless**: no local DB, no lockfile. Each invocation is fully independent.
- **At most one order per run**: `exchange` may call `place_order` exactly once, never retried inside the binary. Cron is the retry mechanism.
- **Fail loud**: all errors → single `error=<kind> detail="..."` line to stderr + non-zero exit. No stack traces.
- **`config` is the only module that reads env vars** — keeps other modules testable without env setup.
- **`--dry-run`**: logs intent line to stdout, exits 0, no API call.

## Error exit codes

| Situation | Exit |
|---|---|
| CLI parse error | 2 (clap default) |
| Config / env / network error | 1 |
| Risk cap exceeded | 1 (no API call made) |
| Exchange error | 1 |

## Success log format (stdout)

```
ts=2026-05-04T12:00:00Z network=testnet asset=UBTC usd=25 qty=0.000389 avg_px=64254.10 fee=0.0125 oid=12345
```

## Auth

Uses an **agent wallet** (API wallet) loaded from `HL_PRIVATE_KEY` env var. Agent wallets cannot withdraw — this is an intentional safety constraint. Never use the master wallet key.

## Testing

use TDD approach: write tests for each module before implementing it
