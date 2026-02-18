using Vogen;

namespace TradingBot.ApiService.Models.Values;

[ValueObject<string>]
public readonly partial struct Symbol
{
    private static Validation Validate(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 20
            ? Validation.Ok
            : Validation.Invalid("Symbol must be non-empty and at most 20 characters");

    // Well-known domain constants for compile-time safety
    public static readonly Symbol Btc = From("BTC");
    public static readonly Symbol BtcUsdc = From("BTC/USDC");
}
