using Binance.Net.Enums;
using Binance.Net.Interfaces.Clients;
using TradingBot.ApiService.Application.Models;

namespace TradingBot.ApiService.Application.Services;

public class HistoricalDataService : IHistoricalDataService
{
    private readonly IBinanceRestClient _binanceClient;
    private readonly ILogger<HistoricalDataService> _logger;

    public HistoricalDataService(
        IBinanceRestClient binanceClient,
        ILogger<HistoricalDataService> logger)
    {
        _binanceClient = binanceClient;
        _logger = logger;
    }

    public async Task<List<Candle>> GetHistoricalDataAsync(
        string symbol,
        string interval,
        DateTimeOffset? startTime = null,
        DateTimeOffset? endTime = null,
        int? limit = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var klineInterval = ParseInterval(interval);

            var result = await _binanceClient.SpotApi.ExchangeData.GetKlinesAsync(
                symbol,
                klineInterval,
                startTime.Value.DateTime,
                endTime.Value.DateTime,
                limit,
                ct: cancellationToken);

            if (!result.Success)
            {
                _logger.LogError("Failed to fetch historical data: {Error}", result.Error?.Message);
                return [];
            }

            var candles = result.Data.Select(k => new Candle
            {
                OpenTime = k.OpenTime,
                Open = k.OpenPrice,
                High = k.HighPrice,
                Low = k.LowPrice,
                Close = k.ClosePrice,
                Volume = k.Volume,
                CloseTime = k.CloseTime
            }).ToList();

            _logger.LogInformation(
                "Fetched {Count} candles for {Symbol} ({Interval})",
                candles.Count, symbol, interval);

            return candles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching historical data for {Symbol}", symbol);
            return [];
        }
    }

    private KlineInterval ParseInterval(string interval)
    {
        return interval.ToLower() switch
        {
            "1m" => KlineInterval.OneMinute,
            "5m" => KlineInterval.FiveMinutes,
            "15m" => KlineInterval.FifteenMinutes,
            "30m" => KlineInterval.ThirtyMinutes,
            "1h" => KlineInterval.OneHour,
            "4h" => KlineInterval.FourHour,
            "1d" => KlineInterval.OneDay,
            "1w" => KlineInterval.OneWeek,
            _ => KlineInterval.OneHour
        };
    }
}

