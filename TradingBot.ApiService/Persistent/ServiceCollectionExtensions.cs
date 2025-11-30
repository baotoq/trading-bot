using Binance.Net;
using Binance.Net.Clients;
using Binance.Net.Interfaces.Clients;
using CryptoExchange.Net.Authentication;
using TradingBot.ApiService.Services;
using TradingBot.ApiService.Services.Backtesting;
using TradingBot.ApiService.Services.RealTimeTrading;
using TradingBot.ApiService.Services.Strategy;

namespace TradingBot.ApiService.Persistent;

public static class ServiceCollectionExtensions
{
    extension(IHostApplicationBuilder builder)
    {
        public void AddPersistentServices()
        {
            builder.AddNpgsqlDbContext<ApplicationDbContext>("tradingbotdb");
            builder.Services.AddHostedService<EfCoreMigrationHostedService>();
        }
    }
}