using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.Models.Ids;

namespace TradingBot.ApiService.Models;

public class Purchase : BaseEntity<PurchaseId>
{
    public DateTimeOffset ExecutedAt { get; set; }
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public decimal Cost { get; set; }
    public decimal Multiplier { get; set; }
    public PurchaseStatus Status { get; set; } = PurchaseStatus.Pending;
    public bool IsDryRun { get; set; }
    public string? OrderId { get; set; }
    public string? RawResponse { get; set; }
    public string? FailureReason { get; set; }

    // Multiplier metadata fields for audit trail
    public string? MultiplierTier { get; set; }
    public decimal DropPercentage { get; set; }
    public decimal High30Day { get; set; }
    public decimal Ma200Day { get; set; }
}

public enum PurchaseStatus
{
    Pending,
    Filled,
    PartiallyFilled,
    Failed,
    Cancelled
}
