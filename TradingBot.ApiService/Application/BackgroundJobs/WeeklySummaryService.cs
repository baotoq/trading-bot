using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.Configuration;
using TradingBot.ApiService.Infrastructure.Data;
using TradingBot.ApiService.Infrastructure.Hyperliquid;
using TradingBot.ApiService.Infrastructure.Telegram;
using TradingBot.ApiService.Models;

namespace TradingBot.ApiService.Application.BackgroundJobs;

/// <summary>
/// Background service for weekly DCA summary reporting.
/// Sends comprehensive summary on Sunday evening 20:00-21:00 UTC with weekly buys, totals, and P&L.
/// </summary>
public class WeeklySummaryService(
    ILogger<WeeklySummaryService> logger,
    IServiceScopeFactory scopeFactory) : TimeBackgroundService(logger)
{
    private DateOnly _lastSummarySent = DateOnly.MinValue;

    protected override TimeSpan Interval => TimeSpan.FromHours(1);

    protected override async Task ProcessAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        // Check if current UTC time is Sunday and within the 20:00-21:00 UTC window
        if (now.DayOfWeek != DayOfWeek.Sunday)
        {
            return;
        }

        if (now.Hour != 20)
        {
            return;
        }

        // Prevent duplicate summaries for the same day
        var today = DateOnly.FromDateTime(now);
        if (_lastSummarySent == today)
        {
            return;
        }

        try
        {
            logger.LogInformation("Generating weekly DCA summary for week ending {Date}", today);

            await using var scope = scopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<TradingBotDbContext>();
            var telegramService = scope.ServiceProvider.GetRequiredService<TelegramNotificationService>();
            var hyperliquidClient = scope.ServiceProvider.GetRequiredService<HyperliquidClient>();

            // Calculate week boundaries (Monday 00:00 UTC to now)
            var weekStart = GetMondayOfWeek(today);
            var weekStartDateTime = weekStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

            // Query this week's purchases
            var purchases = await dbContext.Purchases
                .Where(p => p.ExecutedAt >= weekStartDateTime)
                .Where(p => p.Status == PurchaseStatus.Filled || p.Status == PurchaseStatus.PartiallyFilled)
                .Where(p => !p.IsDryRun)
                .OrderBy(p => p.ExecutedAt)
                .ToListAsync(cancellationToken);

            // Query lifetime running totals
            var lifetimeTotals = await dbContext.Purchases
                .Where(p => p.Status == PurchaseStatus.Filled || p.Status == PurchaseStatus.PartiallyFilled)
                .Where(p => !p.IsDryRun)
                .GroupBy(p => 1)
                .Select(g => new
                {
                    TotalBtc = g.Sum(p => p.Quantity),
                    TotalUsd = g.Sum(p => p.Cost),
                    Count = g.Count()
                })
                .FirstOrDefaultAsync(cancellationToken);

            // Fetch current BTC price for P&L calculation
            decimal currentPrice;
            try
            {
                currentPrice = await hyperliquidClient.GetSpotPriceAsync("BTC/USDC", cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to fetch current BTC price for weekly summary, using 0");
                currentPrice = 0m;
            }

            // Build message
            var message = BuildWeeklySummaryMessage(
                weekStart,
                today,
                purchases,
                lifetimeTotals,
                currentPrice,
                now);

            await telegramService.SendMessageAsync(message, cancellationToken);

            _lastSummarySent = today;

            logger.LogInformation("Weekly DCA summary sent successfully for week ending {Date}", today);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error generating weekly DCA summary");
        }
    }

    private string BuildWeeklySummaryMessage(
        DateOnly weekStart,
        DateOnly weekEnd,
        List<Purchase> purchases,
        dynamic? lifetimeTotals,
        decimal currentPrice,
        DateTime now)
    {
        var weeklyBuysList = "";
        if (purchases.Count > 0)
        {
            weeklyBuysList = string.Join("\n", purchases.Select(p =>
                $"• {p.ExecutedAt:MMM dd} - `{p.Quantity:F5}` BTC @ `${p.Price:N2}` ({p.Multiplier:F1}x)"));
        }
        else
        {
            weeklyBuysList = "_No purchases this week_";
        }

        // Calculate week totals
        var weekCount = purchases.Count;
        var weekBtc = purchases.Sum(p => p.Quantity);
        var weekUsd = purchases.Sum(p => p.Cost);
        var weekAvgPrice = weekBtc > 0 ? weekUsd / weekBtc : 0m;
        var weekBestPrice = purchases.Any() ? purchases.Min(p => p.Price.Value) : 0m;
        var weekWorstPrice = purchases.Any() ? purchases.Max(p => p.Price.Value) : 0m;

        var weekTotalsSection = weekCount > 0
            ? $"""
            *Week Totals:*
            Buys: `{weekCount}`
            BTC Bought: `{weekBtc:F5}` BTC
            USD Spent: `${weekUsd:N2}`
            Avg Price: `${weekAvgPrice:N2}`
            Best Buy: `${weekBestPrice:N2}`
            Worst Buy: `${weekWorstPrice:N2}`
            """
            : "*Week Totals:*\n_No purchases this week_";

        // Calculate lifetime metrics
        var totalBtc = lifetimeTotals?.TotalBtc ?? 0m;
        var totalUsd = lifetimeTotals?.TotalUsd ?? 0m;
        var totalCount = lifetimeTotals?.Count ?? 0;
        var avgCost = totalBtc > 0 ? totalUsd / totalBtc : 0m;

        // Calculate unrealized P&L
        var pnlSection = "";
        if (currentPrice > 0 && totalBtc > 0)
        {
            var currentValue = totalBtc * currentPrice;
            var unrealizedPnl = currentValue - totalUsd;
            var pnlPercent = totalUsd > 0 ? (unrealizedPnl / totalUsd) * 100 : 0m;

            pnlSection = $"""
            Current Price: `${currentPrice:N2}`
            Unrealized P&L: `{pnlPercent:F1}%` (`${unrealizedPnl:N2}`)
            """;
        }
        else
        {
            pnlSection = currentPrice > 0
                ? $"Current Price: `${currentPrice:N2}`"
                : "_Current price unavailable_";
        }

        return $"""
            *📊 Weekly DCA Summary*
            _Week of {weekStart:MMM dd} - {weekEnd:MMM dd, yyyy}_

            *This Week's Buys:*
            {weeklyBuysList}

            {weekTotalsSection}

            *Lifetime Totals:*
            Total Purchases: `{totalCount}`
            Total BTC: `{totalBtc:F5}` BTC
            Total Spent: `${totalUsd:N2}`
            Avg Cost Basis: `${avgCost:N2}`
            {pnlSection}

            _{now:yyyy-MM-dd HH:mm} UTC_
            """;
    }

    /// <summary>
    /// Returns the Monday of the week containing the given date.
    /// </summary>
    private DateOnly GetMondayOfWeek(DateOnly date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        // Handle Sunday (0) as 7 for calculation
        var daysFromMonday = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
        return date.AddDays(-daysFromMonday);
    }
}
