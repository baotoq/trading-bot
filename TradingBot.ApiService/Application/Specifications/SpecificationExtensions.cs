using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;

namespace TradingBot.ApiService.Application.Specifications;

public static class SpecificationExtensions
{
    public static IQueryable<T> WithSpecification<T>(this IQueryable<T> source, Specification<T> spec) where T : class
    {
        return SpecificationEvaluator.Default.GetQuery(source, spec);
    }
}
