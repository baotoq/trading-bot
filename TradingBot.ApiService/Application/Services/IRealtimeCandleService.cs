namespace TradingBot.ApiService.Application.Services;

public interface IRealtimeCandleService
{
    Task StartMonitoringAsync(string symbol, string interval, CancellationToken cancellationToken = default);
    Task StopMonitoringAsync(string symbol, string interval);
    bool IsMonitoring(string symbol, string interval);
    IReadOnlyList<(string Symbol, string Interval)> GetActiveMonitors();
}
