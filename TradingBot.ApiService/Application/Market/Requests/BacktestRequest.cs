using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Requests;

/// <summary>
/// Request to run a backtest for a single strategy
/// </summary>
public record BacktestRequest(
    Symbol Symbol,
    string Strategy,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    decimal InitialCapital = 10000m,
    decimal RiskPercent = 1.5m);

/// <summary>
/// Request to compare multiple strategies
/// </summary>
public record CompareRequest(
    Symbol Symbol,
    List<string> Strategies,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    decimal InitialCapital = 10000m,
    decimal RiskPercent = 1.5m);
