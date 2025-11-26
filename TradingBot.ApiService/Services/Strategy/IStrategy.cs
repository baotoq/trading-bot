using TradingBot.ApiService.Models;

namespace TradingBot.ApiService.Services.Strategy;

/// <summary>
/// Base interface for all trading strategies
/// </summary>
public interface IStrategy
{
    /// <summary>
    /// Strategy name
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Strategy description
    /// </summary>
    string Description { get; }
    
    /// <summary>
    /// Analyze market data and generate trading signal
    /// </summary>
    Task<TradingSignal> AnalyzeAsync(string symbol, List<Candle> candles, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Validate strategy parameters
    /// </summary>
    bool ValidateParameters(Dictionary<string, object> parameters);
}

/// <summary>
/// Base class for trading strategies
/// </summary>
public abstract class BaseStrategy : IStrategy
{
    public abstract string Name { get; }
    public abstract string Description { get; }
    
    protected readonly ILogger _logger;

    protected BaseStrategy(ILogger logger)
    {
        _logger = logger;
    }

    public abstract Task<TradingSignal> AnalyzeAsync(string symbol, List<Candle> candles, CancellationToken cancellationToken = default);
    
    public virtual bool ValidateParameters(Dictionary<string, object> parameters)
    {
        return true;
    }

    /// <summary>
    /// Calculate Simple Moving Average
    /// </summary>
    protected decimal CalculateSMA(List<Candle> candles, int period)
    {
        if (candles.Count < period)
            return 0;

        return candles.TakeLast(period).Average(c => c.Close);
    }

    /// <summary>
    /// Calculate Exponential Moving Average
    /// </summary>
    protected decimal CalculateEMA(List<Candle> candles, int period)
    {
        if (candles.Count < period)
            return 0;

        var multiplier = 2m / (period + 1);
        var ema = candles.Take(period).Average(c => c.Close);

        foreach (var candle in candles.Skip(period))
        {
            ema = (candle.Close - ema) * multiplier + ema;
        }

        return ema;
    }

    /// <summary>
    /// Calculate Relative Strength Index (RSI)
    /// </summary>
    protected decimal CalculateRSI(List<Candle> candles, int period = 14)
    {
        if (candles.Count < period + 1)
            return 50;

        var gains = new List<decimal>();
        var losses = new List<decimal>();

        for (int i = 1; i < candles.Count; i++)
        {
            var change = candles[i].Close - candles[i - 1].Close;
            gains.Add(change > 0 ? change : 0);
            losses.Add(change < 0 ? -change : 0);
        }

        var avgGain = gains.TakeLast(period).Average();
        var avgLoss = losses.TakeLast(period).Average();

        if (avgLoss == 0)
            return 100;

        var rs = avgGain / avgLoss;
        var rsi = 100 - (100 / (1 + rs));

        return rsi;
    }

    /// <summary>
    /// Calculate MACD (Moving Average Convergence Divergence)
    /// </summary>
    protected (decimal macd, decimal signal, decimal histogram) CalculateMACD(
        List<Candle> candles, 
        int fastPeriod = 12, 
        int slowPeriod = 26, 
        int signalPeriod = 9)
    {
        if (candles.Count < slowPeriod)
            return (0, 0, 0);

        var fastEMA = CalculateEMA(candles, fastPeriod);
        var slowEMA = CalculateEMA(candles, slowPeriod);
        var macd = fastEMA - slowEMA;

        // Calculate signal line (EMA of MACD)
        var macdValues = new List<decimal> { macd };
        var signal = macdValues.Average(); // Simplified

        var histogram = macd - signal;

        return (macd, signal, histogram);
    }

    /// <summary>
    /// Calculate Bollinger Bands
    /// </summary>
    protected (decimal upper, decimal middle, decimal lower) CalculateBollingerBands(
        List<Candle> candles, 
        int period = 20, 
        decimal stdDevMultiplier = 2)
    {
        if (candles.Count < period)
            return (0, 0, 0);

        var closes = candles.TakeLast(period).Select(c => c.Close).ToList();
        var sma = closes.Average();
        
        var sumOfSquares = closes.Sum(c => (c - sma) * (c - sma));
        var stdDev = (decimal)Math.Sqrt((double)(sumOfSquares / period));

        var upper = sma + (stdDevMultiplier * stdDev);
        var lower = sma - (stdDevMultiplier * stdDev);

        return (upper, sma, lower);
    }
}

