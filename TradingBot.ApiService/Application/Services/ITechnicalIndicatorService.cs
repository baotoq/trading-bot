using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Services;

public interface ITechnicalIndicatorService
{
    decimal CalculateSMA(List<Candle> candles, int period);
    decimal CalculateEMA(List<Candle> candles, int period);
    decimal CalculateRSI(List<Candle> candles, int period = 14);
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
    List<decimal> FindSupportLevels(List<Candle> candles, int lookback = 50);
    List<decimal> FindResistanceLevels(List<Candle> candles, int lookback = 50);
    bool DetectBullishDivergence(List<Candle> candles, List<decimal> rsiValues, int lookback = 14);
    bool DetectBearishDivergence(List<Candle> candles, List<decimal> rsiValues, int lookback = 14);
}
