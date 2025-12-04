using MediatR;
using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Domain;
using TradingBot.ApiService.Infrastructure;

namespace TradingBot.ApiService.Application.Queries;

public record CheckTrendAlignmentQuery(Symbol Symbol, TradeSide Side) : IRequest<bool>;

public class CheckTrendAlignmentQueryHandler : IRequestHandler<CheckTrendAlignmentQuery, bool>
{
    private readonly ApplicationDbContext _context;
    private readonly ITechnicalIndicatorService _indicatorService;
    private readonly ILogger<CheckTrendAlignmentQueryHandler> _logger;

    public CheckTrendAlignmentQueryHandler(
        ApplicationDbContext context,
        ITechnicalIndicatorService indicatorService,
        ILogger<CheckTrendAlignmentQueryHandler> logger)
    {
        _context = context;
        _indicatorService = indicatorService;
        _logger = logger;
    }

    public async Task<bool> Handle(CheckTrendAlignmentQuery request, CancellationToken cancellationToken)
    {
        var symbol = request.Symbol;
        var side = request.Side;

        _logger.LogInformation("Checking trend alignment for {Symbol} on {Side} side", symbol, side);

        try
        {
            // Get 15m candles for trend check
            var candles = await _context.Candles
                .Where(c => c.Symbol == symbol && c.Interval == "15m")
                .OrderByDescending(c => c.OpenTime)
                .Take(50)
                .OrderBy(c => c.OpenTime)
                .ToListAsync(cancellationToken);

            if (candles.Count < 50)
            {
                _logger.LogWarning("Insufficient candles for trend alignment check");
                return false;
            }

            var ema21 = _indicatorService.CalculateEMA(candles, 21);
            var ema50 = _indicatorService.CalculateEMA(candles, 50);

            bool isAligned = side switch
            {
                TradeSide.Long => ema21 > ema50,
                TradeSide.Short => ema21 < ema50,
                _ => false
            };

            _logger.LogInformation(
                "Trend alignment for {Symbol} {Side}: EMA21={Ema21}, EMA50={Ema50}, Aligned={Aligned}",
                symbol, side, ema21, ema50, isAligned);

            return isAligned;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking trend alignment for {Symbol}", symbol);
            return false;
        }
    }
}
