using System.Text.Json.Serialization;

namespace TradingBot.ApiService.Infrastructure.PriceFeeds.Etf;

/// <summary>
/// JSON DTO for VNDirect dchart API response.
/// Arrays are parallel — index i across all arrays represents the same data point.
/// </summary>
public record VNDirectDchartResponse(
    [property: JsonPropertyName("t")] long[]? Timestamps,
    [property: JsonPropertyName("c")] decimal[]? Close,
    [property: JsonPropertyName("o")] decimal[]? Open,
    [property: JsonPropertyName("h")] decimal[]? High,
    [property: JsonPropertyName("l")] decimal[]? Low,
    [property: JsonPropertyName("v")] long[]? Volume,
    [property: JsonPropertyName("s")] string? Status);
