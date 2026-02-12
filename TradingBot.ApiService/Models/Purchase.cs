using TradingBot.ApiService.BuildingBlocks;

namespace TradingBot.ApiService.Models;

public class Purchase : BaseEntity
{
    public DateTimeOffset ExecutedAt { get; set; }
    public decimal Price { get; set; }
    public decimal Quantity { get; set; }
    public decimal Cost { get; set; }
    public decimal Multiplier { get; set; }
    public PurchaseStatus Status { get; set; } = PurchaseStatus.Pending;
    public string? OrderId { get; set; }
    public string? RawResponse { get; set; }
    public string? FailureReason { get; set; }
}

public enum PurchaseStatus
{
    Pending,
    Filled,
    PartiallyFilled,
    Failed,
    Cancelled
}
