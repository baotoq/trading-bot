# Phase 1: Foundation & Hyperliquid Client - Research

**Researched:** 2026-02-12
**Domain:** Hyperliquid API integration, EIP-712 signing, PostgreSQL distributed locking, HTTP resilience, Entity Framework Core
**Confidence:** MEDIUM

## Summary

Phase 1 focuses on establishing the critical infrastructure: Hyperliquid API client with EIP-712 authentication, domain models with EF Core persistence, PostgreSQL advisory locks for distributed coordination, and resilience patterns for reliable HTTP communication.

The highest risk area is EIP-712 signing for Hyperliquid authentication. While Nethereum provides the signing primitives, there's no .NET SDK for Hyperliquid, requiring direct HTTP client implementation with manual EIP-712 message construction. Hyperliquid's documentation emphasizes that signature generation is error-prone (field ordering, numeric precision, address formatting all matter) and produces unhelpful error messages on failure.

PostgreSQL advisory locks provide a simpler alternative to Redis-backed distributed locking, especially within an Aspire-orchestrated environment. The DistributedLock.Postgres library handles transaction-scoped locking that works with connection poolers like PgBouncer. Microsoft.Extensions.Http.Resilience (built on Polly v8) provides production-ready retry and circuit breaker patterns with minimal configuration.

**Primary recommendation:** Start with Python SDK analysis to understand EIP-712 message structure, implement signing using Nethereum.Signer.EIP712, validate against testnet early, and use PostgreSQL advisory locks instead of fixing the existing Redis stub.

## Standard Stack

The established libraries/tools for this domain:

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| Nethereum.Signer.EIP712 | 5.8.0+ | EIP-712 typed data signing | Most mature .NET Ethereum library, official EIP-712 support |
| Microsoft.Extensions.Http.Resilience | Latest | HTTP retry/circuit breaker | Official Microsoft package built on Polly v8, zero-allocation design |
| Aspire.Npgsql.EntityFrameworkCore.PostgreSQL | 13.0.1+ | EF Core + PostgreSQL + Aspire | Official Aspire component with health checks, telemetry |
| DistributedLock.Postgres | 2.5.0+ | PostgreSQL advisory locks | Mature library, handles connection pooling scenarios |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| Cronos | 0.8.4+ | Cron expression parsing | When daily schedule timing needs flexible configuration |
| PeriodicTimer | Built-in (.NET 6+) | Periodic task scheduling | For simple fixed-interval schedules (preferred over Thread.Sleep) |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| DistributedLock.Postgres | Dapr DistributedLock with Redis | Redis adds infrastructure complexity, Dapr already stubbed in codebase but PostgreSQL advisory locks are simpler |
| Custom HTTP client | Hyperliquid SDK | No official .NET SDK exists; Python SDK only for reference |
| Cronos | NCrontab | Cronos has better async support and timezone handling |

**Installation:**
```bash
dotnet add package Nethereum.Signer.EIP712
dotnet add package Microsoft.Extensions.Http.Resilience
dotnet add package DistributedLock.Postgres
dotnet add package Cronos  # Optional for flexible scheduling
```

## Architecture Patterns

### Recommended Project Structure
```
TradingBot.ApiService/
├── Models/              # Domain entities
│   ├── Purchase.cs
│   ├── DailyPrice.cs    # May not persist - fetch fresh each time
│   └── DcaConfiguration.cs  # Or use IOptions pattern
├── Services/            # Business logic
│   ├── HyperliquidClient.cs
│   ├── DcaSchedulerService.cs  # BackgroundService
│   └── PriceService.cs
├── Infrastructure/      # Technical concerns
│   ├── Data/
│   │   ├── TradingBotDbContext.cs
│   │   └── Migrations/
│   ├── Hyperliquid/
│   │   ├── HyperliquidSigner.cs  # EIP-712 signing
│   │   └── Models/     # API DTOs
│   └── Locking/
│       └── PostgresLockProvider.cs
└── Configuration/       # Options classes
    └── DcaOptions.cs
```

### Pattern 1: EIP-712 Signing with Nethereum

**What:** Use Nethereum's Eip712TypedDataSigner to sign Hyperliquid orders

**When to use:** Every order submission to Hyperliquid requires EIP-712 signature

**Key insights:**
- Hyperliquid uses custom EIP-712 message structure (no official .NET documentation)
- Must construct TypedData with exact domain separator and message types
- Field ordering in type definitions is critical (different order = different hash)
- Always use SignTypedDataV4 for MetaMask compatibility
- Lowercase all addresses before signing (uppercase causes signature mismatch)

**Example:**
```csharp
// Source: https://github.com/Nethereum/Nethereum/blob/master/src/Nethereum.Signer.EIP712/Eip712TypedDataSigner.cs
using Nethereum.Signer.EIP712;

public class HyperliquidSigner
{
    private readonly Eip712TypedDataSigner _signer = new();
    private readonly EthECKey _key;

    public HyperliquidSigner(string privateKey)
    {
        _key = new EthECKey(privateKey);
    }

    public string SignOrder(OrderAction action, long nonce, bool isTestnet)
    {
        // Must analyze Python SDK to determine exact TypedData structure
        var typedData = new TypedData
        {
            Domain = new Domain
            {
                Name = "Exchange", // Example - verify with Python SDK
                Version = "1",
                ChainId = isTestnet ? 421614 : 42161, // Arbitrum testnet/mainnet
                VerifyingContract = "..." // Must get from Hyperliquid docs
            },
            Types = new Dictionary<string, MemberDescription[]>
            {
                ["EIP712Domain"] = new[]
                {
                    new MemberDescription { Name = "name", Type = "string" },
                    new MemberDescription { Name = "version", Type = "string" },
                    new MemberDescription { Name = "chainId", Type = "uint256" },
                    new MemberDescription { Name = "verifyingContract", Type = "address" }
                },
                ["Order"] = new[]
                {
                    // Field order MUST match Python SDK exactly
                    new MemberDescription { Name = "asset", Type = "uint32" },
                    new MemberDescription { Name = "isBuy", Type = "bool" },
                    new MemberDescription { Name = "limitPx", Type = "string" },
                    new MemberDescription { Name = "sz", Type = "string" },
                    // ... other fields
                }
            },
            PrimaryType = "Order",
            Message = new[]
            {
                new MemberValue { TypeName = "uint32", Value = action.Asset },
                new MemberValue { TypeName = "bool", Value = action.IsBuy },
                // ... other values
            }
        };

        // SignTypedDataV4 is the correct method (not SignTypedData)
        var signature = _signer.SignTypedDataV4(typedData, _key);
        return signature;
    }
}
```

**Critical unknowns requiring Python SDK analysis:**
1. Exact TypedData structure for Hyperliquid orders
2. Domain separator values (name, version, verifyingContract address)
3. Complete field list and ordering for Order message type
4. How to handle different order types (market vs limit)
5. How to encode spot asset indices (10000 + index)

### Pattern 2: PostgreSQL Advisory Locks for DCA Coordination

**What:** Use PostgreSQL advisory locks instead of Redis to prevent concurrent DCA executions

**When to use:** Before any DCA order execution to ensure only one instance runs

**Why PostgreSQL advisory locks:**
- Already using PostgreSQL for persistence
- No additional infrastructure (no Redis/Dapr lock store needed)
- Transaction-scoped locks work with connection pooling (PgBouncer)
- Simpler than fixing existing Dapr stub

**Example:**
```csharp
// Source: https://github.com/madelson/DistributedLock (PostgreSQL provider)
using Medallion.Threading.Postgres;

public class DcaSchedulerService : BackgroundService
{
    private readonly IDistributedLockProvider _lockProvider;
    private const string DcaLockKey = "dca-execution";

    public DcaSchedulerService(TradingBotDbContext dbContext)
    {
        // Create lock provider from existing connection string
        _lockProvider = new PostgresDistributedSynchronizationProvider(
            dbContext.Database.GetConnectionString());
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await WaitForScheduledTime(stoppingToken);

            // Acquire distributed lock with 30s timeout
            await using var lockHandle = await _lockProvider
                .AcquireLockAsync(DcaLockKey, timeout: TimeSpan.FromSeconds(30), stoppingToken);

            if (lockHandle != null)
            {
                await ExecuteDcaPurchase(stoppingToken);
            }
            else
            {
                _logger.LogWarning("Failed to acquire DCA lock - another instance is running");
            }
        }
    }
}
```

**Notes:**
- Lock is automatically released when `lockHandle` is disposed
- If process crashes, PostgreSQL cleans up locks automatically
- Can use `TryAcquireLockAsync` for immediate return instead of waiting

### Pattern 3: HTTP Resilience with Standard Handler

**What:** Use AddStandardResilienceHandler for Hyperliquid HTTP client

**When to use:** All HTTP communication with external APIs (Hyperliquid)

**Example:**
```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience
services.AddHttpClient<HyperliquidClient>(client =>
{
    client.BaseAddress = new Uri(isTestnet
        ? "https://api.hyperliquid-testnet.xyz"
        : "https://api.hyperliquid.xyz");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddStandardResilienceHandler(options =>
{
    // Customize retry for DCA: don't retry POSTs (order placement)
    options.Retry.DisableForUnsafeHttpMethods();

    // Circuit breaker for detecting sustained API issues
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
    options.CircuitBreaker.FailureRatio = 0.1; // 10% failure rate
    options.CircuitBreaker.MinimumThroughput = 3;
});
```

**Standard resilience handler provides:**
1. Rate limiter (1000 concurrent requests)
2. Total request timeout (30s)
3. Retry with exponential backoff (3 retries, 2s initial delay)
4. Circuit breaker (10% failure ratio, 5s break duration)
5. Attempt timeout (10s per attempt)

**Critical for Phase 1:**
- Disable retries for POST requests (order placement) to avoid duplicate orders
- Keep circuit breaker enabled to detect API outages
- Log all retry attempts for debugging

### Pattern 4: EF Core Auto-Migration on Startup

**What:** Apply pending migrations when application starts

**When to use:** Development and single-instance deployments

**Example:**
```csharp
// Source: https://www.thereformedprogrammer.net/how-to-safely-apply-an-ef-core-migrate-on-asp-net-core-startup/
var app = builder.Build();

// Apply migrations before running app
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<TradingBotDbContext>();
    await dbContext.Database.MigrateAsync();
}

await app.RunAsync();
```

**Safety considerations:**
- Safe for single instance (Aspire local dev)
- For multi-instance: use distributed lock or separate migration job
- Test migrations on separate testnet database first
- Separate databases for testnet vs mainnet (different connection strings)

### Pattern 5: BackgroundService for Daily DCA Scheduling

**What:** Use BackgroundService to execute DCA at specific time each day

**When to use:** Daily DCA purchases at configured UTC time

**Example:**
```csharp
// Sources:
// - https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services
// - https://www.w3tutorials.net/blog/how-to-run-cron-job-every-day-in-asp-net-core-application/
public class DcaSchedulerService : BackgroundService
{
    private readonly ILogger<DcaSchedulerService> _logger;
    private readonly IServiceProvider _services;
    private readonly IOptionsMonitor<DcaOptions> _options;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var nextRun = CalculateNextRun();
            var delay = nextRun - DateTime.UtcNow;

            if (delay > TimeSpan.Zero)
            {
                _logger.LogInformation("Next DCA run scheduled for {Time} UTC", nextRun);
                await Task.Delay(delay, stoppingToken);
            }

            // Use scope for DI lifetime management
            using var scope = _services.CreateScope();
            var hyperliquid = scope.ServiceProvider.GetRequiredService<HyperliquidClient>();
            var lockProvider = scope.ServiceProvider.GetRequiredService<IDistributedLockProvider>();

            await using var lockHandle = await lockProvider.AcquireLockAsync("dca-execution");
            if (lockHandle != null)
            {
                await ExecuteDcaPurchase(hyperliquid, stoppingToken);
            }
        }
    }

    private DateTime CalculateNextRun()
    {
        var config = _options.CurrentValue;
        var now = DateTime.UtcNow;
        var scheduled = new DateTime(now.Year, now.Month, now.Day,
            config.DailyBuyHour, config.DailyBuyMinute, 0, DateTimeKind.Utc);

        // If already passed today, schedule for tomorrow
        return scheduled <= now ? scheduled.AddDays(1) : scheduled;
    }
}
```

**Notes:**
- Use IServiceProvider to create scopes (DbContext is scoped, BackgroundService is singleton)
- IOptionsMonitor enables hot reload of configuration
- Calculate next run time instead of fixed 24-hour intervals (handles clock drift)
- Distributed lock prevents duplicate execution if multiple instances

### Anti-Patterns to Avoid

- **Don't use Thread.Sleep for scheduling** - Use PeriodicTimer or calculated Task.Delay instead
- **Don't retry order placement requests** - Can cause duplicate orders, disable retries for POST
- **Don't log private keys** - Even in debug/development environments
- **Don't use session-level advisory locks with PgBouncer** - Use transaction-scoped locks instead
- **Don't ignore EIP-712 field ordering** - Different order produces invalid signature

## Don't Hand-Roll

Problems that look simple but have existing solutions:

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Distributed locking | Custom Redis lock with TTL | DistributedLock.Postgres | Handles edge cases: deadlocks, connection pooling, automatic cleanup, reentrant locks |
| HTTP retry logic | Custom retry with delays | Microsoft.Extensions.Http.Resilience | Zero-allocation, circuit breaker, telemetry, configurable strategies |
| EIP-712 signing | Manual hash calculation | Nethereum.Signer.EIP712 | Handles encoding rules, domain separator, keccak256, ECDSA signing correctly |
| Cron scheduling | Parsing cron expressions | Cronos library | Handles timezones, DST, leap years, edge cases |
| EF Core migrations | Manual SQL scripts | EF Core migrations | Type-safe, version control, up/down support, database-agnostic |

**Key insight:** Authentication and cryptographic signing are error-prone domains. Even small mistakes (address case, field order, numeric encoding) produce hard-to-debug failures. Hyperliquid's own docs warn that signatures fail with unhelpful error messages.

## Common Pitfalls

### Pitfall 1: EIP-712 Signature Generation Errors

**What goes wrong:** Invalid signatures that produce cryptic error messages like "User or API Wallet 0x0123... does not exist"

**Why it happens:**
- Field ordering in TypedData doesn't match expected structure
- Addresses contain uppercase characters (must lowercase)
- Trailing zeros in numeric values (precision issues)
- Wrong domain separator values

**How to avoid:**
1. Analyze Python SDK signing implementation line-by-line
2. Add extensive logging at each signature generation step
3. Test against testnet with small amounts first
4. Verify signature recovery locally before sending
5. Compare generated message hashes with Python SDK output

**Warning signs:**
- Generic authentication errors from API
- Signatures that work locally but fail in API
- Inconsistent success/failure for same order parameters

### Pitfall 2: Concurrent DCA Execution in Multi-Instance Deployment

**What goes wrong:** Multiple instances execute DCA simultaneously, placing duplicate orders

**Why it happens:**
- BackgroundService runs in every app instance
- No coordination between instances
- Clock drift causes slight timing differences

**How to avoid:**
- Use distributed lock (PostgreSQL advisory lock) before order execution
- Lock timeout should exceed max order execution time
- Log which instance acquires lock
- Monitor for lock acquisition failures

**Warning signs:**
- Duplicate purchase records in database
- Multiple orders placed within seconds
- Balance insufficient errors (second instance tries to buy)

### Pitfall 3: Retry Logic Causing Duplicate Orders

**What goes wrong:** HTTP client retries POST request after timeout, placing duplicate orders

**Why it happens:**
- Standard resilience handler retries all HTTP methods by default
- Order might succeed on Hyperliquid but response times out
- Retry logic re-submits same order

**How to avoid:**
```csharp
options.Retry.DisableForUnsafeHttpMethods(); // Disables POST, PUT, DELETE retries
```
- Never retry POST requests for order placement
- Implement idempotency with client order ID (order `c` field)
- Query order status instead of retrying

**Warning signs:**
- Duplicate orders with same parameters
- Balance drops more than expected
- Timeout errors followed by successful orders

### Pitfall 4: PostgreSQL Advisory Lock Session Leakage with PgBouncer

**What goes wrong:** Session-level advisory locks don't release when connection returns to pool

**Why it happens:**
- `pg_advisory_lock` is session-scoped (persists across transactions)
- PgBouncer reuses connections across different clients
- Lock held by previous session blocks new requests

**How to avoid:**
- Use transaction-scoped locks: `pg_advisory_xact_lock`
- DistributedLock.Postgres handles this automatically
- Don't use raw SQL for advisory locks

**Warning signs:**
- Lock acquisition times out after initial success
- Other instances can't acquire lock even after first completes
- Locks visible in `pg_locks` view after transaction ends

### Pitfall 5: Private Key Exposure in Logs

**What goes wrong:** Private key appears in logs through exception messages, request dumps, or debug output

**Why it happens:**
- Exception serialization includes all object properties
- HTTP client logging dumps request bodies
- Debug logging includes sensitive configuration

**How to avoid:**
```csharp
// Configure logging to exclude sensitive data
services.AddHttpClient<HyperliquidClient>()
    .ConfigureHttpMessageHandlerBuilder(builder =>
    {
        // Don't log request/response bodies in production
        builder.AdditionalHandlers.Add(new LoggingHandler(redactBody: true));
    });

// Mark sensitive config properties
public class HyperliquidOptions
{
    public string PrivateKey { get; set; } = ""; // Never log this

    public override string ToString() => $"API: {ApiUrl}, IsTestnet: {IsTestnet}";
}
```
- Store private key in user secrets (local) / environment variables (production)
- Never log configuration objects directly
- Redact sensitive fields in structured logging
- Review all log output before deploying

**Warning signs:**
- "0x..." patterns in log files
- Secrets in exception stack traces
- Configuration dumps in startup logs

## Code Examples

Verified patterns from official sources:

### Hyperliquid HTTP Client Configuration

```csharp
// Source: https://hyperliquid.gitbook.io/hyperliquid-docs/for-developers/api
public class HyperliquidClient
{
    private readonly HttpClient _http;
    private readonly HyperliquidSigner _signer;
    private readonly ILogger<HyperliquidClient> _logger;

    public HyperliquidClient(HttpClient http, HyperliquidSigner signer, ILogger<HyperliquidClient> logger)
    {
        _http = http;
        _signer = signer;
        _logger = logger;
    }

    public async Task<SpotMetadata> GetSpotMetadataAsync(CancellationToken ct = default)
    {
        var request = new { type = "spotMeta" };
        var response = await _http.PostAsJsonAsync("/info", request, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<SpotMetadata>(ct);
    }

    public async Task<decimal> GetSpotPriceAsync(string symbol, CancellationToken ct = default)
    {
        var meta = await GetSpotMetadataAsync(ct);
        var request = new
        {
            type = "spotMetaAndAssetCtxs"
        };
        var response = await _http.PostAsJsonAsync("/info", request, ct);
        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<SpotMetaAndAssetCtxs>(ct);

        // Find BTC/USDC price from response
        var btcPair = data.Ctxs.First(c => c.Name == symbol);
        return decimal.Parse(btcPair.MarkPx);
    }

    public async Task<OrderResponse> PlaceSpotOrderAsync(
        int assetIndex,
        bool isBuy,
        decimal size,
        decimal price,
        CancellationToken ct = default)
    {
        // Asset for spot: 10000 + index (from spotMeta.universe)
        var asset = 10000 + assetIndex;

        var order = new
        {
            a = asset,
            b = isBuy,
            p = price.ToString("F8"), // 8 decimal places
            s = size.ToString("F8"),
            r = false, // reduce-only
            t = new { limit = new { tif = "Gtc" } } // Good til canceled
        };

        var nonce = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var signature = _signer.SignOrder(order, nonce);

        var request = new
        {
            action = new
            {
                type = "order",
                orders = new[] { order },
                grouping = "na"
            },
            nonce,
            signature = new
            {
                r = signature.R,
                s = signature.S,
                v = signature.V
            },
            vaultAddress = (string?)null
        };

        _logger.LogInformation("Placing spot order: {Asset} {Side} {Size} @ {Price}",
            asset, isBuy ? "BUY" : "SELL", size, price);

        var response = await _http.PostAsJsonAsync("/exchange", request, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Order failed: {Status} {Content}", response.StatusCode, content);
            throw new HyperliquidException($"Order failed: {content}");
        }

        return await response.Content.ReadFromJsonAsync<OrderResponse>(ct);
    }
}
```

### Entity Framework Core DbContext with Aspire

```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/aspire/database/postgresql-entity-framework-component
public class TradingBotDbContext : DbContext
{
    public TradingBotDbContext(DbContextOptions<TradingBotDbContext> options)
        : base(options)
    {
    }

    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<DailyPrice> DailyPrices => Set<DailyPrice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Purchase>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Price).HasPrecision(18, 8);
            entity.Property(e => e.Quantity).HasPrecision(18, 8);
            entity.Property(e => e.Cost).HasPrecision(18, 2);
            entity.Property(e => e.Multiplier).HasPrecision(4, 2);
            entity.HasIndex(e => e.Timestamp);
        });

        modelBuilder.Entity<DailyPrice>(entity =>
        {
            entity.HasKey(e => e.Date);
            entity.Property(e => e.Open).HasPrecision(18, 8);
            entity.Property(e => e.High).HasPrecision(18, 8);
            entity.Property(e => e.Low).HasPrecision(18, 8);
            entity.Property(e => e.Close).HasPrecision(18, 8);
        });
    }
}

// In Program.cs with Aspire
builder.AddNpgsqlDbContext<TradingBotDbContext>("tradingbotdb");

// Apply migrations on startup
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<TradingBotDbContext>();
    await dbContext.Database.MigrateAsync();
}
```

### Domain Models

```csharp
public class Purchase
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public decimal Cost { get; set; }
    public decimal Multiplier { get; set; }
    public string Status { get; set; } = ""; // Pending, Filled, Failed
    public string? OrderId { get; set; }
    public string? RawResponse { get; set; } // Store full API response for debugging
}

public class DailyPrice
{
    public DateTime Date { get; set; } // Date only, no time
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
}
```

### Configuration with IOptionsMonitor

```csharp
// appsettings.json
{
  "DcaOptions": {
    "BaseDailyAmount": 10.0,
    "DailyBuyHour": 14,
    "DailyBuyMinute": 0,
    "BearMarketMaPeriod": 200,
    "HighLookbackDays": 30,
    "Multipliers": {
      "Default": 1.0,
      "Drop5Percent": 1.5,
      "Drop10Percent": 2.0,
      "Drop20Percent": 3.0,
      "Drop30Percent": 4.0
    },
    "BearBoostFactor": 1.5
  },
  "Hyperliquid": {
    "IsTestnet": true,
    "ApiUrl": "https://api.hyperliquid-testnet.xyz"
  }
}

// Options class
public class DcaOptions
{
    public decimal BaseDailyAmount { get; set; }
    public int DailyBuyHour { get; set; }
    public int DailyBuyMinute { get; set; }
    public int BearMarketMaPeriod { get; set; }
    public int HighLookbackDays { get; set; }
    public Dictionary<string, decimal> Multipliers { get; set; } = new();
    public decimal BearBoostFactor { get; set; }
}

public class HyperliquidOptions
{
    public bool IsTestnet { get; set; }
    public string ApiUrl { get; set; } = "";
}

// In Program.cs
builder.Services.Configure<DcaOptions>(builder.Configuration.GetSection("DcaOptions"));
builder.Services.Configure<HyperliquidOptions>(builder.Configuration.GetSection("Hyperliquid"));

// In service
public class DcaSchedulerService : BackgroundService
{
    private readonly IOptionsMonitor<DcaOptions> _options;

    public DcaSchedulerService(IOptionsMonitor<DcaOptions> options)
    {
        _options = options;

        // React to configuration changes
        _options.OnChange(newOptions =>
        {
            _logger.LogInformation("DCA configuration updated: {Amount} USD daily",
                newOptions.BaseDailyAmount);
        });
    }

    private void ExecuteDca()
    {
        // Always get current value (hot reload support)
        var config = _options.CurrentValue;
        var amount = config.BaseDailyAmount;
    }
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Microsoft.Extensions.Http.Polly | Microsoft.Extensions.Http.Resilience | .NET 8 (2023) | Zero-allocation design, Polly v8 integration, better telemetry |
| Manual advisory lock SQL | DistributedLock.Postgres library | Mature since 2020 | Handles connection pooling, reentrant locks, automatic cleanup |
| Thread.Sleep for scheduling | PeriodicTimer | .NET 6 (2021) | Non-blocking, async-friendly, better resource usage |
| Timer for BackgroundService | IHostApplicationLifetime events | .NET 6+ (2021) | Cleaner startup/shutdown, better cancellation handling |

**Deprecated/outdated:**
- **Polly direct usage with HttpClientFactory**: Use Microsoft.Extensions.Http.Resilience instead (integrates Polly v8 properly)
- **Session-level advisory locks**: Use transaction-scoped locks for connection pooling compatibility
- **SignTypedData (v1)**: Use SignTypedDataV4 for MetaMask/web3 compatibility

## Open Questions

Things that couldn't be fully resolved:

1. **Hyperliquid EIP-712 Message Structure**
   - What we know: Uses EIP-712, requires signature for all write operations, field ordering matters
   - What's unclear: Exact TypedData structure, domain separator values, complete field list
   - Recommendation: Must analyze Python SDK signing implementation before building .NET version (https://github.com/hyperliquid-dex/hyperliquid-python-sdk)

2. **Spot Order Minimum Size**
   - What we know: Minimum $10 order value mentioned in docs, asset index = 10000 + spotMeta index
   - What's unclear: Minimum BTC quantity, lot size constraints, price precision requirements
   - Recommendation: Test with testnet to determine actual minimums, start with $10 orders

3. **Testnet Balance Acquisition**
   - What we know: Testnet URL is https://api.hyperliquid-testnet.xyz
   - What's unclear: How to fund testnet wallet with USDC, any faucet availability
   - Recommendation: Check Hyperliquid Discord/docs for testnet funding process

4. **PostgreSQL Advisory Lock Key Strategy**
   - What we know: Need unique key for DCA execution lock
   - What's unclear: Should use string hash or numeric ID, collision prevention strategy
   - Recommendation: DistributedLock.Postgres handles key hashing internally, use descriptive string like "dca-execution"

5. **Order Status Polling Strategy**
   - What we know: Can check order status via API, orders have `oid` (order ID)
   - What's unclear: Polling frequency, how long to wait, partial fill handling
   - Recommendation: Poll every 1-2 seconds for 30s max, market orders usually fill immediately

## Sources

### Primary (HIGH confidence)

- [Microsoft Learn: Build resilient HTTP apps](https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience) - Microsoft.Extensions.Http.Resilience patterns
- [Nethereum GitHub: Eip712TypedDataSigner.cs](https://github.com/Nethereum/Nethereum/blob/master/src/Nethereum.Signer.EIP712/Eip712TypedDataSigner.cs) - EIP-712 signing implementation
- [DistributedLock GitHub Repository](https://github.com/madelson/DistributedLock) - PostgreSQL advisory locks
- [Microsoft Learn: Background tasks with hosted services](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-10.0) - BackgroundService patterns
- [Hyperliquid Docs: Exchange endpoint](https://hyperliquid.gitbook.io/hyperliquid-docs/for-developers/api/exchange-endpoint) - Order placement API
- [Hyperliquid Docs: Spot endpoint](https://hyperliquid.gitbook.io/hyperliquid-docs/for-developers/api/info-endpoint/spot) - Spot metadata
- [Hyperliquid Docs: Rate limits](https://hyperliquid.gitbook.io/hyperliquid-docs/for-developers/api/rate-limits-and-user-limits) - API constraints
- [NuGet: Aspire.Npgsql.EntityFrameworkCore.PostgreSQL 13.1.0](https://www.nuget.org/packages/Aspire.Npgsql.EntityFrameworkCore.PostgreSQL) - Official Aspire component

### Secondary (MEDIUM confidence)

- [Turnkey Blog: Hyperliquid secure EIP-712 signing](https://www.turnkey.com/blog/hyperliquid-secure-eip-712-signing) - EIP-712 domain parameters
- [Hyperliquid Docs: Signing](https://hyperliquid.gitbook.io/hyperliquid-docs/for-developers/api/signing) - Common signing mistakes
- [Reformed Programmer: EF Core migrate on startup](https://www.thereformedprogrammer.net/how-to-safely-apply-an-ef-core-migrate-on-asp-net-core-startup/) - Migration patterns
- [Milan Jovanovic: Distributed Locking in .NET](https://www.milanjovanovic.tech/blog/distributed-locking-in-dotnet-coordinating-work-across-multiple-instances) - Lock strategies
- [OneUptime Blog: Advisory Locks in PostgreSQL](https://oneuptime.com/blog/post/2026-01-25-use-advisory-locks-postgresql/view) - Recent advisory lock usage (Jan 2026)
- [C# Corner: Polly v8 Retry and Circuit Breaker](https://www.c-sharpcorner.com/article/build-robust-middleware-in-net-retry-and-circuit-breaker-with-polly-v8/) - Polly v8 patterns

### Tertiary (LOW confidence - needs validation)

- [Chainstack: Hyperliquid User-signed actions](https://docs.chainstack.com/docs/hyperliquid-user-signed-actions) - EIP-712 examples (not .NET specific)
- Web search results mentioning Python SDK structure - Must verify by reading actual SDK code

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - All libraries are official/mature with clear documentation
- Architecture: MEDIUM - EIP-712 signing structure requires Python SDK analysis for validation
- Pitfalls: HIGH - Well-documented issues with advisory locks, retry logic, signature generation

**Research date:** 2026-02-12
**Valid until:** March 2026 (30 days - stable technologies, but Hyperliquid API may evolve)

**Critical next steps:**
1. Analyze Hyperliquid Python SDK signing implementation to extract exact EIP-712 structure
2. Set up testnet wallet and acquire test USDC
3. Implement minimal signing + order placement and validate against testnet
4. Document exact TypedData structure in implementation notes
