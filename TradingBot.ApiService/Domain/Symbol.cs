namespace TradingBot.ApiService.Domain;

/// <summary>
/// Represents a trading symbol with validation
/// </summary>
public record Symbol
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
}
