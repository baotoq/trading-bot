using TradingBot.ApiService.Application.Events;
using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.Models.Ids;
using TradingBot.ApiService.Models.Values;

namespace TradingBot.ApiService.Models;

public class Purchase : AggregateRoot<PurchaseId>
{
    // Protected parameterless constructor required by EF Core for materialization
    protected Purchase() { }

    private Purchase(
        PurchaseId id,
        Price price,
        UsdAmount cost,
        Multiplier multiplier,
        string? multiplierTier,
        Percentage dropPercentage,
        decimal high30Day,
        decimal ma200Day,
        bool isDryRun)
    {
        Id = id;
        ExecutedAt = DateTimeOffset.UtcNow;
        Price = price;
        Quantity = Quantity.From(0);
        Cost = cost;
        Multiplier = multiplier;
        Status = PurchaseStatus.Pending;
        MultiplierTier = multiplierTier;
        DropPercentage = dropPercentage;
        High30Day = high30Day;
        Ma200Day = ma200Day;
        IsDryRun = isDryRun;
    }

    public DateTimeOffset ExecutedAt { get; private set; }
    public Price Price { get; private set; }
    public Quantity Quantity { get; private set; }
    public UsdAmount Cost { get; private set; }
    public Multiplier Multiplier { get; private set; }
    public PurchaseStatus Status { get; private set; } = PurchaseStatus.Pending;
    public bool IsDryRun { get; private set; }
    public string? OrderId { get; private set; }
    public string? RawResponse { get; private set; }
    public string? FailureReason { get; private set; }

    // Multiplier metadata fields for audit trail
    public string? MultiplierTier { get; private set; }
    public Percentage DropPercentage { get; private set; }
    public decimal High30Day { get; private set; } // Stays decimal: uses 0 as sentinel for "data unavailable"
    public decimal Ma200Day { get; private set; }  // Stays decimal: uses 0 as sentinel for "data unavailable"

    public static Purchase Create(
        Price price,
        UsdAmount cost,
        Multiplier multiplier,
        string? multiplierTier,
        Percentage dropPercentage,
        decimal high30Day,
        decimal ma200Day,
        bool isDryRun)
    {
        var purchase = new Purchase(
            PurchaseId.New(),
            price,
            cost,
            multiplier,
            multiplierTier,
            dropPercentage,
            high30Day,
            ma200Day,
            isDryRun);

        purchase.AddDomainEvent(new PurchaseCreatedEvent(
            purchase.Id,
            price.Value,
            cost.Value,
            multiplier.Value,
            DateTimeOffset.UtcNow));

        return purchase;
    }

    public void RecordDryRunFill(Quantity quantity, Price price, UsdAmount actualCost)
    {
        Quantity = quantity;
        Price = price;
        Cost = actualCost;
        Status = PurchaseStatus.Filled;
        OrderId = $"DRY-RUN-{Guid.NewGuid():N}";
        IsDryRun = true;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new PurchaseCompletedEvent(
            Id,
            price.Value,
            quantity.Value,
            actualCost.Value,
            DateTimeOffset.UtcNow));
    }

    public void RecordFill(Quantity quantity, Price avgPrice, UsdAmount actualCost, string orderId, decimal requestedQuantity)
    {
        Quantity = quantity;
        Price = avgPrice;
        Cost = actualCost;
        OrderId = orderId;
        Status = quantity.Value >= requestedQuantity * 0.95m
            ? PurchaseStatus.Filled
            : PurchaseStatus.PartiallyFilled;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new PurchaseCompletedEvent(
            Id,
            avgPrice.Value,
            quantity.Value,
            actualCost.Value,
            DateTimeOffset.UtcNow));
    }

    public void RecordResting(string orderId)
    {
        OrderId = orderId;
        Status = PurchaseStatus.PartiallyFilled;
        FailureReason = "Order resting instead of filling (IOC should not rest)";
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new PurchaseFailedEvent(
            Id,
            FailureReason,
            DateTimeOffset.UtcNow));
    }

    public void RecordFailure(string reason, string? rawResponse = null)
    {
        Status = PurchaseStatus.Failed;
        FailureReason = reason;
        RawResponse = rawResponse;
        UpdatedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new PurchaseFailedEvent(
            Id,
            reason,
            DateTimeOffset.UtcNow));
    }

    public void SetRawResponse(string rawResponse)
    {
        RawResponse = rawResponse;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public enum PurchaseStatus
{
    Pending,
    Filled,
    PartiallyFilled,
    Failed,
    Cancelled
}
