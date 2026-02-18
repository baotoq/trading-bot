using Vogen;

[assembly: VogenDefaults(
    underlyingType: typeof(Guid),
    conversions: Conversions.EfCoreValueConverter | Conversions.SystemTextJson | Conversions.TypeConverter,
    toPrimitiveCasting: CastOperator.Implicit,
    fromPrimitiveCasting: CastOperator.Implicit)]
