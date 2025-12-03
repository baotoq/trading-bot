using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Services;

public interface IRealtimeCandleService
{
    Task StartMonitoringAsync(Symbol symbol, string interval, CancellationToken cancellationToken = default);
    Task StopMonitoringAsync(Symbol symbol, string interval);
    bool IsMonitoring(Symbol symbol, string interval);
    IReadOnlyList<(Symbol Symbol, string Interval)> GetActiveMonitors();
}
