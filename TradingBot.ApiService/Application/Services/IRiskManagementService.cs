using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Services;

public interface IRiskManagementService
{
    Task<RiskCheckResult> ValidateTradeAsync(
        TradingSignal signal,
        PositionParameters parameters,
        CancellationToken cancellationToken = default);

    Task<bool> CanOpenNewPositionAsync(Symbol symbol, CancellationToken cancellationToken = default);
}

public class RiskCheckResult
{
    public bool IsApproved { get; set; }
    public List<string> Warnings { get; set; } = new();
    public List<string> Violations { get; set; } = new();
    public string? RejectionReason { get; set; }
}
