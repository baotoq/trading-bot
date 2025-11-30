using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace TradingBot.ApiService.Domain;

/// <summary>
/// Entity for storing historical candlestick data in the database
/// </summary>
[Index(nameof(Symbol), nameof(Interval), nameof(OpenTime), IsUnique = true)]
[Index(nameof(Symbol), nameof(Interval), nameof(OpenTime))]
public class Candle
{
    [Key] public long Id { get; set; }

    [Required] [MaxLength(20)] public string Symbol { get; set; } = string.Empty;

    [Required] [MaxLength(10)] public string Interval { get; set; } = string.Empty;

    public DateTimeOffset OpenTime { get; set; }

    public decimal Open { get; set; }

    public decimal High { get; set; }

    public decimal Low { get; set; }

    public decimal Close { get; set; }

    public decimal Volume { get; set; }

    public DateTimeOffset CloseTime { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

