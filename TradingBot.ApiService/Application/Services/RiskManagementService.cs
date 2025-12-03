using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Domain;
using TradingBot.ApiService.Infrastructure;

namespace TradingBot.ApiService.Application.Services;

public class RiskManagementService : IRiskManagementService
{
    private readonly ApplicationDbContext _context;
    private readonly IBinanceService _binanceService;
    private readonly ILogger<RiskManagementService> _logger;
    private readonly IConfiguration _configuration;

    // Risk limits (can be moved to configuration)
    private const int MaxConsecutiveLosses = 3;
    private const int MaxDailyTrades = 5;
    private const decimal MaxDailyDrawdownPercent = 6m;
    private const decimal MinRiskPerTradePercent = 2m;  // Minimum 2% risk
    private const decimal MaxRiskPerTradePercent = 4m;  // Maximum 4% risk
    private const int MaxConcurrentPositions = 3;
    private const decimal MaxTotalExposurePercent = 50m;
    private const decimal MinRiskRewardRatio = 2m;
    private const decimal MaxSpreadPercent = 0.1m;

    public RiskManagementService(
        ApplicationDbContext context,
        IBinanceService binanceService,
        ILogger<RiskManagementService> logger,
        IConfiguration configuration)
    {
        _context = context;
        _binanceService = binanceService;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<RiskCheckResult> ValidateTradeAsync(
        TradingSignal signal,
        PositionParameters parameters,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Validating trade for {Symbol}", signal.Symbol);

        var result = new RiskCheckResult();

        try
        {
            // Check 1: Max consecutive losses
            var consecutiveLosses = await GetConsecutiveLossesAsync(cancellationToken);
            if (consecutiveLosses >= MaxConsecutiveLosses)
            {
                result.IsApproved = false;
                result.RejectionReason = $"Max consecutive losses reached ({consecutiveLosses}/{MaxConsecutiveLosses})";
                result.Violations.Add(result.RejectionReason);
                return result;
            }

            if (consecutiveLosses >= 2)
            {
                result.Warnings.Add($"Warning: {consecutiveLosses} consecutive losses");
            }

            // Check 2: Max daily trades
            var todayTrades = await GetTodayTradeCountAsync(cancellationToken);
            if (todayTrades >= MaxDailyTrades)
            {
                result.IsApproved = false;
                result.RejectionReason = $"Max daily trades reached ({todayTrades}/{MaxDailyTrades})";
                result.Violations.Add(result.RejectionReason);
                return result;
            }

            // Check 3: Daily drawdown
            var todayDrawdown = await GetTodayDrawdownPercentAsync(cancellationToken);
            if (todayDrawdown >= MaxDailyDrawdownPercent)
            {
                result.IsApproved = false;
                result.RejectionReason = $"Max daily drawdown reached ({todayDrawdown:F2}%/{MaxDailyDrawdownPercent}%)";
                result.Violations.Add(result.RejectionReason);
                return result;
            }

            if (todayDrawdown >= MaxDailyDrawdownPercent * 0.75m)
            {
                result.Warnings.Add($"Warning: Daily drawdown at {todayDrawdown:F2}% (limit: {MaxDailyDrawdownPercent}%)");
            }

            // Check 4: Risk per trade (must be between 2% and 4%)
            if (parameters.StopLossPercent < MinRiskPerTradePercent)
            {
                result.IsApproved = false;
                result.RejectionReason = $"Stop-loss too tight ({parameters.StopLossPercent:F2}% < {MinRiskPerTradePercent}%)";
                result.Violations.Add(result.RejectionReason);
                return result;
            }

            if (parameters.StopLossPercent > MaxRiskPerTradePercent)
            {
                result.IsApproved = false;
                result.RejectionReason = $"Stop-loss too wide ({parameters.StopLossPercent:F2}% > {MaxRiskPerTradePercent}%)";
                result.Violations.Add(result.RejectionReason);
                return result;
            }

            // Check 5: Max concurrent positions
            var openPositions = await GetOpenPositionsCountAsync(cancellationToken);
            if (openPositions >= MaxConcurrentPositions)
            {
                result.IsApproved = false;
                result.RejectionReason = $"Max concurrent positions reached ({openPositions}/{MaxConcurrentPositions})";
                result.Violations.Add(result.RejectionReason);
                return result;
            }

            // Check 6: Total exposure
            var accountBalance = await _binanceService.GetFuturesAccountBalanceAsync(cancellationToken);
            var currentExposure = await GetTotalExposureAsync(cancellationToken);
            var newExposure = currentExposure + parameters.PositionSize;
            var newExposurePercent = (newExposure / accountBalance.TotalWalletBalance) * 100;

            if (newExposurePercent > MaxTotalExposurePercent)
            {
                result.IsApproved = false;
                result.RejectionReason = $"Total exposure would exceed limit ({newExposurePercent:F2}% > {MaxTotalExposurePercent}%)";
                result.Violations.Add(result.RejectionReason);
                return result;
            }

            // Check 7: Risk/Reward ratio
            var riskRewardRatio = Math.Abs(parameters.TakeProfit1 - parameters.EntryPrice) /
                                  Math.Abs(parameters.EntryPrice - parameters.StopLoss);

            if (riskRewardRatio < MinRiskRewardRatio)
            {
                result.IsApproved = false;
                result.RejectionReason = $"Risk/reward ratio too low ({riskRewardRatio:F2} < {MinRiskRewardRatio})";
                result.Violations.Add(result.RejectionReason);
                return result;
            }

            // Check 8: Signal confidence
            if (signal.Confidence < 0.5m)
            {
                result.IsApproved = false;
                result.RejectionReason = $"Signal confidence too low ({signal.Confidence * 100:F0}% < 50%)";
                result.Violations.Add(result.RejectionReason);
                return result;
            }

            if (signal.Confidence < 0.7m)
            {
                result.Warnings.Add($"Moderate signal confidence: {signal.Confidence * 100:F0}%");
            }

            // Check 9: Position parameters validation
            if (!parameters.IsValid)
            {
                result.IsApproved = false;
                result.RejectionReason = $"Invalid position parameters: {parameters.ValidationError}";
                result.Violations.Add(result.RejectionReason);
                return result;
            }

            // All checks passed
            result.IsApproved = true;
            _logger.LogInformation(
                "Trade validated successfully for {Symbol}. Warnings: {Warnings}",
                signal.Symbol, result.Warnings.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating trade for {Symbol}", signal.Symbol);
            result.IsApproved = false;
            result.RejectionReason = $"Validation error: {ex.Message}";
            return result;
        }
    }

    public async Task<bool> CanOpenNewPositionAsync(Symbol symbol, CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if there's already an open position for this symbol
            var existingPosition = await _context.Set<Position>()
                .FirstOrDefaultAsync(
                    p => p.Symbol == symbol && p.Status == PositionStatus.Open,
                    cancellationToken);

            if (existingPosition != null)
            {
                _logger.LogWarning("Position already open for {Symbol}", symbol);
                return false;
            }

            // Check max concurrent positions
            var openPositions = await GetOpenPositionsCountAsync(cancellationToken);
            if (openPositions >= MaxConcurrentPositions)
            {
                _logger.LogWarning("Max concurrent positions reached ({Count}/{Max})", openPositions, MaxConcurrentPositions);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if can open position for {Symbol}", symbol);
            return false;
        }
    }

    private async Task<int> GetConsecutiveLossesAsync(CancellationToken cancellationToken)
    {
        var recentTrades = await _context.Set<TradeLog>()
            .Where(t => t.ExitTime != null)
            .OrderByDescending(t => t.ExitTime)
            .Take(10)
            .ToListAsync(cancellationToken);

        int consecutiveLosses = 0;
        foreach (var trade in recentTrades)
        {
            if (!trade.IsWin)
            {
                consecutiveLosses++;
            }
            else
            {
                break;
            }
        }

        return consecutiveLosses;
    }

    private async Task<int> GetTodayTradeCountAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var count = await _context.Set<TradeLog>()
            .CountAsync(t => t.EntryTime >= today, cancellationToken);

        return count;
    }

    private async Task<decimal> GetTodayDrawdownPercentAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var todayTrades = await _context.Set<TradeLog>()
            .Where(t => t.EntryTime >= today && t.ExitTime != null)
            .ToListAsync(cancellationToken);

        if (!todayTrades.Any())
            return 0m;

        var totalPnL = todayTrades.Sum(t => t.RealizedPnL);

        if (totalPnL >= 0)
            return 0m;

        // Get account balance to calculate percentage
        var accountBalance = await _binanceService.GetFuturesAccountBalanceAsync(cancellationToken);
        if (accountBalance.TotalWalletBalance == 0)
            return 0m;

        return Math.Abs((totalPnL / accountBalance.TotalWalletBalance) * 100);
    }

    private async Task<int> GetOpenPositionsCountAsync(CancellationToken cancellationToken)
    {
        return await _context.Set<Position>()
            .CountAsync(p => p.Status == PositionStatus.Open, cancellationToken);
    }

    private async Task<decimal> GetTotalExposureAsync(CancellationToken cancellationToken)
    {
        var openPositions = await _context.Set<Position>()
            .Where(p => p.Status == PositionStatus.Open)
            .ToListAsync(cancellationToken);

        return openPositions.Sum(p => p.Quantity * p.EntryPrice);
    }
}
