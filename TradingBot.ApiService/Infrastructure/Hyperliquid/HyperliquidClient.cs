using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Options;
using TradingBot.ApiService.Configuration;
using TradingBot.ApiService.Infrastructure.Hyperliquid.Models;

namespace TradingBot.ApiService.Infrastructure.Hyperliquid;

/// <summary>
/// HTTP client for Hyperliquid REST API (spot trading).
/// Handles price fetching, balance queries, and order placement with EIP-712 authentication.
/// </summary>
public class HyperliquidClient
{
    private readonly HttpClient _http;
    private readonly HyperliquidSigner _signer;
    private readonly HyperliquidOptions _options;
    private readonly ILogger<HyperliquidClient> _logger;

    public HyperliquidClient(
        HttpClient http,
        HyperliquidSigner signer,
        IOptions<HyperliquidOptions> options,
        ILogger<HyperliquidClient> logger)
    {
        _http = http;
        _signer = signer;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// Gets spot market metadata including token list and indices.
    /// </summary>
    public async Task<SpotMetaResponse> GetSpotMetadataAsync(CancellationToken ct = default)
    {
        var request = new { type = "spotMeta" };
        var response = await PostInfoAsync<SpotMetaResponse>(request, ct);
        return response;
    }

    /// <summary>
    /// Gets current BTC spot price from Hyperliquid.
    /// </summary>
    /// <param name="symbol">Symbol name (e.g., "BTC/USDC")</param>
    public async Task<decimal> GetSpotPriceAsync(string symbol = "BTC/USDC", CancellationToken ct = default)
    {
        // First get metadata to find asset index
        var meta = await GetSpotMetadataAsync(ct);
        var assetIndex = meta.Universe.FindIndex(u => u.Name == symbol);

        if (assetIndex == -1)
        {
            throw new HyperliquidApiException($"Symbol {symbol} not found in spot metadata");
        }

        // Get price context
        var request = new { type = "spotMetaAndAssetCtxs" };
        var priceResponse = await PostInfoAsync<SpotMetaAndAssetCtxsResponse>(request, ct);

        if (assetIndex >= priceResponse.Ctx.Count)
        {
            throw new HyperliquidApiException($"Asset index {assetIndex} out of range in price context");
        }

        var ctx = priceResponse.Ctx[assetIndex];
        var price = decimal.Parse(ctx.MarkPx, CultureInfo.InvariantCulture);

        _logger.LogDebug("Fetched {Symbol} price: {Price}", symbol, price);

        return price;
    }

    /// <summary>
    /// Gets USDC balance for the configured wallet.
    /// </summary>
    public async Task<decimal> GetBalancesAsync(CancellationToken ct = default)
    {
        var walletAddress = _signer.GetAddress();
        var request = new
        {
            type = "spotClearinghouseState",
            user = walletAddress
        };

        var response = await PostInfoAsync<UserBalanceResponse>(request, ct);

        // Find USDC balance (token 0 is typically USDC in spot)
        var usdcBalance = response.Balances.FirstOrDefault(b => b.Coin == "USDC");

        if (usdcBalance == null)
        {
            _logger.LogWarning("USDC balance not found for wallet {Wallet}", walletAddress);
            return 0m;
        }

        var balance = decimal.Parse(usdcBalance.Total, CultureInfo.InvariantCulture);
        _logger.LogDebug("USDC balance for {Wallet}: {Balance}", walletAddress, balance);

        return balance;
    }

    /// <summary>
    /// Places a spot buy order on Hyperliquid with EIP-712 signature.
    /// </summary>
    /// <param name="assetIndex">Asset index from spotMeta.universe (e.g., 0 for BTC/USDC)</param>
    /// <param name="isBuy">True for buy, false for sell</param>
    /// <param name="size">Order size in base asset (BTC)</param>
    /// <param name="price">Limit price in quote asset (USDC)</param>
    public async Task<OrderResponse> PlaceSpotOrderAsync(
        int assetIndex,
        bool isBuy,
        decimal size,
        decimal price,
        CancellationToken ct = default)
    {
        // Spot assets start at 10000 + index
        var asset = 10000 + assetIndex;

        // Create order wire format
        var order = new OrderRequest
        {
            Asset = asset,
            IsBuy = isBuy,
            LimitPx = FloatToWire(price),
            Size = FloatToWire(size),
            ReduceOnly = false,
            OrderType = new OrderTypeWire
            {
                Limit = new LimitOrderType
                {
                    Tif = "Ioc" // Immediate-or-cancel for market-like behavior
                }
            }
        };

        var action = new OrderAction
        {
            Type = "order",
            Orders = [order],
            Grouping = "na"
        };

        var nonce = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var signature = _signer.SignOrderAction(action, nonce, _options.IsTestnet);

        var exchangeRequest = new ExchangeRequest
        {
            Action = action,
            Nonce = nonce,
            Signature = signature,
            VaultAddress = null
        };

        _logger.LogInformation("Placing spot order: asset={Asset} side={Side} size={Size} price={Price}",
            asset, isBuy ? "BUY" : "SELL", size, price);

        var response = await PostExchangeAsync<OrderResponse>(exchangeRequest, ct);

        // Check for errors in response
        if (response.Status == "err" || response.Response?.Data?.Statuses.Any(s => s.Error != null) == true)
        {
            var errorMsg = response.Response?.Data?.Statuses.FirstOrDefault(s => s.Error != null)?.Error ?? "Unknown error";
            _logger.LogError("Order placement failed: {Error}", errorMsg);
            throw new HyperliquidApiException($"Order placement failed: {errorMsg}");
        }

        _logger.LogInformation("Order placed successfully: status={Status}", response.Status);

        return response;
    }

    /// <summary>
    /// Gets order status by order ID.
    /// </summary>
    public async Task<OrderStatusResponse> GetOrderStatusAsync(long oid, CancellationToken ct = default)
    {
        var walletAddress = _signer.GetAddress();
        var request = new OrderStatusRequest
        {
            Type = "orderStatus",
            User = walletAddress,
            Oid = oid
        };

        var response = await PostInfoAsync<OrderStatusResponse>(request, ct);
        return response;
    }

    /// <summary>
    /// Converts decimal to string with 8 decimal places and normalized format.
    /// Matches Python SDK float_to_wire logic.
    /// </summary>
    private string FloatToWire(decimal value)
    {
        // Round to 8 decimal places
        var rounded = Math.Round(value, 8);

        // Format with up to 8 decimals, no trailing zeros
        var formatted = rounded.ToString("0.########", CultureInfo.InvariantCulture);

        // Handle -0 edge case
        if (formatted == "-0")
        {
            formatted = "0";
        }

        return formatted;
    }

    /// <summary>
    /// Posts to /info endpoint (read operations).
    /// </summary>
    private async Task<T> PostInfoAsync<T>(object request, CancellationToken ct)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var response = await _http.PostAsJsonAsync("/info", request, jsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Hyperliquid /info request failed: {StatusCode} {Content}",
                response.StatusCode, errorContent);
            throw new HyperliquidApiException($"Hyperliquid API error: {errorContent}", (int)response.StatusCode);
        }

        var result = await response.Content.ReadFromJsonAsync<T>(ct);

        if (result == null)
        {
            throw new HyperliquidApiException("Failed to deserialize Hyperliquid response");
        }

        return result;
    }

    /// <summary>
    /// Posts to /exchange endpoint (write operations: orders, cancels, etc.).
    /// </summary>
    private async Task<T> PostExchangeAsync<T>(object request, CancellationToken ct)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var response = await _http.PostAsJsonAsync("/exchange", request, jsonOptions, ct);
        var content = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Hyperliquid /exchange request failed: {StatusCode} {Content}",
                response.StatusCode, content);
            throw new HyperliquidApiException($"Hyperliquid API error: {content}", (int)response.StatusCode);
        }

        var result = JsonSerializer.Deserialize<T>(content, jsonOptions);

        if (result == null)
        {
            throw new HyperliquidApiException($"Failed to deserialize Hyperliquid response: {content}");
        }

        return result;
    }
}
