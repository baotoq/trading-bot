namespace TradingBot.ApiService.Application.Requests;

public record MonitoringRequest(
    string Symbol,
    string Interval = "5m",
    string Strategy = "EmaMomentumScalper"
);

public record StopMonitoringRequest(
    string Symbol,
    string Interval = "5m"
);
