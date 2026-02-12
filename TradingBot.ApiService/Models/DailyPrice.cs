using TradingBot.ApiService.BuildingBlocks;

namespace TradingBot.ApiService.Models;

/// <summary>
/// Daily OHLCV candle data for historical price analysis.
/// Used by smart multiplier calculations to determine price drops and MA200.
/// </summary>
public class DailyPrice : AuditedEntity
{
    /// <summary>
    /// Date of the candle (UTC).
    /// Part of composite primary key with Symbol.
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// Trading symbol (e.g., "BTC").
    /// Part of composite primary key with Date.
    /// </summary>
    public string Symbol { get; set; } = "BTC";

    /// <summary>
    /// Opening price for the day.
    /// </summary>
    public decimal Open { get; set; }

    /// <summary>
    /// Highest price during the day.
    /// </summary>
    public decimal High { get; set; }

    /// <summary>
    /// Lowest price during the day.
    /// </summary>
    public decimal Low { get; set; }

    /// <summary>
    /// Closing price for the day.
    /// </summary>
    public decimal Close { get; set; }

    /// <summary>
    /// Trading volume for the day.
    /// </summary>
    public decimal Volume { get; set; }

    /// <summary>
    /// Candle close timestamp (UTC).
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }
}
