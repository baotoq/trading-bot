using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.Models.Ids;
using TradingBot.ApiService.Models.Values;

namespace TradingBot.ApiService.Models;

public class FixedDeposit : AggregateRoot<FixedDepositId>
{
    // Protected parameterless constructor required by EF Core for materialization
    protected FixedDeposit() { }

    public string BankName { get; private set; } = null!;
    public VndAmount Principal { get; private set; }
    public decimal AnnualInterestRate { get; private set; }
    public DateOnly StartDate { get; private set; }
    public DateOnly MaturityDate { get; private set; }
    public CompoundingFrequency CompoundingFrequency { get; private set; }
    public FixedDepositStatus Status { get; private set; }

    public static FixedDeposit Create(string bankName, VndAmount principal, decimal annualInterestRate,
        DateOnly startDate, DateOnly maturityDate, CompoundingFrequency compoundingFrequency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bankName);

        if (maturityDate <= startDate)
            throw new ArgumentException("Maturity date must be after start date");

        if (annualInterestRate <= 0 || annualInterestRate > 1)
            throw new ArgumentException("Annual interest rate must be between 0 and 1 (exclusive)");

        return new FixedDeposit
        {
            Id = FixedDepositId.New(),
            BankName = bankName,
            Principal = principal,
            AnnualInterestRate = annualInterestRate,
            StartDate = startDate,
            MaturityDate = maturityDate,
            CompoundingFrequency = compoundingFrequency,
            Status = FixedDepositStatus.Active
        };
    }

    public void Update(string bankName, VndAmount principal, decimal annualInterestRate,
        DateOnly startDate, DateOnly maturityDate, CompoundingFrequency compoundingFrequency)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bankName);

        if (maturityDate <= startDate)
            throw new ArgumentException("Maturity date must be after start date");

        if (annualInterestRate <= 0 || annualInterestRate > 1)
            throw new ArgumentException("Annual interest rate must be between 0 and 1 (exclusive)");

        BankName = bankName;
        Principal = principal;
        AnnualInterestRate = annualInterestRate;
        StartDate = startDate;
        MaturityDate = maturityDate;
        CompoundingFrequency = compoundingFrequency;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Mature()
    {
        Status = FixedDepositStatus.Matured;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public enum CompoundingFrequency
{
    Simple,
    Monthly,
    Quarterly,
    SemiAnnual,
    Annual
}

public enum FixedDepositStatus { Active, Matured }
