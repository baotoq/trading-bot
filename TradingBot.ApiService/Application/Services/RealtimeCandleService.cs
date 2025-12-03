using Binance.Net.Clients;
using Binance.Net.Enums;
using Binance.Net.Objects.Models.Spot.Socket;
using CryptoExchange.Net.Sockets;
using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Domain;
using TradingBot.ApiService.Infrastructure;

namespace TradingBot.ApiService.Application.Services;

public class RealtimeCandleService : IRealtimeCandleService, IDisposable
{
    private readonly BinanceSocketClient _socketClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RealtimeCandleService> _logger;
    private readonly Dictionary<string, UpdateSubscription> _subscriptions = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public RealtimeCandleService(
        IServiceProvider serviceProvider,
        ILogger<RealtimeCandleService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _socketClient = new BinanceSocketClient();
    }

    public async Task StartMonitoringAsync(string symbol, string interval, CancellationToken cancellationToken = default)
    {
        var key = GetKey(symbol, interval);

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            if (_subscriptions.ContainsKey(key))
            {
                _logger.LogWarning("Already monitoring {Symbol} on {Interval}", symbol, interval);
                return;
            }

            var klineInterval = ParseInterval(interval);

            _logger.LogInformation("Starting real-time monitoring for {Symbol} on {Interval}", symbol, interval);

            var result = await _socketClient.SpotApi.ExchangeData.SubscribeToKlineUpdatesAsync(
                symbol,
                klineInterval,
                async data => await OnCandleUpdateAsync(data.Data, cancellationToken),
                cancellationToken);

            if (result.Success)
            {
                _subscriptions[key] = result.Data;
                _logger.LogInformation("Successfully subscribed to {Symbol} {Interval} kline updates", symbol, interval);
            }
            else
            {
                _logger.LogError("Failed to subscribe to {Symbol} {Interval}: {Error}",
                    symbol, interval, result.Error?.Message);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task StopMonitoringAsync(string symbol, string interval)
    {
        var key = GetKey(symbol, interval);

        await _semaphore.WaitAsync();
        try
        {
            if (_subscriptions.TryGetValue(key, out var subscription))
            {
                await subscription.CloseAsync();
                _subscriptions.Remove(key);
                _logger.LogInformation("Stopped monitoring {Symbol} on {Interval}", symbol, interval);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public bool IsMonitoring(string symbol, string interval)
    {
        var key = GetKey(symbol, interval);
        return _subscriptions.ContainsKey(key);
    }

    public IReadOnlyList<(string Symbol, string Interval)> GetActiveMonitors()
    {
        return _subscriptions.Keys
            .Select(k =>
            {
                var parts = k.Split('_');
                return (parts[0], parts[1]);
            })
            .ToList();
    }

    private async Task OnCandleUpdateAsync(BinanceStreamKlineData klineData, CancellationToken cancellationToken)
    {
        try
        {
            var kline = klineData.Data;

            // Only process completed candles
            if (!kline.Final)
            {
                return;
            }

            _logger.LogDebug("Received completed candle for {Symbol}: Open={Open}, Close={Close}, Volume={Volume}",
                klineData.Symbol, kline.OpenPrice, kline.ClosePrice, kline.Volume);

            // Create a scope to get scoped services
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Check if candle already exists
            var existingCandle = await context.Candles
                .FirstOrDefaultAsync(c =>
                    c.Symbol == klineData.Symbol &&
                    c.Interval == klineData.Data.Interval.ToString() &&
                    c.OpenTime == kline.OpenTime,
                    cancellationToken);

            if (existingCandle != null)
            {
                // Update existing candle
                existingCandle.OpenPrice = kline.OpenPrice;
                existingCandle.HighPrice = kline.HighPrice;
                existingCandle.LowPrice = kline.LowPrice;
                existingCandle.ClosePrice = kline.ClosePrice;
                existingCandle.Volume = kline.Volume;
                existingCandle.CloseTime = kline.CloseTime;
                existingCandle.QuoteAssetVolume = kline.QuoteVolume;
                existingCandle.NumberOfTrades = (int)kline.TradeCount;
                existingCandle.UpdatedAt = DateTime.UtcNow;

                _logger.LogDebug("Updated existing candle for {Symbol} at {OpenTime}", klineData.Symbol, kline.OpenTime);
            }
            else
            {
                // Insert new candle
                var candle = new Candle
                {
                    Symbol = klineData.Symbol,
                    Interval = klineData.Data.Interval.ToString(),
                    OpenTime = kline.OpenTime,
                    OpenPrice = kline.OpenPrice,
                    HighPrice = kline.HighPrice,
                    LowPrice = kline.LowPrice,
                    ClosePrice = kline.ClosePrice,
                    Volume = kline.Volume,
                    CloseTime = kline.CloseTime,
                    QuoteAssetVolume = kline.QuoteVolume,
                    NumberOfTrades = (int)kline.TradeCount
                };

                context.Candles.Add(candle);
                _logger.LogInformation("New candle saved for {Symbol} {Interval}: Close=${Close}, Volume={Volume}",
                    klineData.Symbol, klineData.Data.Interval, kline.ClosePrice, kline.Volume);
            }

            await context.SaveChangesAsync(cancellationToken);

            // Trigger signal generation for this symbol
            var signalGenerator = scope.ServiceProvider.GetRequiredService<ISignalGeneratorService>();
            await signalGenerator.GenerateSignalAsync(klineData.Symbol, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing candle update for {Symbol}", klineData.Symbol);
        }
    }

    private static string GetKey(string symbol, string interval) => $"{symbol}_{interval}";

    private static KlineInterval ParseInterval(string interval)
    {
        return interval.ToLower() switch
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

    public void Dispose()
    {
        _socketClient?.Dispose();
        _semaphore?.Dispose();
    }
}
