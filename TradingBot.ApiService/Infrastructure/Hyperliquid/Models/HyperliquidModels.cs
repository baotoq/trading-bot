using System.Text.Json.Serialization;

namespace TradingBot.ApiService.Infrastructure.Hyperliquid.Models;

// Info endpoint responses
public class SpotMetaResponse
{
    [JsonPropertyName("universe")]
    public List<SpotToken> Universe { get; set; } = [];
}

public class SpotToken
{
    [JsonPropertyName("tokens")]
    public List<int> Tokens { get; set; } = [];

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("isCanonical")]
    public bool IsCanonical { get; set; }
}

public class SpotMetaAndAssetCtxsResponse
{
    [JsonPropertyName("ctx")]
    public List<SpotAssetContext> Ctx { get; set; } = [];
}

public class SpotAssetContext
{
    [JsonPropertyName("dayNtlVlm")]
    public string DayNtlVlm { get; set; } = string.Empty;

    [JsonPropertyName("markPx")]
    public string MarkPx { get; set; } = string.Empty;

    [JsonPropertyName("midPx")]
    public string MidPx { get; set; } = string.Empty;

    [JsonPropertyName("prevDayPx")]
    public string PrevDayPx { get; set; } = string.Empty;
}

public class UserBalanceResponse
{
    [JsonPropertyName("balances")]
    public List<TokenBalance> Balances { get; set; } = [];
}

public class TokenBalance
{
    [JsonPropertyName("coin")]
    public string Coin { get; set; } = string.Empty;

    [JsonPropertyName("hold")]
    public string Hold { get; set; } = string.Empty;

    [JsonPropertyName("token")]
    public int Token { get; set; }

    [JsonPropertyName("total")]
    public string Total { get; set; } = string.Empty;
}

// Exchange endpoint requests/responses
public class OrderRequest
{
    [JsonPropertyName("a")]
    public int Asset { get; set; }

    [JsonPropertyName("b")]
    public bool IsBuy { get; set; }

    [JsonPropertyName("p")]
    public string LimitPx { get; set; } = string.Empty;

    [JsonPropertyName("s")]
    public string Size { get; set; } = string.Empty;

    [JsonPropertyName("r")]
    public bool ReduceOnly { get; set; }

    [JsonPropertyName("t")]
    public OrderTypeWire OrderType { get; set; } = new();

    [JsonPropertyName("c")]
    public string? Cloid { get; set; }
}

public class OrderTypeWire
{
    [JsonPropertyName("limit")]
    public LimitOrderType? Limit { get; set; }

    [JsonPropertyName("trigger")]
    public TriggerOrderType? Trigger { get; set; }
}

public class LimitOrderType
{
    [JsonPropertyName("tif")]
    public string Tif { get; set; } = "Gtc";
}

public class TriggerOrderType
{
    [JsonPropertyName("triggerPx")]
    public string TriggerPx { get; set; } = string.Empty;

    [JsonPropertyName("isMarket")]
    public bool IsMarket { get; set; }

    [JsonPropertyName("tpsl")]
    public string Tpsl { get; set; } = string.Empty;
}

public class OrderAction
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "order";

    [JsonPropertyName("orders")]
    public List<OrderRequest> Orders { get; set; } = [];

    [JsonPropertyName("grouping")]
    public string Grouping { get; set; } = "na";
}

public class ExchangeRequest
{
    [JsonPropertyName("action")]
    public OrderAction Action { get; set; } = new();

    [JsonPropertyName("nonce")]
    public long Nonce { get; set; }

    [JsonPropertyName("signature")]
    public SignatureData Signature { get; set; } = new();

    [JsonPropertyName("vaultAddress")]
    public string? VaultAddress { get; set; }
}

public class SignatureData
{
    [JsonPropertyName("r")]
    public string R { get; set; } = string.Empty;

    [JsonPropertyName("s")]
    public string S { get; set; } = string.Empty;

    [JsonPropertyName("v")]
    public int V { get; set; }
}

public class OrderResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("response")]
    public OrderResponseData? Response { get; set; }
}

public class OrderResponseData
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public OrderResponseStatuses? Data { get; set; }
}

public class OrderResponseStatuses
{
    [JsonPropertyName("statuses")]
    public List<OrderStatus> Statuses { get; set; } = [];
}

public class OrderStatus
{
    [JsonPropertyName("resting")]
    public OrderResting? Resting { get; set; }

    [JsonPropertyName("filled")]
    public OrderFilled? Filled { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

public class OrderResting
{
    [JsonPropertyName("oid")]
    public long Oid { get; set; }
}

public class OrderFilled
{
    [JsonPropertyName("totalSz")]
    public string TotalSz { get; set; } = string.Empty;

    [JsonPropertyName("avgPx")]
    public string AvgPx { get; set; } = string.Empty;

    [JsonPropertyName("oid")]
    public long Oid { get; set; }
}

public class OrderStatusRequest
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "orderStatus";

    [JsonPropertyName("user")]
    public string User { get; set; } = string.Empty;

    [JsonPropertyName("oid")]
    public long Oid { get; set; }
}

public class OrderStatusResponse
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("order")]
    public OrderInfo? Order { get; set; }
}

public class OrderInfo
{
    [JsonPropertyName("order")]
    public OrderDetails Order { get; set; } = new();

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("statusTimestamp")]
    public long StatusTimestamp { get; set; }
}

public class OrderDetails
{
    [JsonPropertyName("coin")]
    public string Coin { get; set; } = string.Empty;

    [JsonPropertyName("side")]
    public string Side { get; set; } = string.Empty;

    [JsonPropertyName("limitPx")]
    public string LimitPx { get; set; } = string.Empty;

    [JsonPropertyName("sz")]
    public string Sz { get; set; } = string.Empty;

    [JsonPropertyName("oid")]
    public long Oid { get; set; }

    [JsonPropertyName("timestamp")]
    public long Timestamp { get; set; }

    [JsonPropertyName("origSz")]
    public string OrigSz { get; set; } = string.Empty;
}

// Custom exception for Hyperliquid API errors
public class HyperliquidApiException : Exception
{
    public int? StatusCode { get; }

    public HyperliquidApiException(string message) : base(message)
    {
    }

    public HyperliquidApiException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }

    public HyperliquidApiException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
