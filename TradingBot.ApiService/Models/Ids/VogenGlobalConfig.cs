using Vogen;

[assembly: VogenDefaults(
    underlyingType: typeof(Guid),
    conversions: Conversions.EfCoreValueConverter | Conversions.SystemTextJson,
    toPrimitiveCasting: CastOperator.Implicit,
    fromPrimitiveCasting: CastOperator.Implicit)]
