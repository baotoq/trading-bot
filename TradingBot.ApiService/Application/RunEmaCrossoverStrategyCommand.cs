using MediatR;
using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Application.Strategies;
using TradingBot.ApiService.Domain;
using TradingBot.ApiService.Infrastructure;

namespace TradingBot.ApiService.Application;

public record RunEmaCrossoverStrategyCommand : IRequest<BacktestResult>
{
}

public class RunEmaCrossoverStrategyCommandHandler(ApplicationDbContext context) : IRequestHandler<RunEmaCrossoverStrategyCommand, BacktestResult>
{
    public async Task<BacktestResult> Handle(RunEmaCrossoverStrategyCommand request, CancellationToken cancellationToken)
    {
        var candles = await context.Candles
            .Where(c => c.Symbol == "BTCUSDT" && c.Interval == "4h")
            .OrderBy(c => c.OpenTime)
            .ToListAsync(cancellationToken: cancellationToken);

        var strategy = new EmaCrossoverStrategy();

        var backtestResult = strategy.Backtest(candles, initialCapital: 10000m, feePercentage: 0.1m);

        return backtestResult;
    }
}