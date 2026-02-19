using Ardalis.Specification;
using TradingBot.ApiService.Models;
using TradingBot.ApiService.Models.Values;

namespace TradingBot.ApiService.Application.Specifications.DailyPrices;

public class DailyPriceByDateRangeSpec : Specification<DailyPrice>
{
    public DailyPriceByDateRangeSpec(Symbol symbol, DateOnly startDate)
    {
        Query.Where(dp => dp.Symbol == symbol && dp.Date >= startDate).OrderBy(dp => dp.Date).AsNoTracking();
    }
}
