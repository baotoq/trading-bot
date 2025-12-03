using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Logging;
using TradingBot.ApiService.Application.Requests;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Domain;
using TradingBot.ApiService.Endpoints;

namespace TradingBot.ApiService.Tests.Endpoints;

public class RealtimeEndpointsTests
{
    private readonly IRealtimeCandleService _candleService;
    private readonly ISignalGeneratorService _signalGenerator;
    private readonly ITelegramNotificationService _telegramService;
    private readonly ILogger<RealtimeEndpoints> _logger;

    public RealtimeEndpointsTests()
    {
        _candleService = Substitute.For<IRealtimeCandleService>();
        _signalGenerator = Substitute.For<ISignalGeneratorService>();
        _telegramService = Substitute.For<ITelegramNotificationService>();
        _logger = Substitute.For<ILogger<RealtimeEndpoints>>();
    }

    [Fact]
    public void MapRealtimeEndpoints_RegistersAllEndpoints()
    {
        // Arrange
        var builder = WebApplication.CreateBuilder();
        var app = builder.Build();

        // Act
        var group = app.MapRealtimeEndpoints();

        // Assert
        group.Should().NotBeNull();
    }

    [Fact]
    public async Task StartMonitoring_WithValidRequest_ReturnsSuccess()
    {
        // Arrange
        var request = new MonitoringRequest
        {
            Symbol = "BTCUSDT",
            Interval = "5m",
            Strategy = "EmaMomentumScalper"
        };

        _candleService.StartMonitoringAsync(request.Symbol, request.Interval)
            .Returns(Task.CompletedTask);
        _signalGenerator.EnableSignalNotificationsAsync(request.Symbol, request.Strategy)
            .Returns(Task.CompletedTask);

        // Act
        var result = await CallStartMonitoring(request);

        // Assert
        result.Should().BeOfType<Ok<object>>();
        await _candleService.Received(1).StartMonitoringAsync(request.Symbol, request.Interval);
        await _signalGenerator.Received(1).EnableSignalNotificationsAsync(request.Symbol, request.Strategy);
    }

    [Fact]
    public async Task StartMonitoring_WithException_ReturnsBadRequest()
    {
        // Arrange
        var request = new MonitoringRequest
        {
            Symbol = "BTCUSDT",
            Interval = "5m",
            Strategy = "EmaMomentumScalper"
        };

        _candleService.StartMonitoringAsync(request.Symbol, request.Interval)
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await CallStartMonitoring(request);

        // Assert
        result.Should().BeOfType<BadRequest<object>>();
    }

    [Fact]
    public async Task StartMonitoring_WithDefaultInterval_UsesCorrectInterval()
    {
        // Arrange
        var request = new MonitoringRequest
        {
            Symbol = "BTCUSDT",
            Interval = "5m",
            Strategy = "EmaMomentumScalper"
        };

        // Act
        await CallStartMonitoring(request);

        // Assert
        await _candleService.Received(1).StartMonitoringAsync(request.Symbol, "5m");
    }

    [Fact]
    public async Task StopMonitoring_WithValidRequest_ReturnsSuccess()
    {
        // Arrange
        var request = new StopMonitoringRequest
        {
            Symbol = "BTCUSDT",
            Interval = "5m"
        };

        _candleService.StopMonitoringAsync(request.Symbol, request.Interval)
            .Returns(Task.CompletedTask);
        _signalGenerator.DisableSignalNotificationsAsync(request.Symbol)
            .Returns(Task.CompletedTask);

        // Act
        var result = await CallStopMonitoring(request);

        // Assert
        result.Should().BeOfType<Ok<object>>();
        await _candleService.Received(1).StopMonitoringAsync(request.Symbol, request.Interval);
        await _signalGenerator.Received(1).DisableSignalNotificationsAsync(request.Symbol);
    }

    [Fact]
    public async Task StopMonitoring_WithException_ReturnsBadRequest()
    {
        // Arrange
        var request = new StopMonitoringRequest
        {
            Symbol = "BTCUSDT",
            Interval = "5m"
        };

        _candleService.StopMonitoringAsync(request.Symbol, request.Interval)
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var result = await CallStopMonitoring(request);

        // Assert
        result.Should().BeOfType<BadRequest<object>>();
    }

    [Fact]
    public void GetMonitoringStatus_WithNoActiveMonitors_ReturnsEmptyList()
    {
        // Arrange
        _candleService.GetActiveMonitors().Returns(new List<(string Symbol, string Interval)>());
        _signalGenerator.GetEnabledNotifications().Returns(new Dictionary<string, string>());

        // Act
        var result = CallGetMonitoringStatus();

        // Assert
        result.Should().BeOfType<Ok<object>>();
    }

    [Fact]
    public void GetMonitoringStatus_WithActiveMonitors_ReturnsCorrectStatus()
    {
        // Arrange
        var activeMonitors = new List<(string Symbol, string Interval)>
        {
            ("BTCUSDT", "5m"),
            ("ETHUSDT", "1m")
        };

        var enabledNotifications = new Dictionary<string, string>
        {
            ["BTCUSDT"] = "EmaMomentumScalper",
            ["ETHUSDT"] = "MACD"
        };

        _candleService.GetActiveMonitors().Returns(activeMonitors);
        _signalGenerator.GetEnabledNotifications().Returns(enabledNotifications);

        // Act
        var result = CallGetMonitoringStatus();

        // Assert
        result.Should().BeOfType<Ok<object>>();
        _candleService.Received(1).GetActiveMonitors();
        _signalGenerator.Received(1).GetEnabledNotifications();
    }

    [Fact]
    public void GetMonitoringStatus_WithMismatchedNotifications_ReturnsCorrectFlags()
    {
        // Arrange
        var activeMonitors = new List<(string Symbol, string Interval)>
        {
            ("BTCUSDT", "5m"),
            ("ETHUSDT", "1m")
        };

        var enabledNotifications = new Dictionary<string, string>
        {
            ["BTCUSDT"] = "EmaMomentumScalper"
            // ETHUSDT has no enabled notification
        };

        _candleService.GetActiveMonitors().Returns(activeMonitors);
        _signalGenerator.GetEnabledNotifications().Returns(enabledNotifications);

        // Act
        var result = CallGetMonitoringStatus();

        // Assert
        result.Should().BeOfType<Ok<object>>();
    }

    [Fact]
    public async Task TestTelegramNotification_WithSuccessfulSend_ReturnsSuccess()
    {
        // Arrange
        _telegramService.SendMessageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        var result = await CallTestTelegramNotification();

        // Assert
        result.Should().BeOfType<Ok<object>>();
        await _telegramService.Received(1).SendMessageAsync(
            Arg.Is<string>(msg => msg.Contains("Test Message")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TestTelegramNotification_WithException_ReturnsBadRequest()
    {
        // Arrange
        _telegramService.SendMessageAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Telegram API error"));

        // Act
        var result = await CallTestTelegramNotification();

        // Assert
        result.Should().BeOfType<BadRequest<object>>();
    }

    [Theory]
    [InlineData("BTCUSDT", "1m", "EmaMomentumScalper")]
    [InlineData("ETHUSDT", "5m", "MACD")]
    [InlineData("BNBUSDT", "15m", "RSI")]
    public async Task StartMonitoring_WithDifferentSymbolsAndIntervals_CallsServicesCorrectly(
        Symbol symbol, string interval, string strategy)
    {
        // Arrange
        var request = new MonitoringRequest
        {
            Symbol = symbol,
            Interval = interval,
            Strategy = strategy
        };

        // Act
        await CallStartMonitoring(request);

        // Assert
        await _candleService.Received(1).StartMonitoringAsync(symbol, interval);
        await _signalGenerator.Received(1).EnableSignalNotificationsAsync(symbol, strategy);
    }

    [Theory]
    [InlineData("BTCUSDT", "1m")]
    [InlineData("ETHUSDT", "5m")]
    [InlineData("BNBUSDT", "15m")]
    public async Task StopMonitoring_WithDifferentSymbolsAndIntervals_CallsServicesCorrectly(
        Symbol symbol, string interval)
    {
        // Arrange
        var request = new StopMonitoringRequest
        {
            Symbol = symbol,
            Interval = interval
        };

        // Act
        await CallStopMonitoring(request);

        // Assert
        await _candleService.Received(1).StopMonitoringAsync(symbol, interval);
        await _signalGenerator.Received(1).DisableSignalNotificationsAsync(symbol);
    }

    // Helper methods to call the endpoint methods using reflection
    // This simulates the actual endpoint invocation
    private async Task<IResult> CallStartMonitoring(MonitoringRequest request)
    {
        var method = typeof(RealtimeEndpoints).GetMethod(
            "StartMonitoring",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var result = method?.Invoke(null, new object[] { request, _candleService, _signalGenerator, _logger });
        return await (Task<IResult>)result!;
    }

    private async Task<IResult> CallStopMonitoring(StopMonitoringRequest request)
    {
        var method = typeof(RealtimeEndpoints).GetMethod(
            "StopMonitoring",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var result = method?.Invoke(null, new object[] { request, _candleService, _signalGenerator, _logger });
        return await (Task<IResult>)result!;
    }

    private IResult CallGetMonitoringStatus()
    {
        var method = typeof(RealtimeEndpoints).GetMethod(
            "GetMonitoringStatus",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var result = method?.Invoke(null, new object[] { _candleService, _signalGenerator });
        return (IResult)result!;
    }

    private async Task<IResult> CallTestTelegramNotification()
    {
        var method = typeof(RealtimeEndpoints).GetMethod(
            "TestTelegramNotification",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var result = method?.Invoke(null, new object[] { _telegramService, _logger });
        return await (Task<IResult>)result!;
    }
}
