using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.Models.Ids;

namespace TradingBot.ApiService.Models;

public class AssetTransaction : BaseEntity<AssetTransactionId>
{
    // Protected parameterless constructor required by EF Core for materialization
    protected AssetTransaction() { }

    public PortfolioAssetId PortfolioAssetId { get; private set; }
    public DateOnly Date { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal PricePerUnit { get; private set; }
    public Currency Currency { get; private set; }
    public TransactionType Type { get; private set; }
    public decimal? Fee { get; private set; }
    public TransactionSource Source { get; private set; }

    internal static AssetTransaction Create(PortfolioAssetId assetId, DateOnly date, decimal quantity,
        decimal pricePerUnit, Currency currency, TransactionType type, decimal? fee, TransactionSource source)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pricePerUnit);

        return new AssetTransaction
        {
            Id = AssetTransactionId.New(),
            PortfolioAssetId = assetId,
            Date = date,
            Quantity = quantity,
            PricePerUnit = pricePerUnit,
            Currency = currency,
            Type = type,
            Fee = fee,
            Source = source
        };
    }
}

public enum TransactionType { Buy, Sell }

public enum TransactionSource { Manual, Bot }
