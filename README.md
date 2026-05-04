# trading-bot

One-shot Hyperliquid spot DCA bot. Run via cron to dollar-cost average into UBTC.

Each invocation places **one** spot market buy (IOC) and exits. No internal
scheduling, no state files, no retries — cron drives the cadence.

## Build

```
cargo build --release
# binary: target/release/trading-bot
```

## Setup

1. **Generate an agent wallet** on Hyperliquid (UI → API → "Generate API Wallet").
   Record its private key. Agent wallets cannot withdraw — that's the safety
   property the bot relies on.
2. **Approve the agent wallet** from your master wallet.
3. **Fund the master wallet** with USDC on Hyperliquid spot.
4. Set `HL_PRIVATE_KEY` to the **agent** key. Never the master key.

## Testnet smoke

```
HL_PRIVATE_KEY=<agent-testnet-key> target/release/trading-bot \
    --testnet --usd 10 --max-spend-usd 10 --asset UBTC --dry-run
```

Expect `dry_run=true network=testnet asset=UBTC usd=10`, exit 0.
Drop `--dry-run` to actually buy; verify on the Hyperliquid testnet UI.

## Mainnet first-run protocol

Before adding to cron, do **one** tiny mainnet buy by hand:

```
HL_PRIVATE_KEY=<agent-mainnet-key> target/release/trading-bot \
    --mainnet --usd 5 --max-spend-usd 5
```

Verify the fill on the Hyperliquid UI. **Only then** raise the USD amount.

## Cron

```
# $25 of UBTC every Monday at 09:00 (mainnet)
0 9 * * 1 HL_PRIVATE_KEY=<key> /usr/local/bin/trading-bot \
    --mainnet --usd 25 --asset UBTC --max-spend-usd 25 \
    >> /var/log/trading-bot.log 2>&1
```

## Output

Success (stdout, single line):

```
ts=2026-05-04T12:00:00Z network=mainnet asset=UBTC/USDC usd=25 qty=0.000389 avg_px=64254.1 fee=0 oid=12345
```

Failure (stderr, single line, non-zero exit):

```
error=risk_cap_exceeded detail="requested 100 exceeds cap 25"
```

## Design

`docs/superpowers/specs/2026-05-04-hl-dca-bot-design.md` is the authoritative
design doc. `.omc/plans/2026-05-04-hl-dca-implementation.md` is the build plan.
