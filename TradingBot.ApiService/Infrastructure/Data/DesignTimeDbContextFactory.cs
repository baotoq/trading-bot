using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TradingBot.ApiService.Infrastructure.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<TradingBotDbContext>
{
    public TradingBotDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TradingBotDbContext>();

        // This connection string is only used for design-time migration generation
        // At runtime, Aspire provides the actual connection string
        optionsBuilder.UseNpgsql("Host=localhost;Database=tradingbotdb;Username=postgres;Password=postgres");

        return new TradingBotDbContext(optionsBuilder.Options);
    }
}
