using Ardalis.Specification;
using TradingBot.ApiService.Models;

namespace TradingBot.ApiService.Application.Specifications.Purchases;

public class PurchaseFilledStatusSpec : Specification<Purchase>
{
    public PurchaseFilledStatusSpec()
    {
        Query.Where(p => !p.IsDryRun && (p.Status == PurchaseStatus.Filled || p.Status == PurchaseStatus.PartiallyFilled));
    }
}
