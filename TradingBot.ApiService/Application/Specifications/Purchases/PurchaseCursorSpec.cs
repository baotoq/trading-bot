using Ardalis.Specification;
using TradingBot.ApiService.Models;

namespace TradingBot.ApiService.Application.Specifications.Purchases;

public class PurchaseCursorSpec : Specification<Purchase>
{
    public PurchaseCursorSpec(DateTimeOffset cursor)
    {
        Query.Where(p => p.ExecutedAt < cursor).OrderByDescending(p => p.ExecutedAt);
    }
}
