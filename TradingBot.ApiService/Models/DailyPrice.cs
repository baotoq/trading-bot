using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.Models.Values;

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
    public Symbol Symbol { get; set; } = Symbol.Btc;

    /// <summary>
    /// Opening price for the day.
    /// </summary>
    public Price Open { get; set; }

    /// <summary>
    /// Highest price during the day.
    /// </summary>
    public Price High { get; set; }

    /// <summary>
    /// Lowest price during the day.
    /// </summary>
    public Price Low { get; set; }

    /// <summary>
    /// Closing price for the day.
    /// </summary>
    public Price Close { get; set; }

    /// <summary>
    /// Trading volume for the day.
    /// Stays decimal: no domain semantics beyond "number of units"
    /// </summary>
    public decimal Volume { get; set; }

    /// <summary>
    /// Candle close timestamp (UTC).
    /// </summary>
    public DateTimeOffset Timestamp { get; set; }
}
