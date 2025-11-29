using Binance.Net.Enums;
using Binance.Net.Interfaces.Clients;
using CryptoExchange.Net.Objects.Sockets;
using System.Collections.Concurrent;
using System.Collections.Generic;
using TradingBot.ApiService.Models;
using TradingBot.ApiService.Services.Strategy;

namespace TradingBot.ApiService.Services.RealTimeTrading;

/// <summary>
/// Real-time trading service that connects to Binance WebSocket and executes strategies
/// </summary>
public class RealTimeTradingService : IRealTimeTradingService
{
    private readonly IBinanceSocketClient _socketClient;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RealTimeTradingService> _logger;

    private readonly Dictionary<string, MonitoringSession> _activeSessions = new();
    private readonly Dictionary<string, UpdateSubscription> _subscriptions = new();
    private readonly Dictionary<string, List<Candle>> _candleBuffers = new();
    private readonly Dictionary<string, TradingSignal> _latestSignals = new();
    private readonly Lock _lock = new();

    public RealTimeTradingService(
        IBinanceSocketClient socketClient,
        IServiceProvider serviceProvider,
        ILogger<RealTimeTradingService> logger)
    {
        _socketClient = socketClient;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task<bool> StartMonitoringAsync(string symbol, string interval, string strategyName,
        bool autoTrade = false)
    {
        try
        {
            using (_lock.EnterScope())
            {
                if (_activeSessions.ContainsKey(symbol))
                {
                    _logger.LogWarning("Already monitoring {Symbol}", symbol);
                    return false;
                }
            }

            // Parse interval
            var klineInterval = ParseInterval(interval);
            if (klineInterval == null)
            {
                _logger.LogError("Invalid interval: {Interval}", interval);
                return false;
            }

            // Create monitoring session
            var session = new MonitoringSession
            {
                Symbol = symbol,
                Interval = interval,
                StrategyName = strategyName,
                AutoTrade = autoTrade,
                StartTime = DateTime.UtcNow,
                SignalsGenerated = 0,
                TradesExecuted = 0
            };

            // Initialize candle buffer with historical data
            await InitializeCandleBufferAsync(symbol, interval);

            // Subscribe to kline updates
            var subscription = await _socketClient.SpotApi.ExchangeData.SubscribeToKlineUpdatesAsync(
                symbol,
                klineInterval.Value,
                data =>
                {
                    _ = OnKlineUpdateAsync(symbol, strategyName, autoTrade, data);
                });

            if (!subscription.Success)
            {
                _logger.LogError("Failed to subscribe to {Symbol}: {Error}", symbol, subscription.Error?.Message);
                return false;
            }

            using (_lock.EnterScope())
            {
                _activeSessions[symbol] = session;
                _subscriptions[symbol] = subscription.Data;
            }

            _logger.LogInformation("Started monitoring {Symbol} with {Strategy} (AutoTrade: {AutoTrade})",
                symbol, strategyName, autoTrade);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting monitoring for {Symbol}", symbol);
            return false;
        }
    }

    public async Task<bool> StopMonitoringAsync(string symbol)
    {
        try
        {
            UpdateSubscription? subscription = null;

            using (_lock.EnterScope())
            {
                if (!_activeSessions.ContainsKey(symbol))
                {
                    _logger.LogWarning("Not monitoring {Symbol}", symbol);
                    return false;
                }

                _activeSessions.Remove(symbol);
                _candleBuffers.Remove(symbol);
                _latestSignals.Remove(symbol);

                if (_subscriptions.ContainsKey(symbol))
                {
                    subscription = _subscriptions[symbol];
                    _subscriptions.Remove(symbol);
                }
            }

            if (subscription != null)
            {
                await subscription.CloseAsync();
            }

            _logger.LogInformation("Stopped monitoring {Symbol}", symbol);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping monitoring for {Symbol}", symbol);
            return false;
        }
    }

    public List<MonitoringSession> GetActiveMonitoringSessions()
    {
        using (_lock.EnterScope())
        {
            return [.. _activeSessions.Values];
        }
    }

    public Dictionary<string, TradingSignal> GetLatestSignals()
    {
        using (_lock.EnterScope())
        {
            return new Dictionary<string, TradingSignal>(_latestSignals);
        }
    }

    private async Task InitializeCandleBufferAsync(string symbol, string interval)
    {
        try
        {
            // Fetch historical candles to initialize the buffer (100 candles should be enough for all strategies)
            var klineInterval = ParseInterval(interval);
            if (klineInterval == null) return;

            using var scope = _serviceProvider.CreateScope();
            var historicalService = scope.ServiceProvider.GetRequiredService<IHistoricalDataService>();

            var endTime = DateTime.UtcNow;
            var startTime = endTime.AddDays(-7); // Get last 7 days of data

            var candles = await historicalService.GetHistoricalDataAsync(symbol, interval, startTime, endTime);

            using (_lock.EnterScope())
            {
                _candleBuffers[symbol] = candles.TakeLast(100).ToList();
            }

            _logger.LogInformation("Initialized candle buffer for {Symbol} with {Count} candles",
                symbol, _candleBuffers[symbol].Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing candle buffer for {Symbol}", symbol);
            using (_lock.EnterScope())
            {
                _candleBuffers[symbol] = new List<Candle>();
            }
        }
    }

    private async Task OnKlineUpdateAsync(string symbol, string strategyName, bool autoTrade,
        dynamic klineEvent)
    {
        try
        {
            var klineData = klineEvent.Data.Data;

            // Only process closed candles for strategy analysis
            if (!klineData.Final)
            {
                _logger.LogDebug("Ignoring incomplete candle for {Symbol}", symbol);
                return; // Wait for candle to close
            }

            var candle = new Candle
            {
                OpenTime = klineData.OpenTime,
                Open = klineData.OpenPrice,
                High = klineData.HighPrice,
                Low = klineData.LowPrice,
                Close = klineData.ClosePrice,
                Volume = klineData.Volume,
                CloseTime = klineData.CloseTime
            };

            // Update candle buffer
            using (_lock.EnterScope())
            {
                if (!_candleBuffers.ContainsKey(symbol))
                {
                    _candleBuffers[symbol] = new List<Candle>();
                }

                _candleBuffers[symbol].Add(candle);

                // Keep only last 100 candles
                if (_candleBuffers[symbol].Count > 100)
                {
                    _candleBuffers[symbol].RemoveAt(0);
                }
            }

            _logger.LogInformation("New closed candle for {Symbol}: Price={Price}, Volume={Volume}",
                symbol, candle.Close, candle.Volume);

            // Analyze with strategy
            await AnalyzeAndExecuteAsync(symbol, strategyName, autoTrade);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing kline update for {Symbol}", symbol);
        }
    }

    private async Task AnalyzeAndExecuteAsync(string symbol, string strategyName, bool autoTrade)
    {
        try
        {
            List<Candle> candles;
            using (_lock.EnterScope())
            {
                if (!_candleBuffers.ContainsKey(symbol) || _candleBuffers[symbol].Count == 0)
                {
                    return;
                }

                candles = new List<Candle>(_candleBuffers[symbol]);
            }

            // Get strategy from DI
            using var scope = _serviceProvider.CreateScope();
            var strategy = GetStrategy(scope.ServiceProvider, strategyName);

            // Analyze
            var signal = await strategy.AnalyzeAsync(symbol, candles);

            // Update session
            using (_lock.EnterScope())
            {
                _latestSignals[symbol] = signal;

                if (_activeSessions.ContainsKey(symbol))
                {
                    var session = _activeSessions[symbol];
                    session.LatestSignal = signal;
                    session.SignalsGenerated++;
                }
            }

            _logger.LogInformation(
                "Signal for {Symbol}: {Type} (Confidence: {Confidence:P0}) - {Reason}",
                signal.Symbol, signal.Type, signal.Confidence, signal.Reason);

            // Execute trade if auto-trade is enabled and signal is strong enough
            if (autoTrade && ShouldExecuteTrade(signal))
            {
                await ExecuteTradeAsync(symbol, signal);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing and executing for {Symbol}", symbol);
        }
    }

    private bool ShouldExecuteTrade(TradingSignal signal)
    {
        // Only execute on strong buy/sell signals with high confidence
        return (signal.Type == SignalType.StrongBuy || signal.Type == SignalType.StrongSell)
               && signal.Confidence >= 0.75m;
    }

    private async Task ExecuteTradeAsync(string symbol, TradingSignal signal)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var binanceService = scope.ServiceProvider.GetRequiredService<IBinanceService>();
            // For safety, we'll only execute BUY orders in auto-trade
            // You can modify this to include SELL orders as well
            if (signal.Type != SignalType.StrongBuy)
            {
                _logger.LogInformation("Skipping auto-trade for {Symbol}: Not a strong buy signal", symbol);
                return;
            }

            // Get account info to determine trade size
            var accountInfo = await binanceService.GetAccountInfoAsync();
            if (accountInfo == null || !accountInfo.CanTrade)
            {
                _logger.LogWarning("Cannot trade: Account info unavailable or trading disabled");
                return;
            }

            // Calculate trade quantity (example: use 1% of available USDT balance)
            var usdtBalance = accountInfo.Balances.FirstOrDefault(b => b.Asset == "USDT");
            if (usdtBalance == null || usdtBalance.Free < 10) // Minimum 10 USDT
            {
                _logger.LogWarning("Insufficient USDT balance for trading");
                return;
            }

            var tradeAmount = usdtBalance.Free * 0.01m; // Use 1% of balance
            var quantity = Math.Round(tradeAmount / signal.Price, 6); // Calculate quantity

            _logger.LogInformation(
                "Executing AUTO-TRADE: {Side} {Quantity} {Symbol} at ~{Price}",
                signal.Type == SignalType.StrongBuy ? "BUY" : "SELL",
                quantity, symbol, signal.Price);

            // Place market order
            var orderResult = await binanceService.PlaceSpotOrderAsync(
                symbol,
                Models.OrderSide.Buy,
                Models.OrderType.Market,
                quantity);

            if (orderResult != null)
            {
                lock (_lock)
                {
                    if (_activeSessions.ContainsKey(symbol))
                    {
                        _activeSessions[symbol].TradesExecuted++;
                    }
                }

                _logger.LogInformation(
                    "Trade executed successfully: OrderId={OrderId}, Status={Status}",
                    orderResult.OrderId, orderResult.Status);
            }
            else
            {
                _logger.LogError("Failed to execute trade for {Symbol}", symbol);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing trade for {Symbol}", symbol);
        }
    }

    private IStrategy GetStrategy(IServiceProvider serviceProvider, string strategyName)
    {
        return strategyName.ToLower() switch
        {
            "rsi" or "rsistrategy" => serviceProvider.GetRequiredService<RSIStrategy>(),
            "macd" or "macdstrategy" => serviceProvider.GetRequiredService<MACDStrategy>(),
            "ma" or "movingaverage" or "movingaveragecrossoverstrategy" =>
                serviceProvider.GetRequiredService<MovingAverageCrossoverStrategy>(),
            _ => serviceProvider.GetRequiredService<RSIStrategy>() // Default to combined
        };
    }

    private KlineInterval? ParseInterval(string interval)
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
            _ => null
        };
    }
}

