using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TradingBot.ApiService.Domain;


public class SymbolJsonConverter : JsonConverter<Symbol>
{
    public override Symbol Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString();
        return new Symbol(value!);
    }

    public override void Write(Utf8JsonWriter writer, Symbol value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

public record Symbol : IParsable<Symbol>
{
    public string Value { get; }

    public Symbol(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Symbol cannot be null or empty", nameof(value));
        }

        // Normalize to uppercase
        Value = value.ToUpperInvariant().Trim();

        // Validate format (alphanumeric, typically ends with USDT, BUSD, etc.)
        if (!IsValidSymbolFormat(Value))
        {
            throw new ArgumentException($"Invalid symbol format: {value}", nameof(value));
        }
    }

    private static bool IsValidSymbolFormat(string symbol)
    {
        // Symbol should be alphanumeric and at least 3 characters
        // Examples: BTCUSDT, ETHUSDT, BNBBUSD
        return symbol.Length >= 3 && symbol.All(char.IsLetterOrDigit);
    }

    public static implicit operator Symbol(string value) => new Symbol(value);

    public static implicit operator string(Symbol symbol) => symbol.Value;

    public override string ToString() => Value;

    /// <summary>
    /// Gets the base currency from the symbol (e.g., "BTC" from "BTCUSDT")
    /// </summary>
    public string GetBaseCurrency()
    {
        // Common quote currencies
        var quoteCurrencies = new[] { "USDT", "BUSD", "USDC", "BTC", "ETH", "BNB" };

        foreach (var quote in quoteCurrencies)
        {
            if (Value.EndsWith(quote) && Value.Length > quote.Length)
            {
                return Value[..^quote.Length];
            }
        }

        // If no common quote currency found, return first 3 characters as a fallback
        return Value.Length > 3 ? Value[..3] : Value;
    }

    /// <summary>
    /// Gets the quote currency from the symbol (e.g., "USDT" from "BTCUSDT")
    /// </summary>
    public string GetQuoteCurrency()
    {
        var quoteCurrencies = new[] { "USDT", "BUSD", "USDC", "BTC", "ETH", "BNB" };

        foreach (var quote in quoteCurrencies)
        {
            if (Value.EndsWith(quote))
            {
                return quote;
            }
        }

        // If no common quote currency found, return last 3-4 characters as a fallback
        return Value.Length > 3 ? Value[^4..] : Value;
    }

    // IParsable<Symbol> implementation
    public static Symbol Parse(string s, IFormatProvider? provider)
    {
        return new Symbol(s);
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, [MaybeNullWhen(false)] out Symbol result)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            result = null;
            return false;
        }

        try
        {
            result = new Symbol(s);
            return true;
        }
        catch
        {
            result = null;
            return false;
        }
    }
}
