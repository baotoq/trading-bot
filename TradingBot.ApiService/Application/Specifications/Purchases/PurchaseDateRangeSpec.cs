using Ardalis.Specification;
using TradingBot.ApiService.Models;

namespace TradingBot.ApiService.Application.Specifications.Purchases;

public class PurchaseDateRangeSpec : Specification<Purchase>
{
    public PurchaseDateRangeSpec(DateOnly startDate, DateOnly endDate)
    {
        var startDateTime = startDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endDateTime = endDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        Query.Where(p => p.ExecutedAt >= startDateTime && p.ExecutedAt <= endDateTime);
    }
}
