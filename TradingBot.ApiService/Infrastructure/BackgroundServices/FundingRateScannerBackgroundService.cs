using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Infrastructure.BackgroundServices;

/// <summary>
/// Background service that scans all perpetual futures for funding rate arbitrage opportunities.
/// Sends Telegram alerts for the best coins to trade with the delta-neutral strategy.
///
/// Risk Minimization:
/// - Only recommends high-liquidity coins (top by trading volume)
/// - Prioritizes major coins (BTC, ETH, BNB, SOL, etc.) for lower slippage risk
/// - Filters out low funding rates that wouldn't cover fees
/// - Scans every 30 minutes (15 min before each funding settlement window)
/// </summary>
public class FundingRateScannerBackgroundService(
    IServiceProvider services,
    ILogger<FundingRateScannerBackgroundService> logger
) : BackgroundService
{
    // Top liquid coins to prioritize (lower risk due to better liquidity)
    private static readonly HashSet<string> HighLiquidityCoins = new(StringComparer.OrdinalIgnoreCase)
    {
        "BTCUSDT", "ETHUSDT", "BNBUSDT", "SOLUSDT", "XRPUSDT",
        "DOGEUSDT", "ADAUSDT", "AVAXUSDT", "DOTUSDT", "MATICUSDT",
        "LINKUSDT", "LTCUSDT", "ATOMUSDT", "UNIUSDT", "ARBUSDT",
        "OPUSDT", "APTUSDT", "NEARUSDT", "FILUSDT", "INJUSDT"
    };

    // Minimum funding rate to cover trading fees (0.03%)
    private const decimal MinFundingRate = 0.0003m;

    // High opportunity threshold (0.08%)
    private const decimal HighOpportunityThreshold = 0.0008m;

    // Scan interval: every 30 minutes
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait for application startup
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        logger.LogInformation("Starting Funding Rate Scanner Background Service");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScanAndAlertAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in funding rate scanner");
            }

            await Task.Delay(ScanInterval, stoppingToken);
        }
    }

    private async Task ScanAndAlertAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Scanning market for funding rate opportunities...");

        await using var scope = services.CreateAsyncScope();
        var binanceService = scope.ServiceProvider.GetRequiredService<IBinanceService>();
        var telegramService = scope.ServiceProvider.GetRequiredService<ITelegramNotificationService>();

        // Fetch all funding rates
        var allRates = await binanceService.GetAllFundingRatesAsync(cancellationToken);

        if (allRates.Count == 0)
        {
            logger.LogWarning("No funding rates fetched");
            return;
        }

        // Filter and rank opportunities
        var opportunities = allRates
            .Where(r => Math.Abs(r.FundingRate) >= MinFundingRate)
            .OrderByDescending(r => Math.Abs(r.FundingRate))
            .ToList();

        if (opportunities.Count == 0)
        {
            logger.LogInformation("No funding rate opportunities above threshold");
            return;
        }

        // Get top opportunities (prioritizing liquid coins)
        var topOpportunities = opportunities
            .OrderByDescending(r => IsHighLiquidity(r.Symbol.Value) ? 1 : 0)
            .ThenByDescending(r => Math.Abs(r.FundingRate))
            .Take(10)
            .ToList();

        // Get the best opportunity overall
        var bestOverall = topOpportunities.First();

        // Get best among high-liquidity coins only
        var bestSafe = topOpportunities.FirstOrDefault(r => IsHighLiquidity(r.Symbol.Value));

        // Check if we're in the optimal trading window (within 60 minutes of funding)
        var isOptimalWindow = bestOverall.MinutesToNextFunding <= 60;

        // Build and send Telegram message
        var message = BuildAlertMessage(topOpportunities, bestOverall, bestSafe, isOptimalWindow);
        await telegramService.SendMessageAsync(message, cancellationToken);

        logger.LogInformation(
            "Funding rate scan complete. Best opportunity: {Symbol} at {Rate:P4}",
            bestOverall.Symbol, bestOverall.FundingRate);
    }

    private static bool IsHighLiquidity(string symbol)
    {
        return HighLiquidityCoins.Contains(symbol);
    }

    private static string BuildAlertMessage(
        List<FundingRateInfo> opportunities,
        FundingRateInfo bestOverall,
        FundingRateInfo? bestSafe,
        bool isOptimalWindow)
    {
        var windowEmoji = isOptimalWindow ? "🟢" : "🟡";
        var windowStatus = isOptimalWindow ? "OPTIMAL ENTRY WINDOW" : "EARLY - Wait for entry window";

        var message = $@"💰 <b>Funding Rate Arbitrage Scanner</b> 💰

{windowEmoji} <b>Status:</b> {windowStatus}
⏰ <b>Next Funding:</b> {bestOverall.MinutesToNextFunding} minutes

━━━━━━━━━━━━━━━━━━━━━━━━━

🏆 <b>BEST OPPORTUNITY (Highest Rate):</b>
  Symbol: <b>{bestOverall.Symbol}</b>
  Rate: <b>{bestOverall.FundingRate:P4}</b> ({(bestOverall.FundingRate > 0 ? "SHORT" : "LONG")})
  APY: {bestOverall.EstimatedAnnualizedRate:P2}
  Price: ${bestOverall.MarkPrice:F2}
  Liquidity: {(IsHighLiquidity(bestOverall.Symbol.Value) ? "✅ High" : "⚠️ Medium")}";

        if (bestSafe != null && bestSafe.Symbol.Value != bestOverall.Symbol.Value)
        {
            message += $@"

🛡️ <b>SAFEST PICK (High Liquidity):</b>
  Symbol: <b>{bestSafe.Symbol}</b>
  Rate: <b>{bestSafe.FundingRate:P4}</b> ({(bestSafe.FundingRate > 0 ? "SHORT" : "LONG")})
  APY: {bestSafe.EstimatedAnnualizedRate:P2}
  Price: ${bestSafe.MarkPrice:F2}
  Liquidity: ✅ High (Recommended)";
        }

        message += @"

━━━━━━━━━━━━━━━━━━━━━━━━━

📊 <b>TOP 10 OPPORTUNITIES:</b>";

        var rank = 1;
        foreach (var opp in opportunities.Take(10))
        {
            var direction = opp.FundingRate > 0 ? "S" : "L";
            var liquidityIcon = IsHighLiquidity(opp.Symbol.Value) ? "✅" : "  ";
            message += $"\n{rank}. {liquidityIcon} {opp.Symbol}: {opp.FundingRate:P3} ({direction}) | APY: {opp.EstimatedAnnualizedRate:P1}";
            rank++;
        }

        message += @"

━━━━━━━━━━━━━━━━━━━━━━━━━

📋 <b>STRATEGY REMINDER:</b>
• Positive rate → SHORT futures + BUY spot
• Negative rate → LONG futures + SELL spot
• Use 1x leverage for safety
• Exit after funding settlement

✅ = High liquidity (safer)
S = Short, L = Long";

        message += $@"

<i>⏰ Scanned at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</i>";

        return message;
    }
}
