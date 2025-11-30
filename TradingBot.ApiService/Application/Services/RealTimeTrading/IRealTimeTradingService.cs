using TradingBot.ApiService.Application.Models;

namespace TradingBot.ApiService.Application.Services.RealTimeTrading;

/// <summary>
/// Service for real-time trading with Binance WebSocket streams
/// </summary>
public interface IRealTimeTradingService
{
    /// <summary>
    /// Start monitoring a symbol with a specific strategy
    /// </summary>
    Task<bool> StartMonitoringAsync(string symbol, string interval, string strategyName, bool autoTrade = false);

    /// <summary>
    /// Stop monitoring a symbol
    /// </summary>
    Task<bool> StopMonitoringAsync(string symbol);

    /// <summary>
    /// Get all active monitoring sessions
    /// </summary>
    List<MonitoringSession> GetActiveMonitoringSessions();

    /// <summary>
    /// Get latest signals for all monitored symbols
    /// </summary>
    Dictionary<string, TradingSignal> GetLatestSignals();
}

/// <summary>
/// Represents an active monitoring session
/// </summary>
public record MonitoringSession
{
    public string Symbol { get; init; } = string.Empty;
    public string Interval { get; init; } = string.Empty;
    public string StrategyName { get; init; } = string.Empty;
    public bool AutoTrade { get; init; }
    public DateTime StartTime { get; init; }
    public TradingSignal? LatestSignal { get; set; }
    public int SignalsGenerated { get; set; }
    public int TradesExecuted { get; set; }
}

