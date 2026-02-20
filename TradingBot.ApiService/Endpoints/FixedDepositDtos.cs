namespace TradingBot.ApiService.Endpoints;

public record CreateFixedDepositRequest(
    string BankName,
    decimal Principal,
    decimal AnnualInterestRate,
    DateOnly StartDate,
    DateOnly MaturityDate,
    string CompoundingFrequency
);

public record UpdateFixedDepositRequest(
    string BankName,
    decimal Principal,
    decimal AnnualInterestRate,
    DateOnly StartDate,
    DateOnly MaturityDate,
    string CompoundingFrequency
);

public record FixedDepositResponse(
    Guid Id,
    string BankName,
    decimal PrincipalVnd,
    decimal AnnualInterestRate,
    DateOnly StartDate,
    DateOnly MaturityDate,
    string CompoundingFrequency,
    string Status,
    decimal AccruedValueVnd,
    decimal ProjectedMaturityValueVnd,
    int DaysToMaturity,
    DateTimeOffset CreatedAt
);
