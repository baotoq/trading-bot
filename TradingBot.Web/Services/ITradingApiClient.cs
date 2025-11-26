using Refit;
using TradingBot.Web.Models;

namespace TradingBot.Web.Services;

/// <summary>
/// Refit interface for Trading API
/// </summary>
public interface ITradingApiClient
{
    [Post("/trading/signal")]
    Task<TradingSignal> GenerateSignalAsync([Body] GenerateSignalRequest request, CancellationToken cancellationToken = default);

    [Post("/trading/backtest")]
    Task<BacktestResult> RunBacktestAsync([Body] BacktestRequest request, CancellationToken cancellationToken = default);

    [Post("/trading/order")]
    Task<OrderResult> PlaceOrderAsync([Body] PlaceOrderRequest request, CancellationToken cancellationToken = default);
}

// Request DTOs
public record GenerateSignalRequest(
    string Symbol,
    string StrategyName,
    string Interval = "1h",
    int CandleCount = 100);

public record BacktestRequest(
    string StrategyName,
    string Symbol,
    string Interval,
    DateTime StartDate,
    DateTime EndDate,
    decimal InitialCapital = 10000m,
    decimal PositionSize = 0.1m);

public record PlaceOrderRequest(
    string Symbol,
    string Side,
    string Type,
    decimal Quantity,
    decimal? Price = null,
    decimal? StopPrice = null);

