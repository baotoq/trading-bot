using Binance.Net.Clients;
using Binance.Net.Interfaces;
using TradingBot.ApiService.Domain;
using MediatR;
using TradingBot.ApiService.Application.Candles.DomainEvents;

namespace TradingBot.ApiService.Application.Services;

public interface IRealtimeCandleService
{
    Task StartMonitoringAsync(Symbol symbol, CandleInterval interval, CancellationToken cancellationToken = default);
    Task StopMonitoringAsync(Symbol symbol, CandleInterval interval);
    bool IsMonitoring(Symbol symbol, CandleInterval interval);
    IReadOnlyList<(Symbol Symbol, CandleInterval Interval)> GetActiveMonitors();
}

public class RealtimeCandleService : IRealtimeCandleService, IDisposable
{
    private readonly BinanceSocketClient _socketClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RealtimeCandleService> _logger;
    private readonly Dictionary<string, CryptoExchange.Net.Objects.Sockets.UpdateSubscription> _subscriptions = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public RealtimeCandleService(
        IServiceProvider serviceProvider,
        ILogger<RealtimeCandleService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _socketClient = new BinanceSocketClient();
    }

    public async Task StartMonitoringAsync(Symbol symbol, CandleInterval interval, CancellationToken cancellationToken = default)
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

            _logger.LogInformation("Starting real-time monitoring for {Symbol} on {Interval}", symbol, interval);

            var result = await _socketClient.SpotApi.ExchangeData.SubscribeToKlineUpdatesAsync(
                symbol.Value,
                interval.ToKlineInterval(),
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

    public async Task StopMonitoringAsync(Symbol symbol, CandleInterval interval)
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

    public bool IsMonitoring(Symbol symbol, CandleInterval interval)
    {
        var key = GetKey(symbol, interval);
        return _subscriptions.ContainsKey(key);
    }

    public IReadOnlyList<(Symbol Symbol, CandleInterval Interval)> GetActiveMonitors()
    {
        return _subscriptions.Keys
            .Select(k =>
            {
                var parts = k.Split('_');
                return (new Symbol(parts[0]), new CandleInterval(parts[1]));
            })
            .ToList();
    }

    private async Task OnCandleUpdateAsync(IBinanceStreamKlineData klineData, CancellationToken cancellationToken)
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

            await using var scope = _serviceProvider.CreateAsyncScope();
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

            await mediator.Publish(new CandleClosedDomainEvent(
                klineData.Symbol,
                new CandleInterval(klineData.Data.Interval.ToString()),
                kline.OpenTime,
                kline.CloseTime,
                kline.OpenPrice,
                kline.ClosePrice,
                kline.HighPrice,
                kline.LowPrice,
                kline.Volume
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing candle update for {Symbol}", klineData.Symbol);
        }
    }

    private static string GetKey(Symbol symbol, CandleInterval interval) => $"{symbol.Value}_{interval}";

    public void Dispose()
    {
        _socketClient?.Dispose();
        _semaphore?.Dispose();
    }
}
