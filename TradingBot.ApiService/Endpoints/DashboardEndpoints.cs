using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradingBot.ApiService.Configuration;
using TradingBot.ApiService.Models.Values;

namespace TradingBot.ApiService.Endpoints;

public static class DashboardEndpoints
{
    public static WebApplication MapDashboardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/dashboard")
            .AddEndpointFilter<ApiKeyEndpointFilter>();

        group.MapGet("/portfolio", GetPortfolioAsync);
        group.MapGet("/purchases", GetPurchaseHistoryAsync);
        group.MapGet("/status", GetLiveStatusAsync);
        group.MapGet("/chart", GetPriceChartAsync);
        group.MapGet("/config", GetConfigAsync);

        return app;
    }

    private static async Task<IResult> GetPortfolioAsync(
        Infrastructure.Data.TradingBotDbContext db,
        Infrastructure.Hyperliquid.HyperliquidClient hyperliquidClient,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var purchases = await db.Purchases
            .AsNoTracking()
            .Where(p => !p.IsDryRun && (p.Status == Models.PurchaseStatus.Filled || p.Status == Models.PurchaseStatus.PartiallyFilled))
            .ToListAsync(ct);

        var totalBtc = purchases.Sum(p => p.Quantity);
        var totalCost = purchases.Sum(p => p.Cost);
        var averageCostBasis = totalBtc > 0 ? totalCost / totalBtc : 0;

        decimal currentPrice = 0;
        try
        {
            currentPrice = await hyperliquidClient.GetSpotPriceAsync("BTC/USDC", ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to fetch current BTC price for portfolio");
        }

        var unrealizedPnl = (currentPrice * totalBtc) - totalCost;
        var unrealizedPnlPercent = totalCost > 0 ? (unrealizedPnl / totalCost) * 100 : 0;

        var firstPurchaseDate = purchases.Count > 0 ? purchases.Min(p => p.ExecutedAt) : (DateTimeOffset?)null;
        var lastPurchaseDate = purchases.Count > 0 ? purchases.Max(p => p.ExecutedAt) : (DateTimeOffset?)null;

        var response = new PortfolioResponse(
            TotalBtc: totalBtc,
            TotalCost: totalCost,
            AverageCostBasis: averageCostBasis,
            CurrentPrice: currentPrice,
            UnrealizedPnl: unrealizedPnl,
            UnrealizedPnlPercent: unrealizedPnlPercent,
            TotalPurchaseCount: purchases.Count,
            FirstPurchaseDate: firstPurchaseDate,
            LastPurchaseDate: lastPurchaseDate
        );

        return Results.Ok(response);
    }

    private static async Task<IResult> GetPurchaseHistoryAsync(
        Infrastructure.Data.TradingBotDbContext db,
        string? cursor,
        int pageSize = 20,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        string? tier = null,
        CancellationToken ct = default)
    {
        var query = db.Purchases
            .AsNoTracking()
            .Where(p => !p.IsDryRun && (p.Status == Models.PurchaseStatus.Filled || p.Status == Models.PurchaseStatus.PartiallyFilled));

        if (startDate.HasValue)
        {
            var startDateTime = startDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(p => p.ExecutedAt >= startDateTime);
        }

        if (endDate.HasValue)
        {
            var endDateTime = endDate.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
            query = query.Where(p => p.ExecutedAt <= endDateTime);
        }

        if (!string.IsNullOrEmpty(tier))
        {
            query = tier == "Base"
                ? query.Where(p => p.MultiplierTier == null || p.MultiplierTier == "Base")
                : query.Where(p => p.MultiplierTier == tier);
        }

        if (!string.IsNullOrEmpty(cursor))
        {
            if (DateTimeOffset.TryParse(cursor, out var cursorDate))
            {
                query = query.Where(p => p.ExecutedAt < cursorDate);
            }
        }

        var items = await query
            .OrderByDescending(p => p.ExecutedAt)
            .Take(pageSize + 1)
            .Select(p => new PurchaseDto(
                Id: p.Id,
                ExecutedAt: p.ExecutedAt,
                Price: p.Price,
                Cost: p.Cost,
                Quantity: p.Quantity,
                MultiplierTier: p.MultiplierTier ?? "Base",
                Multiplier: p.Multiplier,
                DropPercentage: p.DropPercentage
            ))
            .ToListAsync(ct);

        var hasMore = items.Count > pageSize;
        var resultItems = hasMore ? items.Take(pageSize).ToList() : items;
        var nextCursor = hasMore ? resultItems.Last().ExecutedAt.ToString("o") : null;

        var response = new PurchaseHistoryResponse(
            Items: resultItems,
            NextCursor: nextCursor,
            HasMore: hasMore
        );

        return Results.Ok(response);
    }

    private static async Task<IResult> GetLiveStatusAsync(
        Infrastructure.Data.TradingBotDbContext db,
        IOptionsMonitor<DcaOptions> dcaOptions,
        CancellationToken ct)
    {
        var lastPurchase = await db.Purchases
            .AsNoTracking()
            .Where(p => !p.IsDryRun && (p.Status == Models.PurchaseStatus.Filled || p.Status == Models.PurchaseStatus.PartiallyFilled))
            .OrderByDescending(p => p.ExecutedAt)
            .FirstOrDefaultAsync(ct);

        string healthStatus;
        string? healthMessage;

        if (lastPurchase == null)
        {
            healthStatus = "Warning";
            healthMessage = "No purchases recorded yet";
        }
        else
        {
            var hoursSinceLastPurchase = (DateTimeOffset.UtcNow - lastPurchase.ExecutedAt).TotalHours;
            if (hoursSinceLastPurchase > 36)
            {
                healthStatus = "Warning";
                healthMessage = $"No purchase in {(int)hoursSinceLastPurchase}h";
            }
            else
            {
                healthStatus = "Healthy";
                healthMessage = "Operating normally";
            }
        }

        var options = dcaOptions.CurrentValue;
        var now = DateTimeOffset.UtcNow;
        var todayBuyTime = new DateTimeOffset(
            now.Year, now.Month, now.Day,
            options.DailyBuyHour, options.DailyBuyMinute, 0,
            TimeSpan.Zero);

        var nextBuyTime = todayBuyTime > now ? todayBuyTime : todayBuyTime.AddDays(1);

        var response = new LiveStatusResponse(
            HealthStatus: healthStatus,
            HealthMessage: healthMessage,
            NextBuyTime: nextBuyTime,
            LastPurchaseTime: lastPurchase?.ExecutedAt,
            LastPurchasePrice: lastPurchase?.Price,
            LastPurchaseBtc: lastPurchase?.Quantity,
            LastPurchaseTier: lastPurchase?.MultiplierTier ?? "Base"
        );

        return Results.Ok(response);
    }

    private static async Task<IResult> GetPriceChartAsync(
        Infrastructure.Data.TradingBotDbContext db,
        string timeframe = "1M",
        CancellationToken ct = default)
    {
        var days = timeframe switch
        {
            "7D" => 7,
            "1M" => 30,
            "3M" => 90,
            "6M" => 180,
            "1Y" => 365,
            "All" => 3650,
            _ => 30
        };

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-days));

        var prices = await db.DailyPrices
            .AsNoTracking()
            .Where(dp => dp.Symbol == Symbol.Btc && dp.Date >= startDate)
            .OrderBy(dp => dp.Date)
            .Select(dp => new PricePointDto(
                Date: dp.Date.ToString("yyyy-MM-dd"),
                Price: dp.Close
            ))
            .ToListAsync(ct);

        var purchases = await db.Purchases
            .AsNoTracking()
            .Where(p => !p.IsDryRun &&
                       (p.Status == Models.PurchaseStatus.Filled || p.Status == Models.PurchaseStatus.PartiallyFilled) &&
                       DateOnly.FromDateTime(p.ExecutedAt.DateTime) >= startDate)
            .OrderBy(p => p.ExecutedAt)
            .Select(p => new PurchaseMarkerDto(
                Date: DateOnly.FromDateTime(p.ExecutedAt.DateTime).ToString("yyyy-MM-dd"),
                Price: p.Price,
                BtcAmount: p.Quantity,
                Tier: p.MultiplierTier ?? "Base"
            ))
            .ToListAsync(ct);

        var allPurchases = await db.Purchases
            .AsNoTracking()
            .Where(p => !p.IsDryRun && (p.Status == Models.PurchaseStatus.Filled || p.Status == Models.PurchaseStatus.PartiallyFilled))
            .ToListAsync(ct);

        var totalBtc = allPurchases.Sum(p => p.Quantity);
        var totalCost = allPurchases.Sum(p => p.Cost);
        var averageCostBasis = totalBtc > 0 ? totalCost / totalBtc : 0;

        var response = new PriceChartResponse(
            Prices: prices,
            Purchases: purchases,
            AverageCostBasis: averageCostBasis
        );

        return Results.Ok(response);
    }

    private static Task<IResult> GetConfigAsync(
        IOptionsMonitor<DcaOptions> dcaOptions)
    {
        var options = dcaOptions.CurrentValue;
        var response = new DcaConfigResponse(
            BaseDailyAmount: options.BaseDailyAmount,
            HighLookbackDays: options.HighLookbackDays,
            BearMarketMaPeriod: options.BearMarketMaPeriod,
            BearBoostFactor: options.BearBoostFactor,
            MaxMultiplierCap: options.MaxMultiplierCap,
            Tiers: options.MultiplierTiers
                .Select(t => new MultiplierTierDto(t.DropPercentage, t.Multiplier))
                .ToList()
        );

        return Task.FromResult(Results.Ok(response));
    }
}

public class ApiKeyEndpointFilter : IEndpointFilter
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiKeyEndpointFilter> _logger;

    public ApiKeyEndpointFilter(IConfiguration configuration, ILogger<ApiKeyEndpointFilter> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var apiKey = httpContext.Request.Headers["x-api-key"].FirstOrDefault();
        var expectedKey = _configuration["Dashboard:ApiKey"];

        if (string.IsNullOrEmpty(expectedKey))
        {
            _logger.LogWarning("Dashboard API key not configured. Set Dashboard:ApiKey in configuration or user-secrets");
            return Results.Problem(
                statusCode: 500,
                title: "API key not configured");
        }

        if (string.IsNullOrEmpty(apiKey))
        {
            _logger.LogWarning("Dashboard API request missing x-api-key header");
            return Results.Problem(
                statusCode: 401,
                title: "Unauthorized",
                detail: "API key required. Include x-api-key header.");
        }

        if (!string.Equals(apiKey, expectedKey, StringComparison.Ordinal))
        {
            _logger.LogWarning("Dashboard API request with invalid API key");
            return Results.Problem(
                statusCode: 403,
                title: "Forbidden",
                detail: "Invalid API key");
        }

        return await next(context);
    }
}
