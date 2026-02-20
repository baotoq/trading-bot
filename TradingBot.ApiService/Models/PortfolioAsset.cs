using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.Models.Ids;

namespace TradingBot.ApiService.Models;

public class PortfolioAsset : AggregateRoot<PortfolioAssetId>
{
    // Protected parameterless constructor required by EF Core for materialization
    protected PortfolioAsset() { }

    public string Name { get; private set; } = null!;
    public string Ticker { get; private set; } = null!;
    public AssetType AssetType { get; private set; }
    public Currency NativeCurrency { get; private set; }

    private readonly List<AssetTransaction> _transactions = [];
    public IReadOnlyList<AssetTransaction> Transactions => _transactions.AsReadOnly();

    public static PortfolioAsset Create(string name, string ticker, AssetType assetType, Currency nativeCurrency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(ticker);

        return new PortfolioAsset
        {
            Id = PortfolioAssetId.New(),
            Name = name,
            Ticker = ticker,
            AssetType = assetType,
            NativeCurrency = nativeCurrency
        };
    }

    public AssetTransaction AddTransaction(DateOnly date, decimal quantity, decimal pricePerUnit,
        Currency currency, TransactionType type, decimal? fee, TransactionSource source,
        PurchaseId? sourcePurchaseId = null)
    {
        var tx = AssetTransaction.Create(Id, date, quantity, pricePerUnit, currency, type, fee, source, sourcePurchaseId);
        _transactions.Add(tx);
        return tx;
    }
}

public enum AssetType { Crypto, ETF }

public enum Currency { USD, VND }
