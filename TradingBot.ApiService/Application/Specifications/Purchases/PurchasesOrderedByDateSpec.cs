using Ardalis.Specification;
using TradingBot.ApiService.Models;

namespace TradingBot.ApiService.Application.Specifications.Purchases;

public class PurchasesOrderedByDateSpec : Specification<Purchase>
{
    public PurchasesOrderedByDateSpec()
    {
        Query.OrderByDescending(p => p.ExecutedAt).AsNoTracking();
    }
}
