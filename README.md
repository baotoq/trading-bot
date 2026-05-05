# dca — BTC DCA on Hyperliquid

Small Python CLI that dollar-cost-averages into Hyperliquid spot BTC. Two run
modes share the same core: a one-shot subcommand for cron / launchd /
systemd-timer, and a long-running daemon with an internal cron scheduler.

## Why this exists

Reliable unattended weekly/daily BTC accumulation, with a security posture
that does not put a withdrawal-capable key on the box running the bot.

## Setup

### 1. Create an API wallet on Hyperliquid

1. Open <https://app.hyperliquid.xyz/api> while signed in with the account
   that holds the funds.
2. Generate a new API wallet ("agent") and approve it for your account.
3. **Save its private key** — that's what the bot will sign with. The agent
   key cannot withdraw, so leaking it caps the blast radius at "places trades
   on the spot wallet you allocated for DCA."

### 2. Install

```bash
git clone <this repo> && cd trading-bot
make install
source .venv/bin/activate
```

### 3. Configure

Copy `.env.example` to `.env` and fill it in, or export the vars directly:

```bash
export HL_API_WALLET_KEY=0x...        # the API wallet key (NOT your main key)
export HL_ACCOUNT_ADDRESS=0x...       # the main account that holds funds
export HL_NETWORK=testnet              # start here; switch to mainnet when happy
```

### 4. Smoke-test

```bash
dca balance                            # prints USDC; confirms the wiring
dca buy --dry-run --amount 1           # full pricing path, no order sent
```

### 5. Real buy on testnet (optional but recommended)

```bash
dca buy --amount 1
```

## Usage

### One-shot (recommended)

Run from cron / launchd / a systemd timer:

```bash
# crontab: every Monday at 09:00 local time, $50 of BTC
0 9 * * 1   /path/to/.venv/bin/dca buy --amount 50 >> ~/.dca/dca.log 2>&1
```

Exit codes (sysexits.h):

| Code | Meaning |
|---|---|
| 0   | Success |
| 64  | Config error (missing/invalid env) — won't fix itself |
| 70  | Bug — investigate |
| 75  | Transient (insufficient balance, slippage, network) — try next cycle |
| 77  | Auth error (API wallet not approved) — won't fix itself |

Cron's MTA will mail you on non-zero. Distinct codes let you pipe through
something smarter.

### Daemon

```bash
dca run --schedule "0 9 * * 1" --amount 50
```

The daemon never exits on transient failure. It exits with code 77 only on
unrecoverable config/auth errors. SIGINT / SIGTERM finish the in-flight buy
and exit 0.

## Logging

Every event is one JSON line on stdout. Pipe to a file in cron, or `jq` it
during development:

```bash
dca buy --amount 1 | jq .
```

## Configuration reference

Env vars (prefix `HL_`):

| Var | Default | Description |
|---|---|---|
| `HL_API_WALLET_KEY` | required | Agent key, no withdraw scope. |
| `HL_ACCOUNT_ADDRESS` | required | Main account holding funds. |
| `HL_NETWORK` | `mainnet` | `mainnet` or `testnet`. |
| `HL_DEFAULT_AMOUNT_USDC` | (none) | Used if `--amount` omitted. |
| `HL_DEFAULT_SCHEDULE` | (none) | Cron expr, used by `run` if `--schedule` omitted. |
| `HL_SLIPPAGE_BPS` | `50` | Max slippage in basis points (50 = 0.5%). |

A TOML config at `--config <path>` provides defaults; env vars override.

## Tests

```bash
make test           # unit tests
make test-net       # integration test (needs HL_TESTNET_KEY / HL_TESTNET_ACCOUNT)
```

## Design

Full design lives at
`docs/superpowers/specs/2026-05-05-btc-dca-hyperliquid-cli-design.md`.
