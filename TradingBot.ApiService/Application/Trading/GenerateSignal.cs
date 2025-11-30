using MediatR;
using TradingBot.ApiService.Application.Models;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Application.Services.Strategy;

namespace TradingBot.ApiService.Application.Trading;

public static class GenerateSignal
{
    public record Query(
        string Symbol,
        string StrategyName,
        string Interval = "1h",
        int CandleCount = 100
    ) : IRequest<TradingSignal?>;

    public class Handler : IRequestHandler<Query, TradingSignal?>
    {
        private readonly IHistoricalDataService _historicalDataService;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<Handler> _logger;

        public Handler(
            IHistoricalDataService historicalDataService,
            IServiceProvider serviceProvider,
            ILogger<Handler> logger)
        {
            _historicalDataService = historicalDataService;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public async Task<TradingSignal?> Handle(Query request, CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Generating signal: Strategy={Strategy}, Symbol={Symbol}, Interval={Interval}",
                request.StrategyName, request.Symbol, request.Interval);

            try
            {
                // Fetch historical data
                var candles = await _historicalDataService.GetHistoricalDataAsync(
                    request.Symbol,
                    request.Interval,
                    limit: request.CandleCount,
                    cancellationToken: cancellationToken);

                if (candles.Count < 50)
                {
                    _logger.LogWarning("Insufficient historical data");
                    return null;
                }

                // Get strategy
                var strategy = GetStrategy(request.StrategyName);
                if (strategy == null)
                {
                    _logger.LogWarning("Strategy not found: {Strategy}", request.StrategyName);
                    return null;
                }

                // Generate signal
                var signal = await strategy.AnalyzeAsync(request.Symbol, candles, cancellationToken);

                _logger.LogInformation(
                    "Signal generated: {Type} with confidence {Confidence:P2}. Reason: {Reason}",
                    signal.Type, signal.Confidence, signal.Reason);

                return signal;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating signal");
                return null;
            }
        }

        private IStrategy? GetStrategy(string strategyName)
        {
            return strategyName.ToLower() switch
            {
                "ma crossover" => _serviceProvider.GetService<MovingAverageCrossoverStrategy>(),
                "rsi" => _serviceProvider.GetService<RSIStrategy>(),
                "macd" => _serviceProvider.GetService<MACDStrategy>(),
                _ => null
            };
        }
    }
}


