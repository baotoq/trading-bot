using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradingBot.ApiService.Application.Strategies;

public class StrategyNameJsonConverter : JsonConverter<StrategyName>
{
    public override StrategyName Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
        {
            throw new JsonException($"Expected string value for {nameof(StrategyName)}");
        }

        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException($"{nameof(StrategyName)} cannot be null or empty");
        }

        // Try to parse using enum name (case-insensitive)
        if (Enum.TryParse<StrategyName>(value, ignoreCase: true, out var result))
        {
            return result;
        }

        // Try fuzzy matching
        var normalized = value.Replace(" ", "").Trim();
        foreach (StrategyName strategy in Enum.GetValues<StrategyName>())
        {
            var enumName = strategy.ToString();
            if (enumName.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            {
                return strategy;
            }
        }

        throw new JsonException(
            $"Unknown strategy: '{value}'. Available strategies: {string.Join(", ", Enum.GetNames<StrategyName>())}");
    }

    public override void Write(Utf8JsonWriter writer, StrategyName value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
