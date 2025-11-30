using MediatR;
using TradingBot.ApiService.Application.Services.Backtesting;

namespace TradingBot.ApiService.Application.Trading;

public static class RunBacktest
{
    public record Query(
        string StrategyName,
        string Symbol,
        string Interval,
        DateTime StartDate,
        DateTime EndDate,
        decimal InitialCapital = 10000m,
        decimal PositionSize = 0.1m
    ) : IRequest<BacktestResult?>;

    public class Handler : IRequestHandler<Query, BacktestResult?>
    {
        private readonly IBacktestingService _backtestingService;
        private readonly ILogger<Handler> _logger;

        public Handler(IBacktestingService backtestingService, ILogger<Handler> logger)
        {
            _backtestingService = backtestingService;
            _logger = logger;
        }

        public async Task<BacktestResult?> Handle(Query request, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Running backtest: Strategy={Strategy}, Symbol={Symbol}, Period={Start} to {End}",
                request.StrategyName, request.Symbol, request.StartDate, request.EndDate);

            try
            {
                var result = await _backtestingService.RunBacktestAsync(
                    request.StrategyName,
                    request.Symbol,
                    request.Interval,
                    request.StartDate,
                    request.EndDate,
                    request.InitialCapital,
                    request.PositionSize,
                    cancellationToken);

                _logger.LogInformation(
                    "Backtest completed: Return={Return:P2}, Trades={Trades}, WinRate={WinRate:P2}",
                    result.ReturnPercentage / 100, result.TotalTrades, result.WinRate / 100);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running backtest");
                return null;
            }
        }
    }
}


