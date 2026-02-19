using Ardalis.Specification;
using TradingBot.ApiService.Models;

namespace TradingBot.ApiService.Application.Specifications.Purchases;

public class PurchaseTierFilterSpec : Specification<Purchase>
{
    public PurchaseTierFilterSpec(string tier)
    {
        if (tier == "Base")
        {
            Query.Where(p => p.MultiplierTier == null || p.MultiplierTier == "Base");
        }
        else
        {
            Query.Where(p => p.MultiplierTier == tier);
        }
    }
}
