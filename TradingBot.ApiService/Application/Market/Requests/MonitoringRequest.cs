using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Requests;

public record MonitoringRequest(
    Symbol Symbol,
    string Interval = "5m",
    string Strategy = "EmaMomentumScalper"
);

public record StopMonitoringRequest(
    Symbol Symbol,
    string Interval = "5m"
);
