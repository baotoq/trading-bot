using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Services;

public interface IPositionCalculatorService
{
    Task<PositionParameters> CalculatePositionParametersAsync(
        TradingSignal signal,
        decimal accountEquity,
        decimal riskPercent,
        CancellationToken cancellationToken = default);
}

public class PositionParameters
{
    public decimal EntryPrice { get; set; }
    public decimal StopLoss { get; set; }
    public decimal TakeProfit1 { get; set; }
    public decimal TakeProfit2 { get; set; }
    public decimal TakeProfit3 { get; set; }
    public decimal PositionSize { get; set; }
    public decimal Quantity { get; set; }
    public decimal RiskAmount { get; set; }
    public decimal StopLossDistance { get; set; }
    public decimal StopLossPercent { get; set; }
    public int RecommendedLeverage { get; set; }
    public decimal MarginRequired { get; set; }
    public bool IsValid { get; set; }
    public string? ValidationError { get; set; }
}
