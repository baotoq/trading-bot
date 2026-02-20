using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Infrastructure.Data;
using TradingBot.ApiService.Models;
using TradingBot.ApiService.Models.Ids;
using TradingBot.ApiService.Models.Values;

namespace TradingBot.ApiService.Endpoints;

public static class FixedDepositEndpoints
{
    public static WebApplication MapFixedDepositEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/portfolio/fixed-deposits")
            .AddEndpointFilter<ApiKeyEndpointFilter>();

        group.MapGet("/", GetAllAsync);
        group.MapGet("/{id}", GetByIdAsync);
        group.MapPost("/", CreateAsync);
        group.MapPut("/{id}", UpdateAsync);
        group.MapDelete("/{id}", DeleteAsync);

        return app;
    }

    private static async Task<IResult> GetAllAsync(
        TradingBotDbContext db,
        CancellationToken ct)
    {
        var fixedDeposits = await db.FixedDeposits
            .OrderBy(fd => fd.MaturityDate)
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var result = fixedDeposits.Select(fd => MapToResponse(fd, today)).ToList();

        return Results.Ok(result);
    }

    private static async Task<IResult> GetByIdAsync(
        TradingBotDbContext db,
        Guid id,
        CancellationToken ct)
    {
        var fd = await db.FixedDeposits
            .FirstOrDefaultAsync(f => f.Id == FixedDepositId.From(id), ct);

        if (fd is null)
            return Results.NotFound();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return Results.Ok(MapToResponse(fd, today));
    }

    private static async Task<IResult> CreateAsync(
        TradingBotDbContext db,
        CreateFixedDepositRequest request,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        if (!Enum.TryParse<CompoundingFrequency>(request.CompoundingFrequency, ignoreCase: true, out var compoundingFreq))
            return Results.BadRequest("Invalid compounding frequency");

        try
        {
            var principal = VndAmount.From(request.Principal);
            var fd = FixedDeposit.Create(
                request.BankName,
                principal,
                request.AnnualInterestRate,
                request.StartDate,
                request.MaturityDate,
                compoundingFreq);

            db.FixedDeposits.Add(fd);
            await db.SaveChangesAsync(ct);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var response = MapToResponse(fd, today);
            return Results.Created($"/api/portfolio/fixed-deposits/{fd.Id.Value}", response);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> UpdateAsync(
        TradingBotDbContext db,
        Guid id,
        UpdateFixedDepositRequest request,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var fd = await db.FixedDeposits
            .AsTracking()
            .FirstOrDefaultAsync(f => f.Id == FixedDepositId.From(id), ct);

        if (fd is null)
            return Results.NotFound();

        if (!Enum.TryParse<CompoundingFrequency>(request.CompoundingFrequency, ignoreCase: true, out var compoundingFreq))
            return Results.BadRequest("Invalid compounding frequency");

        try
        {
            var principal = VndAmount.From(request.Principal);
            fd.Update(
                request.BankName,
                principal,
                request.AnnualInterestRate,
                request.StartDate,
                request.MaturityDate,
                compoundingFreq);

            await db.SaveChangesAsync(ct);

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            return Results.Ok(MapToResponse(fd, today));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> DeleteAsync(
        TradingBotDbContext db,
        Guid id,
        CancellationToken ct)
    {
        var fd = await db.FixedDeposits
            .FirstOrDefaultAsync(f => f.Id == FixedDepositId.From(id), ct);

        if (fd is null)
            return Results.NotFound();

        db.FixedDeposits.Remove(fd);
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    }

    private static FixedDepositResponse MapToResponse(FixedDeposit fd, DateOnly today)
    {
        var accruedValue = InterestCalculator.CalculateAccruedValue(
            fd.Principal.Value, fd.AnnualInterestRate, fd.StartDate, today, fd.CompoundingFrequency);

        var projectedMaturityValue = InterestCalculator.CalculateAccruedValue(
            fd.Principal.Value, fd.AnnualInterestRate, fd.StartDate, fd.MaturityDate, fd.CompoundingFrequency);

        var daysToMaturity = Math.Max(0, fd.MaturityDate.DayNumber - today.DayNumber);

        return new FixedDepositResponse(
            Id: fd.Id.Value,
            BankName: fd.BankName,
            PrincipalVnd: fd.Principal.Value,
            AnnualInterestRate: fd.AnnualInterestRate,
            StartDate: fd.StartDate,
            MaturityDate: fd.MaturityDate,
            CompoundingFrequency: fd.CompoundingFrequency.ToString(),
            Status: fd.Status.ToString(),
            AccruedValueVnd: Math.Round(accruedValue, 0),
            ProjectedMaturityValueVnd: Math.Round(projectedMaturityValue, 0),
            DaysToMaturity: daysToMaturity,
            CreatedAt: fd.CreatedAt);
    }
}
