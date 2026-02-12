namespace TradingBot.ApiService.Application.Services;

/// <summary>
/// Core service for executing daily DCA purchases on Hyperliquid.
/// Orchestrates the full buy flow: lock acquisition, idempotency check, balance verification,
/// order placement, persistence, and event publishing.
/// </summary>
public interface IDcaExecutionService
{
    /// <summary>
    /// Executes a single daily purchase for the specified date.
    /// Uses distributed lock and idempotency check to ensure only one purchase per day.
    /// Outcomes communicated via domain events (PurchaseCompleted, PurchaseFailed, PurchaseSkipped).
    /// </summary>
    /// <param name="purchaseDate">The date this purchase is for (used for idempotency check)</param>
    /// <param name="ct">Cancellation token</param>
    Task ExecuteDailyPurchaseAsync(DateOnly purchaseDate, CancellationToken ct = default);
}
