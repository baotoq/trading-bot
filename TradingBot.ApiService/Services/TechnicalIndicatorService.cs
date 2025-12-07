using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Services;

public interface ITechnicalIndicatorService
{
    decimal CalculateSMA(List<Candle> candles, int period);
    decimal CalculateEMA(List<Candle> candles, int period);
    List<decimal> CalculateEMASeries(List<Candle> candles, int period);
    decimal CalculateRSI(List<Candle> candles, int period = 14);
    List<decimal> CalculateRSISeries(List<Candle> candles, int period = 14, int count = 5);
    (decimal macd, decimal signal, decimal histogram) CalculateMACD(
        List<Candle> candles,
        int fastPeriod = 12,
        int slowPeriod = 26,
        int signalPeriod = 9);
    (decimal upper, decimal middle, decimal lower) CalculateBollingerBands(
        List<Candle> candles,
        int period = 20,
        decimal stdDevMultiplier = 2);
    decimal CalculateATR(List<Candle> candles, int period = 14);
    decimal CalculateSupertrend(List<Candle> candles, out bool isUptrend, int period = 10, decimal multiplier = 3);
    decimal CalculateAverageVolume(List<Candle> candles, int period = 20);
    decimal GetSwingHigh(List<Candle> candles, int lookback = 10);
    decimal GetSwingLow(List<Candle> candles, int lookback = 10);
    decimal CalculateVWAP(List<Candle> candles);
    List<decimal> CalculateVWAPSeries(List<Candle> candles, int count = 5);
    decimal CalculateVWAPSlope(List<Candle> candles, int lookback = 5);
    List<VolumeProfileNode> CalculateVolumeProfile(List<Candle> candles, int bins = 20);
    List<decimal> FindHighVolumeNodes(List<Candle> candles, int bins = 20, int topN = 3);
    List<decimal> FindSupportLevels(List<Candle> candles, int lookback = 50);
    List<decimal> FindResistanceLevels(List<Candle> candles, int lookback = 50);
    bool DetectBullishDivergence(List<Candle> candles, List<decimal> rsiValues, int lookback = 14);
    bool DetectBearishDivergence(List<Candle> candles, List<decimal> rsiValues, int lookback = 14);
    CandlestickPattern DetectCandlestickPattern(List<Candle> candles);
}

public class VolumeProfileNode
{
    public decimal PriceLevel { get; set; }
    public decimal Volume { get; set; }
    public decimal PercentOfTotal { get; set; }
}

public enum CandlestickPattern
{
    None,
    BullishEngulfing,
    BearishEngulfing,
    Hammer,
    InvertedHammer,
    ShootingStar,
    HangingMan,
    Doji,
    MorningStar,
    EveningStar
}

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

    public List<decimal> CalculateEMASeries(List<Candle> candles, int period)
    {
        if (candles.Count < period)
            throw new ArgumentException($"Not enough candles. Need {period}, got {candles.Count}");

        var result = new List<decimal>();
        var multiplier = 2m / (period + 1);
        var closePrices = candles.Select(c => c.ClosePrice).ToList();

        // Start with SMA for first EMA value
        var ema = closePrices.Take(period).Average();
        result.Add(ema);

        // Calculate EMA for remaining values
        for (int i = period; i < closePrices.Count; i++)
        {
            ema = (closePrices[i] - ema) * multiplier + ema;
            result.Add(ema);
        }

        return result;
    }

    public List<decimal> CalculateRSISeries(List<Candle> candles, int period = 14, int count = 5)
    {
        if (candles.Count < period + count)
            throw new ArgumentException($"Not enough candles. Need {period + count}, got {candles.Count}");

        var result = new List<decimal>();

        for (int i = 0; i < count; i++)
        {
            var subset = candles.Take(candles.Count - i).ToList();
            if (subset.Count >= period + 1)
            {
                result.Insert(0, CalculateRSI(subset, period));
            }
        }

        return result;
    }

    public List<decimal> CalculateVWAPSeries(List<Candle> candles, int count = 5)
    {
        if (candles.Count < count)
            throw new ArgumentException($"Not enough candles. Need {count}, got {candles.Count}");

        var result = new List<decimal>();

        for (int i = 0; i < count; i++)
        {
            var subset = candles.Take(candles.Count - i).ToList();
            if (subset.Count > 0)
            {
                result.Insert(0, CalculateVWAP(subset));
            }
        }

        return result;
    }

    public decimal CalculateVWAPSlope(List<Candle> candles, int lookback = 5)
    {
        if (candles.Count < lookback)
            return 0;

        var vwapSeries = CalculateVWAPSeries(candles, lookback);
        if (vwapSeries.Count < 2)
            return 0;

        // Calculate slope using linear regression
        var n = vwapSeries.Count;
        var sumX = 0m;
        var sumY = 0m;
        var sumXY = 0m;
        var sumX2 = 0m;

        for (int i = 0; i < n; i++)
        {
            sumX += i;
            sumY += vwapSeries[i];
            sumXY += i * vwapSeries[i];
            sumX2 += i * i;
        }

        var denominator = (n * sumX2 - sumX * sumX);
        if (denominator == 0)
            return 0;

        // Slope normalized by average price
        var slope = (n * sumXY - sumX * sumY) / denominator;
        var avgVwap = sumY / n;

        // Return slope as percentage change per period
        return avgVwap != 0 ? (slope / avgVwap) * 100 : 0;
    }

    public List<VolumeProfileNode> CalculateVolumeProfile(List<Candle> candles, int bins = 20)
    {
        if (candles.Count == 0)
            return new List<VolumeProfileNode>();

        var highPrice = candles.Max(c => c.HighPrice);
        var lowPrice = candles.Min(c => c.LowPrice);
        var priceRange = highPrice - lowPrice;

        if (priceRange == 0)
            return new List<VolumeProfileNode>
            {
                new VolumeProfileNode
                {
                    PriceLevel = highPrice,
                    Volume = candles.Sum(c => c.Volume),
                    PercentOfTotal = 100
                }
            };

        var binSize = priceRange / bins;
        var volumeProfile = new Dictionary<int, decimal>();

        // Initialize bins
        for (int i = 0; i < bins; i++)
        {
            volumeProfile[i] = 0;
        }

        // Distribute volume across bins
        foreach (var candle in candles)
        {
            var typicalPrice = (candle.HighPrice + candle.LowPrice + candle.ClosePrice) / 3;
            var binIndex = (int)Math.Min((typicalPrice - lowPrice) / binSize, bins - 1);
            volumeProfile[binIndex] += candle.Volume;
        }

        var totalVolume = volumeProfile.Values.Sum();
        if (totalVolume == 0)
            totalVolume = 1;

        return volumeProfile
            .Select(kvp => new VolumeProfileNode
            {
                PriceLevel = lowPrice + (kvp.Key + 0.5m) * binSize, // Center of bin
                Volume = kvp.Value,
                PercentOfTotal = (kvp.Value / totalVolume) * 100
            })
            .OrderByDescending(n => n.Volume)
            .ToList();
    }

    public List<decimal> FindHighVolumeNodes(List<Candle> candles, int bins = 20, int topN = 3)
    {
        var volumeProfile = CalculateVolumeProfile(candles, bins);
        return volumeProfile
            .Take(topN)
            .Select(n => n.PriceLevel)
            .ToList();
    }

    public CandlestickPattern DetectCandlestickPattern(List<Candle> candles)
    {
        if (candles.Count < 3)
            return CandlestickPattern.None;

        var current = candles[^1];
        var previous = candles[^2];
        var twoBefore = candles[^3];

        var body = current.ClosePrice - current.OpenPrice;
        var bodySize = Math.Abs(body);
        var upperWick = current.HighPrice - Math.Max(current.OpenPrice, current.ClosePrice);
        var lowerWick = Math.Min(current.OpenPrice, current.ClosePrice) - current.LowPrice;
        var totalRange = current.HighPrice - current.LowPrice;

        var prevBody = previous.ClosePrice - previous.OpenPrice;
        var prevBodySize = Math.Abs(prevBody);

        // Avoid division by zero
        if (totalRange == 0)
            return CandlestickPattern.None;

        var bodyRatio = bodySize / totalRange;
        var upperWickRatio = upperWick / totalRange;
        var lowerWickRatio = lowerWick / totalRange;

        // Doji: Very small body
        if (bodyRatio < 0.1m)
        {
            return CandlestickPattern.Doji;
        }

        // Bullish Engulfing: Current bullish candle engulfs previous bearish candle
        if (body > 0 && prevBody < 0 &&
            current.OpenPrice < previous.ClosePrice &&
            current.ClosePrice > previous.OpenPrice &&
            bodySize > prevBodySize)
        {
            return CandlestickPattern.BullishEngulfing;
        }

        // Bearish Engulfing: Current bearish candle engulfs previous bullish candle
        if (body < 0 && prevBody > 0 &&
            current.OpenPrice > previous.ClosePrice &&
            current.ClosePrice < previous.OpenPrice &&
            bodySize > prevBodySize)
        {
            return CandlestickPattern.BearishEngulfing;
        }

        // Hammer: Small body at top, long lower wick (bullish reversal)
        if (body > 0 && lowerWickRatio > 0.6m && upperWickRatio < 0.1m && bodyRatio < 0.3m)
        {
            return CandlestickPattern.Hammer;
        }

        // Inverted Hammer: Small body at bottom, long upper wick (bullish reversal)
        if (body > 0 && upperWickRatio > 0.6m && lowerWickRatio < 0.1m && bodyRatio < 0.3m)
        {
            return CandlestickPattern.InvertedHammer;
        }

        // Shooting Star: Small body at bottom, long upper wick (bearish reversal)
        if (body < 0 && upperWickRatio > 0.6m && lowerWickRatio < 0.1m && bodyRatio < 0.3m)
        {
            return CandlestickPattern.ShootingStar;
        }

        // Hanging Man: Small body at top, long lower wick (bearish reversal)
        if (body < 0 && lowerWickRatio > 0.6m && upperWickRatio < 0.1m && bodyRatio < 0.3m)
        {
            return CandlestickPattern.HangingMan;
        }

        // Morning Star (3-candle bullish reversal)
        var twoPrevBody = twoBefore.ClosePrice - twoBefore.OpenPrice;
        if (twoPrevBody < 0 && // First candle bearish
            Math.Abs(prevBody) < Math.Abs(twoPrevBody) * 0.3m && // Second candle small
            body > 0 && // Third candle bullish
            current.ClosePrice > (twoBefore.OpenPrice + twoBefore.ClosePrice) / 2)
        {
            return CandlestickPattern.MorningStar;
        }

        // Evening Star (3-candle bearish reversal)
        if (twoPrevBody > 0 && // First candle bullish
            Math.Abs(prevBody) < twoPrevBody * 0.3m && // Second candle small
            body < 0 && // Third candle bearish
            current.ClosePrice < (twoBefore.OpenPrice + twoBefore.ClosePrice) / 2)
        {
            return CandlestickPattern.EveningStar;
        }

        return CandlestickPattern.None;
    }
}
