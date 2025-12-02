using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Strategies;

public class EmaCrossoverStrategy
{
    private readonly int _fastPeriod;
    private readonly int _slowPeriod;
    private readonly decimal _volumeMultiplier;

    public EmaCrossoverStrategy(int fastPeriod = 9, int slowPeriod = 21, decimal volumeMultiplier = 1.5m)
    {
        _fastPeriod = fastPeriod;
        _slowPeriod = slowPeriod;
        _volumeMultiplier = volumeMultiplier;
    }

    public List<TradeSignal> GenerateSignals(List<Candle> candles)
    {
        if (candles.Count < _slowPeriod)
            throw new ArgumentException($"Need at least {_slowPeriod} candles for analysis");

        // Sort candles by time
        var sortedCandles = candles.OrderBy(c => c.OpenTime).ToList();

        // Calculate EMAs
        var fastEma = CalculateEMA(sortedCandles, _fastPeriod);
        var slowEma = CalculateEMA(sortedCandles, _slowPeriod);

        // Calculate average volume
        var avgVolume = sortedCandles.Average(c => c.Volume);

        var signals = new List<TradeSignal>();

        // Generate signals starting from slowPeriod (need enough data for both EMAs)
        for (int i = _slowPeriod; i < sortedCandles.Count; i++)
        {
            var currentCandle = sortedCandles[i];
            var previousCandle = sortedCandles[i - 1];

            var currentFastEma = fastEma[i];
            var currentSlowEma = slowEma[i];
            var previousFastEma = fastEma[i - 1];
            var previousSlowEma = slowEma[i - 1];

            // Check for crossover
            bool bullishCross = previousFastEma <= previousSlowEma && currentFastEma > currentSlowEma;
            bool bearishCross = previousFastEma >= previousSlowEma && currentFastEma < currentSlowEma;

            // Volume confirmation
            bool volumeConfirmed = currentCandle.Volume > (avgVolume * _volumeMultiplier);

            if (bullishCross && volumeConfirmed)
            {
                signals.Add(new TradeSignal
                {
                    Symbol = currentCandle.Symbol,
                    SignalType = SignalType.Buy,
                    Time = currentCandle.OpenTime,
                    Price = currentCandle.ClosePrice,
                    FastEma = currentFastEma,
                    SlowEma = currentSlowEma,
                    Volume = currentCandle.Volume,
                    Reason = $"Bullish crossover: Fast EMA ({currentFastEma:F2}) crossed above Slow EMA ({currentSlowEma:F2}) with volume confirmation"
                });
            }
            else if (bearishCross && volumeConfirmed)
            {
                signals.Add(new TradeSignal
                {
                    Symbol = currentCandle.Symbol,
                    SignalType = SignalType.Sell,
                    Time = currentCandle.OpenTime,
                    Price = currentCandle.ClosePrice,
                    FastEma = currentFastEma,
                    SlowEma = currentSlowEma,
                    Volume = currentCandle.Volume,
                    Reason = $"Bearish crossover: Fast EMA ({currentFastEma:F2}) crossed below Slow EMA ({currentSlowEma:F2}) with volume confirmation"
                });
            }
        }

        return signals;
    }

    private List<decimal> CalculateEMA(List<Candle> candles, int period)
    {
        var ema = new List<decimal>();
        decimal multiplier = 2m / (period + 1);

        // First EMA is SMA
        decimal sum = 0;
        for (int i = 0; i < period; i++)
        {
            sum += candles[i].ClosePrice;
            ema.Add(0); // Placeholder for positions before we have enough data
        }
        ema[period - 1] = sum / period;

        // Calculate EMA for remaining candles
        for (int i = period; i < candles.Count; i++)
        {
            decimal currentEma = (candles[i].ClosePrice * multiplier) + (ema[i - 1] * (1 - multiplier));
            ema.Add(currentEma);
        }

        return ema;
    }

    public BacktestResult Backtest(List<Candle> candles, decimal initialCapital = 10000m, decimal feePercentage = 0.1m)
    {
        var signals = GenerateSignals(candles);
        var result = new BacktestResult
        {
            InitialCapital = initialCapital,
            CurrentCapital = initialCapital,
            Trades = new List<Trade>()
        };

        Trade? openTrade = null;

        foreach (var signal in signals)
        {
            if (signal.SignalType == SignalType.Buy && openTrade == null)
            {
                // Open long position
                decimal fee = (result.CurrentCapital * feePercentage) / 100;
                decimal capitalAfterFee = result.CurrentCapital - fee;
                decimal quantity = capitalAfterFee / signal.Price;

                openTrade = new Trade
                {
                    EntryTime = signal.Time,
                    EntryPrice = signal.Price,
                    Quantity = quantity,
                    EntryFee = fee
                };
            }
            else if (signal.SignalType == SignalType.Sell && openTrade != null)
            {
                // Close long position
                decimal saleValue = openTrade.Quantity * signal.Price;
                decimal exitFee = (saleValue * feePercentage) / 100;
                decimal finalValue = saleValue - exitFee;

                openTrade.ExitTime = signal.Time;
                openTrade.ExitPrice = signal.Price;
                openTrade.ExitFee = exitFee;
                openTrade.Profit = finalValue - (result.CurrentCapital - openTrade.EntryFee);
                openTrade.ProfitPercentage = (openTrade.Profit / result.CurrentCapital) * 100;

                result.CurrentCapital = finalValue;
                result.Trades.Add(openTrade);
                openTrade = null;
            }
        }

        // Calculate statistics
        result.TotalTrades = result.Trades.Count;
        result.WinningTrades = result.Trades.Count(t => t.Profit > 0);
        result.LosingTrades = result.Trades.Count(t => t.Profit <= 0);
        result.WinRate = result.TotalTrades > 0 ? (decimal)result.WinningTrades / result.TotalTrades * 100 : 0;
        result.TotalProfit = result.CurrentCapital - result.InitialCapital;
        result.TotalReturn = ((result.CurrentCapital - result.InitialCapital) / result.InitialCapital) * 100;

        if (result.Trades.Any())
        {
            result.AverageProfit = result.Trades.Average(t => t.Profit);
            result.LargestWin = result.Trades.Max(t => t.Profit);
            result.LargestLoss = result.Trades.Min(t => t.Profit);
        }

        return result;
    }
}

// Example Usage:
/*
// Get your candles from database
var candles = await dbContext.Candles
    .Where(c => c.Symbol == "BTCUSDT" && c.Interval == CandleInterval.OneHour)
    .OrderBy(c => c.OpenTime)
    .ToListAsync();

// Create strategy with default parameters (9 EMA, 21 EMA, 1.5x volume)
var strategy = new EmaCrossoverStrategy();

// Generate signals
var signals = strategy.GenerateSignals(candles);

// Or run a backtest
var backtestResult = strategy.Backtest(candles, initialCapital: 10000m, feePercentage: 0.1m);

Console.WriteLine($"Total Return: {backtestResult.TotalReturn:F2}%");
Console.WriteLine($"Win Rate: {backtestResult.WinRate:F2}%");
Console.WriteLine($"Total Trades: {backtestResult.TotalTrades}");
Console.WriteLine($"Total Profit: ${backtestResult.TotalProfit:F2}");
*/