using Binance.Net.Enums;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradingBot.ApiService.Domain;

public class CandleIntervalJsonConverter : JsonConverter<CandleInterval>
{
    public override CandleInterval Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return new CandleInterval(value!);
    }

    public override void Write(Utf8JsonWriter writer, CandleInterval value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

public record CandleInterval
{
    public string Value { get; }

    public CandleInterval(string value)
    {
        Value = value;
    }

    public static implicit operator CandleInterval(string value) => new(value);

    public static implicit operator string(CandleInterval ci) => ci.Value;

    public KlineInterval ToKlineInterval()
    {
        return Value.ToLower() switch
        {
            "1m" => KlineInterval.OneMinute,
            "3m" => KlineInterval.ThreeMinutes,
            "5m" => KlineInterval.FiveMinutes,
            "15m" => KlineInterval.FifteenMinutes,
            "30m" => KlineInterval.ThirtyMinutes,
            "1h" => KlineInterval.OneHour,
            "2h" => KlineInterval.TwoHour,
            "4h" => KlineInterval.FourHour,
            "6h" => KlineInterval.SixHour,
            "8h" => KlineInterval.EightHour,
            "12h" => KlineInterval.TwelveHour,
            "1d" => KlineInterval.OneDay,
            "3d" => KlineInterval.ThreeDay,
            "1w" => KlineInterval.OneWeek,
            "1M" => KlineInterval.OneMonth,
            _ => KlineInterval.FiveMinutes
        };
    }
}