using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.Models.Ids;
using TradingBot.ApiService.Models.Values;

namespace TradingBot.ApiService.Models;

public class Purchase : BaseEntity<PurchaseId>
{
    public DateTimeOffset ExecutedAt { get; set; }
    public Price Price { get; set; }
    public Quantity Quantity { get; set; }
    public UsdAmount Cost { get; set; }
    public Multiplier Multiplier { get; set; }
    public PurchaseStatus Status { get; set; } = PurchaseStatus.Pending;
    public bool IsDryRun { get; set; }
    public string? OrderId { get; set; }
    public string? RawResponse { get; set; }
    public string? FailureReason { get; set; }

    // Multiplier metadata fields for audit trail
    public string? MultiplierTier { get; set; }
    public Percentage DropPercentage { get; set; }
    public decimal High30Day { get; set; } // Stays decimal: uses 0 as sentinel for "data unavailable"
    public decimal Ma200Day { get; set; }  // Stays decimal: uses 0 as sentinel for "data unavailable"
}

public enum PurchaseStatus
{
    Pending,
    Filled,
    PartiallyFilled,
    Failed,
    Cancelled
}
