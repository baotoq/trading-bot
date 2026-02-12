using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TradingBot.ApiService.Infrastructure.Data;
using TradingBot.ApiService.Models;

namespace TradingBot.ApiService.Application.Health;

/// <summary>
/// Health check for DCA service operation.
/// Reports last purchase timestamp and alerts if no purchase in 36+ hours.
/// </summary>
public class DcaHealthCheck(IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TradingBotDbContext>();

            var lastPurchase = await dbContext.Purchases
                .Where(p => p.Status == PurchaseStatus.Filled || p.Status == PurchaseStatus.PartiallyFilled)
                .Where(p => !p.IsDryRun)
                .OrderByDescending(p => p.ExecutedAt)
                .FirstOrDefaultAsync(cancellationToken);

            var data = new Dictionary<string, object>();

            if (lastPurchase != null)
            {
                data["last_purchase"] = lastPurchase.ExecutedAt.ToString("o");
                data["last_purchase_status"] = lastPurchase.Status.ToString();
                data["last_purchase_btc"] = lastPurchase.Quantity.ToString("F5");

                var hoursSince = (DateTimeOffset.UtcNow - lastPurchase.ExecutedAt).TotalHours;
                if (hoursSince > 36)
                {
                    return HealthCheckResult.Degraded(
                        $"No purchase in {hoursSince:F0} hours",
                        data: data);
                }

                return HealthCheckResult.Healthy("DCA service operating normally", data: data);
            }
            else
            {
                data["last_purchase"] = "never";
                return HealthCheckResult.Degraded("No purchases recorded yet", data: data);
            }
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Failed to check DCA service health", ex);
        }
    }
}
