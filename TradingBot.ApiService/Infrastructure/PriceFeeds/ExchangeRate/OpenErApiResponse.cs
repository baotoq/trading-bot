using System.Text.Json.Serialization;

namespace TradingBot.ApiService.Infrastructure.PriceFeeds.ExchangeRate;

/// <summary>
/// JSON DTO for open.er-api.com /v6/latest/USD response.
/// </summary>
public record OpenErApiResponse(
    [property: JsonPropertyName("result")] string? Result,
    [property: JsonPropertyName("base_code")] string? BaseCode,
    [property: JsonPropertyName("time_last_update_unix")] long TimeLastUpdateUnix,
    [property: JsonPropertyName("rates")] Dictionary<string, decimal>? Rates);
