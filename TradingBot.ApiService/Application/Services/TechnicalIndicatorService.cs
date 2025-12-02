using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Services;

public class TechnicalIndicatorService : ITechnicalIndicatorService
{
    public decimal CalculateSMA(List<Candle> candles, int period)
    {
        if (candles.Count < period)
            throw new ArgumentException($"Not enough candles. Need {period}, got {candles.Count}");

        var closePrices = candles.TakeLast(period).Select(c => c.ClosePrice).ToList();
        return closePrices.Average();
    }

    public decimal CalculateEMA(List<Candle> candles, int period)
    {
        if (candles.Count < period)
            throw new ArgumentException($"Not enough candles. Need {period}, got {candles.Count}");

        var multiplier = 2m / (period + 1);
        var closePrices = candles.Select(c => c.ClosePrice).ToList();

        // Start with SMA for first EMA value
        var ema = closePrices.Take(period).Average();

        // Calculate EMA for remaining values
        for (int i = period; i < closePrices.Count; i++)
        {
            ema = (closePrices[i] - ema) * multiplier + ema;
        }

        return ema;
    }

    public decimal CalculateRSI(List<Candle> candles, int period = 14)
    {
        if (candles.Count < period + 1)
            throw new ArgumentException($"Not enough candles. Need {period + 1}, got {candles.Count}");

        var prices = candles.Select(c => c.ClosePrice).ToList();
        var gains = new List<decimal>();
        var losses = new List<decimal>();

        for (int i = 1; i < prices.Count; i++)
        {
            var change = prices[i] - prices[i - 1];
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

    public (decimal macd, decimal signal, decimal histogram) CalculateMACD(
        List<Candle> candles,
        int fastPeriod = 12,
        int slowPeriod = 26,
        int signalPeriod = 9)
    {
        if (candles.Count < slowPeriod + signalPeriod)
            throw new ArgumentException($"Not enough candles for MACD calculation");

        var fastEMA = CalculateEMA(candles, fastPeriod);
        var slowEMA = CalculateEMA(candles, slowPeriod);
        var macd = fastEMA - slowEMA;

        // Calculate signal line (EMA of MACD)
        // For simplicity, we'll use a simple approximation
        // In production, you'd calculate EMA of MACD values over time
        var macdLine = new List<Candle>();
        for (int i = slowPeriod; i <= candles.Count; i++)
        {
            var subset = candles.Take(i).ToList();
            if (subset.Count >= slowPeriod)
            {
                var f = CalculateEMA(subset, fastPeriod);
                var s = CalculateEMA(subset, slowPeriod);
                macdLine.Add(new Candle
                {
                    Symbol = "TEMP",
                    Interval = "temp",
                    OpenTime = DateTime.UtcNow,
                    ClosePrice = f - s
                });
            }
        }

        var signal = macdLine.Count >= signalPeriod ? CalculateEMA(macdLine, signalPeriod) : macd;
        var histogram = macd - signal;

        return (macd, signal, histogram);
    }

    public (decimal upper, decimal middle, decimal lower) CalculateBollingerBands(
        List<Candle> candles,
        int period = 20,
        decimal stdDevMultiplier = 2)
    {
        if (candles.Count < period)
            throw new ArgumentException($"Not enough candles. Need {period}, got {candles.Count}");

        var closePrices = candles.TakeLast(period).Select(c => c.ClosePrice).ToList();
        var sma = closePrices.Average();

        // Calculate standard deviation
        var sumOfSquares = closePrices.Sum(price => (double)Math.Pow((double)(price - sma), 2));
        var stdDev = (decimal)Math.Sqrt(sumOfSquares / period);

        var upper = sma + (stdDevMultiplier * stdDev);
        var lower = sma - (stdDevMultiplier * stdDev);

        return (upper, sma, lower);
    }

    public decimal CalculateATR(List<Candle> candles, int period = 14)
    {
        if (candles.Count < period + 1)
            throw new ArgumentException($"Not enough candles. Need {period + 1}, got {candles.Count}");

        var trueRanges = new List<decimal>();

        for (int i = 1; i < candles.Count; i++)
        {
            var high = candles[i].HighPrice;
            var low = candles[i].LowPrice;
            var prevClose = candles[i - 1].ClosePrice;

            var tr = Math.Max(
                high - low,
                Math.Max(
                    Math.Abs(high - prevClose),
                    Math.Abs(low - prevClose)
                )
            );

            trueRanges.Add(tr);
        }

        return trueRanges.TakeLast(period).Average();
    }

    public decimal CalculateSupertrend(List<Candle> candles, out bool isUptrend, int period = 10, decimal multiplier = 3)
    {
        if (candles.Count < period + 1)
            throw new ArgumentException($"Not enough candles for Supertrend");

        var atr = CalculateATR(candles, period);
        var lastCandle = candles.Last();
        var hl2 = (lastCandle.HighPrice + lastCandle.LowPrice) / 2;

        var basicUpperBand = hl2 + (multiplier * atr);
        var basicLowerBand = hl2 - (multiplier * atr);

        // Simplified Supertrend calculation
        // In production, you'd track the bands across multiple candles
        var close = lastCandle.ClosePrice;
        isUptrend = close > basicLowerBand;

        return isUptrend ? basicLowerBand : basicUpperBand;
    }

    public decimal CalculateAverageVolume(List<Candle> candles, int period = 20)
    {
        if (candles.Count < period)
            throw new ArgumentException($"Not enough candles. Need {period}, got {candles.Count}");

        return candles.TakeLast(period).Average(c => c.Volume);
    }

    public decimal GetSwingHigh(List<Candle> candles, int lookback = 10)
    {
        if (candles.Count < lookback)
            throw new ArgumentException($"Not enough candles. Need {lookback}, got {candles.Count}");

        return candles.TakeLast(lookback).Max(c => c.HighPrice);
    }

    public decimal GetSwingLow(List<Candle> candles, int lookback = 10)
    {
        if (candles.Count < lookback)
            throw new ArgumentException($"Not enough candles. Need {lookback}, got {candles.Count}");

        return candles.TakeLast(lookback).Min(c => c.LowPrice);
    }

    public decimal CalculateVWAP(List<Candle> candles)
    {
        if (candles.Count == 0)
            throw new ArgumentException("No candles provided for VWAP calculation");

        decimal cumulativeTPV = 0; // Typical Price × Volume
        decimal cumulativeVolume = 0;

        foreach (var candle in candles)
        {
            var typicalPrice = (candle.HighPrice + candle.LowPrice + candle.ClosePrice) / 3;
            cumulativeTPV += typicalPrice * candle.Volume;
            cumulativeVolume += candle.Volume;
        }

        if (cumulativeVolume == 0)
            return candles.Last().ClosePrice; // Fallback to close price

        return cumulativeTPV / cumulativeVolume;
    }

    public List<decimal> FindSupportLevels(List<Candle> candles, int lookback = 50)
    {
        if (candles.Count < lookback)
            return new List<decimal>();

        var recentCandles = candles.TakeLast(lookback).ToList();
        var supportLevels = new List<decimal>();

        // Find swing lows (potential support)
        for (int i = 2; i < recentCandles.Count - 2; i++)
        {
            var current = recentCandles[i].LowPrice;
            var isSwingLow = current < recentCandles[i - 1].LowPrice &&
                            current < recentCandles[i - 2].LowPrice &&
                            current < recentCandles[i + 1].LowPrice &&
                            current < recentCandles[i + 2].LowPrice;

            if (isSwingLow)
            {
                supportLevels.Add(current);
            }
        }

        // Cluster nearby levels (within 0.5% of each other)
        var clusteredSupports = new List<decimal>();
        var sortedSupports = supportLevels.OrderBy(s => s).ToList();

        for (int i = 0; i < sortedSupports.Count; i++)
        {
            if (clusteredSupports.Any(cs => Math.Abs(cs - sortedSupports[i]) / cs < 0.005m))
                continue;

            clusteredSupports.Add(sortedSupports[i]);
        }

        return clusteredSupports.OrderByDescending(s => s).Take(3).ToList();
    }

    public List<decimal> FindResistanceLevels(List<Candle> candles, int lookback = 50)
    {
        if (candles.Count < lookback)
            return new List<decimal>();

        var recentCandles = candles.TakeLast(lookback).ToList();
        var resistanceLevels = new List<decimal>();

        // Find swing highs (potential resistance)
        for (int i = 2; i < recentCandles.Count - 2; i++)
        {
            var current = recentCandles[i].HighPrice;
            var isSwingHigh = current > recentCandles[i - 1].HighPrice &&
                             current > recentCandles[i - 2].HighPrice &&
                             current > recentCandles[i + 1].HighPrice &&
                             current > recentCandles[i + 2].HighPrice;

            if (isSwingHigh)
            {
                resistanceLevels.Add(current);
            }
        }

        // Cluster nearby levels (within 0.5% of each other)
        var clusteredResistances = new List<decimal>();
        var sortedResistances = resistanceLevels.OrderBy(r => r).ToList();

        for (int i = 0; i < sortedResistances.Count; i++)
        {
            if (clusteredResistances.Any(cr => Math.Abs(cr - sortedResistances[i]) / cr < 0.005m))
                continue;

            clusteredResistances.Add(sortedResistances[i]);
        }

        return clusteredResistances.OrderBy(r => r).Take(3).ToList();
    }

    public bool DetectBullishDivergence(List<Candle> candles, List<decimal> rsiValues, int lookback = 14)
    {
        if (candles.Count < lookback || rsiValues.Count < lookback)
            return false;

        var recentCandles = candles.TakeLast(lookback).ToList();
        var recentRsi = rsiValues.TakeLast(lookback).ToList();

        // Find two swing lows in price
        var priceLows = new List<(int index, decimal price)>();
        for (int i = 2; i < recentCandles.Count - 2; i++)
        {
            var current = recentCandles[i].LowPrice;
            if (current < recentCandles[i - 1].LowPrice &&
                current < recentCandles[i - 2].LowPrice &&
                current < recentCandles[i + 1].LowPrice &&
                current < recentCandles[i + 2].LowPrice)
            {
                priceLows.Add((i, current));
            }
        }

        if (priceLows.Count < 2)
            return false;

        // Check last two lows
        var firstLow = priceLows[^2];
        var secondLow = priceLows[^1];

        // Bullish divergence: Price makes lower low, but RSI makes higher low
        var priceIsLowerLow = secondLow.price < firstLow.price;
        var rsiIsHigherLow = recentRsi[secondLow.index] > recentRsi[firstLow.index];

        return priceIsLowerLow && rsiIsHigherLow;
    }

    public bool DetectBearishDivergence(List<Candle> candles, List<decimal> rsiValues, int lookback = 14)
    {
        if (candles.Count < lookback || rsiValues.Count < lookback)
            return false;

        var recentCandles = candles.TakeLast(lookback).ToList();
        var recentRsi = rsiValues.TakeLast(lookback).ToList();

        // Find two swing highs in price
        var priceHighs = new List<(int index, decimal price)>();
        for (int i = 2; i < recentCandles.Count - 2; i++)
        {
            var current = recentCandles[i].HighPrice;
            if (current > recentCandles[i - 1].HighPrice &&
                current > recentCandles[i - 2].HighPrice &&
                current > recentCandles[i + 1].HighPrice &&
                current > recentCandles[i + 2].HighPrice)
            {
                priceHighs.Add((i, current));
            }
        }

        if (priceHighs.Count < 2)
            return false;

        // Check last two highs
        var firstHigh = priceHighs[^2];
        var secondHigh = priceHighs[^1];

        // Bearish divergence: Price makes higher high, but RSI makes lower high
        var priceIsHigherHigh = secondHigh.price > firstHigh.price;
        var rsiIsLowerHigh = recentRsi[secondHigh.index] < recentRsi[firstHigh.index];

        return priceIsHigherHigh && rsiIsLowerHigh;
    }
}
