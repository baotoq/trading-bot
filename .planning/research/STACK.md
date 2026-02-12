# Stack Research: Hyperliquid Integration for .NET

**Research Date:** 2026-02-12

## Hyperliquid API Overview

Hyperliquid is a decentralized perpetual exchange that also supports spot trading. It provides:
- **REST API** for info queries and order placement
- **WebSocket API** for real-time market data and order updates
- **EVM-compatible signing** for authentication (uses ETH private key)

### API Endpoints

**Mainnet:**
- Info API: `https://api.hyperliquid.xyz/info` (POST)
- Exchange API: `https://api.hyperliquid.xyz/exchange` (POST)
- WebSocket: `wss://api.hyperliquid.xyz/ws`

**Testnet:**
- Info API: `https://api.hyperliquid-testnet.xyz/info` (POST)
- Exchange API: `https://api.hyperliquid-testnet.xyz/exchange` (POST)
- WebSocket: `wss://api.hyperliquid-testnet.xyz/ws`

## .NET SDK Options

### Option 1: Direct HTTP Client (Recommended)

No official or well-maintained .NET SDK exists for Hyperliquid. The recommended approach is to build a thin API client using `HttpClient`.

**Pros:**
- Full control over request/response handling
- No dependency on third-party SDK maintenance
- Fits existing codebase pattern (BuildingBlocks infrastructure)
- Easy to test with `IHttpClientFactory`

**Cons:**
- Must implement EIP-712 signing manually
- More initial development work

**Required NuGet packages:**
- `Nethereum.Signer` — EIP-712 typed data signing (ETH key-based auth)
- `System.Text.Json` — Already included in .NET 10.0
- `Microsoft.Extensions.Http` — Already used via Aspire

### Option 2: Community .NET SDK

Some community SDKs exist (e.g., `Hyperliquid.Net`) but:
- Not widely adopted or battle-tested
- May not support spot trading endpoints
- Maintenance and update frequency uncertain
- Could introduce breaking changes

**Recommendation:** Avoid community SDKs for production trading. Build a thin client.

## Authentication: EIP-712 Signing

Hyperliquid uses Ethereum-style EIP-712 typed data signing for all exchange actions (placing orders, canceling orders, transfers).

### How It Works

1. Construct a typed data payload (EIP-712 format) for the action
2. Sign with ETH private key using `Nethereum.Signer`
3. Send signed payload to the exchange API

### Key Package: Nethereum

```
dotnet add package Nethereum.Signer
```

Nethereum is the most mature .NET Ethereum library. It provides:
- `EthECKey` — Private key management
- `Eip712TypedDataSigner` — EIP-712 signing
- Widely used, well-maintained, production-ready

## Hyperliquid Spot API

### Getting Spot Market Data

**POST** `https://api.hyperliquid.xyz/info`

```json
{"type": "spotMeta"}
```

Returns: list of spot tokens with metadata (name, decimals, token ID)

### Getting Spot Prices

**POST** `https://api.hyperliquid.xyz/info`

```json
{"type": "spotMetaAndAssetCtxs"}
```

Returns: spot metadata + current mark prices, 24h volume, etc.

### Getting Order Book

**POST** `https://api.hyperliquid.xyz/info`

```json
{"type": "l2Book", "coin": "BTC"}
```

### Placing a Spot Order

**POST** `https://api.hyperliquid.xyz/exchange`

Requires EIP-712 signed action:
```json
{
  "action": {
    "type": "order",
    "orders": [{
      "a": <token_index>,
      "b": true,
      "p": "price",
      "s": "size",
      "r": false,
      "t": {"limit": {"tif": "Ioc"}}
    }],
    "grouping": "na"
  },
  "nonce": <timestamp_ms>,
  "signature": "<eip712_signature>"
}
```

- `a`: Asset/token index (from spotMeta)
- `b`: true = buy, false = sell
- `p`: Price as string
- `s`: Size as string
- `r`: Reduce only (false for spot buys)
- `t.limit.tif`: Time in force — "Ioc" (immediate or cancel) or "Gtc" (good til cancel)

### Getting Balances

**POST** `https://api.hyperliquid.xyz/info`

```json
{"type": "spotClearinghouseState", "user": "0x..."}
```

Returns: spot token balances for the user address.

## Candle/OHLCV Data

**POST** `https://api.hyperliquid.xyz/info`

```json
{
  "type": "candleSnapshot",
  "coin": "BTC",
  "interval": "1d",
  "startTime": <unix_ms>,
  "endTime": <unix_ms>
}
```

Supported intervals: `1m`, `5m`, `15m`, `1h`, `4h`, `1d`

This is needed for:
- 200-day MA calculation (fetch 200+ daily candles)
- 30-day high tracking (fetch 30 daily candles)

## Rate Limits

Hyperliquid has relatively generous rate limits:
- Info endpoints: ~1200 requests/minute
- Exchange endpoints: ~100 orders/minute
- WebSocket: No specific limit on subscriptions

For a daily DCA bot, rate limits are not a concern — we make a handful of requests per day.

## Proposed Client Architecture

```
HyperliquidOptions (config)
├── BaseUrl
├── PrivateKey (ETH key, from user secrets)
└── IsTestnet

IHyperliquidClient
├── GetSpotMetaAsync()
├── GetSpotBalancesAsync(address)
├── GetCandlesAsync(coin, interval, start, end)
├── GetMidPriceAsync(coin)
├── PlaceSpotOrderAsync(order)
└── GetOrderStatusAsync(orderId)

HyperliquidClient : IHyperliquidClient
├── Uses HttpClient + IHttpClientFactory
├── Uses Nethereum for EIP-712 signing
├── Structured logging via ILogger
└── Retry/resilience via Polly (from Aspire defaults)
```

## Key NuGet Packages Needed

| Package | Version | Purpose |
|---------|---------|---------|
| `Nethereum.Signer` | latest | EIP-712 signing for Hyperliquid auth |
| `Nethereum.ABI` | latest | ABI encoding for typed data |

All other dependencies (HttpClient, JSON, logging, resilience) are already available in the existing stack.

## Existing Dependencies to Remove

- `Binance.Net` 11.11.0 — No longer needed (switching from Binance to Hyperliquid)
- `CryptoExchange.Net` 9.13.0 — Base SDK for Binance, no longer needed

These can be removed in a later cleanup phase. They won't interfere with the Hyperliquid integration.

## Risk Considerations

1. **Testnet first** — Always develop and test against Hyperliquid testnet before mainnet
2. **Private key security** — Store ETH private key in .NET User Secrets or env vars, never in config files
3. **Spot vs Perp token indices** — Spot tokens have different indices than perp markets; must use `spotMeta` to resolve
4. **Decimal precision** — Hyperliquid uses string-based prices/sizes with specific decimal precision per token
5. **USDC settlement** — Hyperliquid spot trades settle in USDC (need USDC balance to buy BTC)

---

*Stack research: 2026-02-12*
