using Binance.Net.Enums;

namespace TradingBot.ApiService.Domain;

public record CandleInterval
{
    public string Value { get; }

    public CandleInterval(string value)
    {
        Value = value;
    }

    public static implicit operator CandleInterval(string value) => new CandleInterval(value);

    public static implicit operator string(CandleInterval ci) => ci.Value;

    public KlineInterval ToKlineInterval()
    {
        return Value.ToLower() switch
        {
            "1m" => KlineInterval.OneMinute,
            "5m" => KlineInterval.FiveMinutes,
            "15m" => KlineInterval.FifteenMinutes,
            "30m" => KlineInterval.ThirtyMinutes,
            "1h" => KlineInterval.OneHour,
            "4h" => KlineInterval.FourHour,
            "1d" => KlineInterval.OneDay,
            "1w" => KlineInterval.OneWeek,
            _ => KlineInterval.OneHour
        };
    }
}