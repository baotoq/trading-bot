using Microsoft.EntityFrameworkCore;

namespace TradingBot.ApiService.Endpoints;

public static class DashboardEndpoints
{
    public static WebApplication MapDashboardEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/dashboard")
            .AddEndpointFilter<ApiKeyEndpointFilter>();

        group.MapGet("/portfolio", GetPortfolioAsync);

        return app;
    }

    private static async Task<IResult> GetPortfolioAsync(
        Infrastructure.Data.TradingBotDbContext db,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        // Placeholder portfolio endpoint -- real implementation in Phase 10
        var totalPurchases = await db.Purchases
            .Where(p => !p.IsDryRun && (p.Status == Models.PurchaseStatus.Filled || p.Status == Models.PurchaseStatus.PartiallyFilled))
            .CountAsync(ct);

        return Results.Ok(new
        {
            totalPurchases,
            message = "Portfolio endpoint ready. Full implementation in Phase 10."
        });
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
