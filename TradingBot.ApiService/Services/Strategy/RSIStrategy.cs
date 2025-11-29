using TradingBot.ApiService.Models;

namespace TradingBot.ApiService.Services.Strategy;

/// <summary>
/// RSI (Relative Strength Index) Strategy
/// Buy when RSI < 30 (oversold)
/// Sell when RSI > 70 (overbought)
/// </summary>
public class RSIStrategy : BaseStrategy
{
    public override string Name => "RSI";
    public override string Description => "Buy when oversold (RSI < 30), sell when overbought (RSI > 70)";

    private readonly int _period;
    private readonly decimal _oversoldLevel;
    private readonly decimal _overboughtLevel;

    public RSIStrategy(
        ILogger<RSIStrategy> logger,
        int period = 14,
        decimal oversoldLevel = 30,
        decimal overboughtLevel = 70) : base(logger)
    {
        _period = period;
        _oversoldLevel = oversoldLevel;
        _overboughtLevel = overboughtLevel;
    }

    public override async Task<TradingSignal> AnalyzeAsync(
        string symbol,
        List<Candle> candles,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        if (candles.Count < _period + 1)
        {
            return CreateHoldSignal(symbol, candles.Last().Close, "Insufficient data");
        }

        var rsi = CalculateRSI(candles, _period);
        var currentPrice = candles.Last().Close;

        _logger.LogInformation("RSI for {Symbol}: {RSI:F2}", symbol, rsi);

        // Strong oversold - Strong Buy signal
        if (rsi < 20)
        {
            return new TradingSignal
            {
                Symbol = symbol,
                Type = SignalType.StrongBuy,
                Price = currentPrice,
                Confidence = 0.90m,
                Strategy = Name,
                Reason = $"Extremely oversold: RSI = {rsi:F2}",
                Indicators = new Dictionary<string, object> { ["RSI"] = rsi, ["Period"] = _period }
            };
        }

        // Oversold - Buy signal
        if (rsi < _oversoldLevel)
        {
            return new TradingSignal
            {
                Symbol = symbol,
                Type = SignalType.Buy,
                Price = currentPrice,
                Confidence = 0.75m,
                Strategy = Name,
                Reason = $"Oversold: RSI = {rsi:F2}",
                Indicators = new Dictionary<string, object> { ["RSI"] = rsi, ["Period"] = _period }
            };
        }

        // Strong overbought - Strong Sell signal
        if (rsi > 80)
        {
            return new TradingSignal
            {
                Symbol = symbol,
                Type = SignalType.StrongSell,
                Price = currentPrice,
                Confidence = 0.90m,
                Strategy = Name,
                Reason = $"Extremely overbought: RSI = {rsi:F2}",
                Indicators = new Dictionary<string, object> { ["RSI"] = rsi, ["Period"] = _period }
            };
        }

        // Overbought - Sell signal
        if (rsi > _overboughtLevel)
        {
            return new TradingSignal
            {
                Symbol = symbol,
                Type = SignalType.Sell,
                Price = currentPrice,
                Confidence = 0.75m,
                Strategy = Name,
                Reason = $"Overbought: RSI = {rsi:F2}",
                Indicators = new Dictionary<string, object> { ["RSI"] = rsi, ["Period"] = _period }
            };
        }

        // Neutral - Hold
        return CreateHoldSignal(
            symbol,
            currentPrice,
            $"Neutral: RSI = {rsi:F2}",
            new Dictionary<string, object> { ["RSI"] = rsi, ["Period"] = _period });
    }

    private TradingSignal CreateHoldSignal(
        string symbol,
        decimal price,
        string reason,
        Dictionary<string, object>? indicators = null)
    {
        return new TradingSignal
        {
            Symbol = symbol,
            Type = SignalType.Hold,
            Price = price,
            Confidence = 0.5m,
            Strategy = Name,
            Reason = reason,
            Indicators = indicators ?? new()
        };
    }
}


