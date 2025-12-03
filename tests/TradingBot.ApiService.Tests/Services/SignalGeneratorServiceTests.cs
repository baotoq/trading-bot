using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Application.Strategies;
using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Tests.Services;

public class SignalGeneratorServiceTests
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITelegramNotificationService _telegramService;
    private readonly ILogger<SignalGeneratorService> _logger;
    private readonly IStrategy _mockStrategy;

    public SignalGeneratorServiceTests()
    {
        _telegramService = Substitute.For<ITelegramNotificationService>();
        _logger = Substitute.For<ILogger<SignalGeneratorService>>();
        _mockStrategy = Substitute.For<IStrategy>();

        // Setup service provider with scoped services
        var services = new ServiceCollection();
        services.AddScoped<EmaMomentumScalperStrategy>(_ => (EmaMomentumScalperStrategy)_mockStrategy);
        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public async Task GenerateSignalAsync_WithoutEnabledNotifications_DoesNotSendSignal()
    {
        // Arrange
        var service = new SignalGeneratorService(_serviceProvider, _telegramService, _logger);
        Symbol symbol = "BTCUSDT";

        // Act
        await service.GenerateSignalAsync(symbol);

        // Assert
        await _telegramService.DidNotReceive().SendSignalNotificationAsync(
            Arg.Any<TradingSignal>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnableSignalNotificationsAsync_EnablesNotificationsForSymbol()
    {
        // Arrange
        var service = new SignalGeneratorService(_serviceProvider, _telegramService, _logger);
        Symbol symbol = "BTCUSDT";
        var strategy = "EmaMomentumScalper";

        // Act
        await service.EnableSignalNotificationsAsync(symbol, strategy);

        // Assert
        service.IsNotificationEnabled(symbol).Should().BeTrue();
    }

    [Fact]
    public async Task DisableSignalNotificationsAsync_DisablesNotificationsForSymbol()
    {
        // Arrange
        var service = new SignalGeneratorService(_serviceProvider, _telegramService, _logger);
        Symbol symbol = "BTCUSDT";
        var strategy = "EmaMomentumScalper";

        await service.EnableSignalNotificationsAsync(symbol, strategy);

        // Act
        await service.DisableSignalNotificationsAsync(symbol);

        // Assert
        service.IsNotificationEnabled(symbol).Should().BeFalse();
    }

    [Fact]
    public async Task GetEnabledNotifications_ReturnsAllEnabledSymbols()
    {
        // Arrange
        var service = new SignalGeneratorService(_serviceProvider, _telegramService, _logger);

        await service.EnableSignalNotificationsAsync("BTCUSDT", "EmaMomentumScalper");
        await service.EnableSignalNotificationsAsync("ETHUSDT", "EmaMomentumScalper");

        // Act
        var enabled = service.GetEnabledNotifications();

        // Assert
        enabled.Should().HaveCount(2);
        enabled.Should().ContainKey("BTCUSDT");
        enabled.Should().ContainKey("ETHUSDT");
    }

    [Fact]
    public async Task GenerateSignalAsync_WithHoldSignal_DoesNotSendNotification()
    {
        // Arrange
        var service = new SignalGeneratorService(_serviceProvider, _telegramService, _logger);
        Symbol symbol = "BTCUSDT";

        _mockStrategy.AnalyzeAsync(symbol, Arg.Any<CancellationToken>())
            .Returns(new TradingSignal
            {
                Symbol = symbol,
                Type = SignalType.Hold,
                Price = 50000m,
                Confidence = 0.5m,
                Strategy = "Test",
                Reason = "No clear signal",
                Timestamp = DateTime.UtcNow
            });

        await service.EnableSignalNotificationsAsync(symbol, "EmaMomentumScalper");

        // Act
        await service.GenerateSignalAsync(symbol);

        // Assert
        await _telegramService.DidNotReceive().SendSignalNotificationAsync(
            Arg.Any<TradingSignal>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateSignalAsync_WithBuySignal_SendsNotification()
    {
        // Arrange
        var service = new SignalGeneratorService(_serviceProvider, _telegramService, _logger);
        Symbol symbol = "BTCUSDT";

        var expectedSignal = new TradingSignal
        {
            Symbol = symbol,
            Type = SignalType.Buy,
            Price = 50000m,
            Confidence = 0.75m,
            Strategy = "EMA Momentum Scalper",
            Reason = "Bullish crossover",
            Timestamp = DateTime.UtcNow
        };

        _mockStrategy.AnalyzeAsync(symbol, Arg.Any<CancellationToken>())
            .Returns(expectedSignal);

        await service.EnableSignalNotificationsAsync(symbol, "EmaMomentumScalper");

        // Act
        await service.GenerateSignalAsync(symbol);

        // Assert
        await _telegramService.Received(1).SendSignalNotificationAsync(
            Arg.Is<TradingSignal>(s => s.Type == SignalType.Buy && s.Symbol == symbol),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateSignalAsync_WithSameSignalTypeDuringCooldown_DoesNotSendDuplicateNotification()
    {
        // Arrange
        var service = new SignalGeneratorService(_serviceProvider, _telegramService, _logger);
        Symbol symbol = "BTCUSDT";

        var signal = new TradingSignal
        {
            Symbol = symbol,
            Type = SignalType.Buy,
            Price = 50000m,
            Confidence = 0.75m,
            Strategy = "EMA Momentum Scalper",
            Reason = "Bullish signal",
            Timestamp = DateTime.UtcNow
        };

        _mockStrategy.AnalyzeAsync(symbol, Arg.Any<CancellationToken>())
            .Returns(signal);

        await service.EnableSignalNotificationsAsync(symbol, "EmaMomentumScalper");

        // Act - Generate signal twice quickly
        await service.GenerateSignalAsync(symbol);
        await service.GenerateSignalAsync(symbol);

        // Assert - Should only send once due to cooldown
        await _telegramService.Received(1).SendSignalNotificationAsync(
            Arg.Any<TradingSignal>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateSignalAsync_WithDifferentSignalTypeDuringCooldown_SendsBothNotifications()
    {
        // Arrange
        var service = new SignalGeneratorService(_serviceProvider, _telegramService, _logger);
        Symbol symbol = "BTCUSDT";

        var buySignal = new TradingSignal
        {
            Symbol = symbol,
            Type = SignalType.Buy,
            Price = 50000m,
            Confidence = 0.75m,
            Strategy = "EMA Momentum Scalper",
            Reason = "Bullish signal",
            Timestamp = DateTime.UtcNow
        };

        var sellSignal = new TradingSignal
        {
            Symbol = symbol,
            Type = SignalType.Sell,
            Price = 50500m,
            Confidence = 0.75m,
            Strategy = "EMA Momentum Scalper",
            Reason = "Bearish signal",
            Timestamp = DateTime.UtcNow
        };

        _mockStrategy.AnalyzeAsync(symbol, Arg.Any<CancellationToken>())
            .Returns(buySignal, sellSignal);

        await service.EnableSignalNotificationsAsync(symbol, "EmaMomentumScalper");

        // Act - Generate different signal types
        await service.GenerateSignalAsync(symbol);
        await service.GenerateSignalAsync(symbol);

        // Assert - Should send both notifications
        await _telegramService.Received(2).SendSignalNotificationAsync(
            Arg.Any<TradingSignal>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateSignalAsync_WithException_LogsErrorAndDoesNotThrow()
    {
        // Arrange
        var service = new SignalGeneratorService(_serviceProvider, _telegramService, _logger);
        Symbol symbol = "BTCUSDT";

        _mockStrategy.AnalyzeAsync(symbol, Arg.Any<CancellationToken>())
            .Returns<TradingSignal>(x => throw new Exception("Test exception"));

        await service.EnableSignalNotificationsAsync(symbol, "EmaMomentumScalper");

        // Act & Assert - Should not throw
        await service.GenerateSignalAsync(symbol);
    }

    [Fact]
    public async Task GenerateSignalAsync_WithCancellationToken_PropagatesToken()
    {
        // Arrange
        var service = new SignalGeneratorService(_serviceProvider, _telegramService, _logger);
        Symbol symbol = "BTCUSDT";
        var cts = new CancellationTokenSource();

        var signal = new TradingSignal
        {
            Symbol = symbol,
            Type = SignalType.Buy,
            Price = 50000m,
            Confidence = 0.75m,
            Strategy = "Test",
            Reason = "Test",
            Timestamp = DateTime.UtcNow
        };

        _mockStrategy.AnalyzeAsync(symbol, cts.Token)
            .Returns(signal);

        await service.EnableSignalNotificationsAsync(symbol, "EmaMomentumScalper");

        // Act
        await service.GenerateSignalAsync(symbol, cts.Token);

        // Assert
        await _mockStrategy.Received(1).AnalyzeAsync(symbol, cts.Token);
    }

    [Fact]
    public async Task DisableSignalNotificationsAsync_ClearsCooldownData()
    {
        // Arrange
        var service = new SignalGeneratorService(_serviceProvider, _telegramService, _logger);
        Symbol symbol = "BTCUSDT";

        var signal = new TradingSignal
        {
            Symbol = symbol,
            Type = SignalType.Buy,
            Price = 50000m,
            Confidence = 0.75m,
            Strategy = "EMA Momentum Scalper",
            Reason = "Bullish signal",
            Timestamp = DateTime.UtcNow
        };

        _mockStrategy.AnalyzeAsync(symbol, Arg.Any<CancellationToken>())
            .Returns(signal);

        await service.EnableSignalNotificationsAsync(symbol, "EmaMomentumScalper");
        await service.GenerateSignalAsync(symbol);

        // Act
        await service.DisableSignalNotificationsAsync(symbol);
        await service.EnableSignalNotificationsAsync(symbol, "EmaMomentumScalper");
        await service.GenerateSignalAsync(symbol);

        // Assert - After re-enabling, cooldown should be cleared
        await _telegramService.Received(2).SendSignalNotificationAsync(
            Arg.Any<TradingSignal>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void IsNotificationEnabled_WithNonExistentSymbol_ReturnsFalse()
    {
        // Arrange
        var service = new SignalGeneratorService(_serviceProvider, _telegramService, _logger);

        // Act
        var isEnabled = service.IsNotificationEnabled("BTCUSDT");

        // Assert
        isEnabled.Should().BeFalse();
    }

    [Theory]
    [InlineData("EmaMomentumScalper")]
    [InlineData("ema")]
    [InlineData("MACD")]
    [InlineData("RSI")]
    public async Task EnableSignalNotificationsAsync_WithDifferentStrategies_StoresCorrectStrategy(string strategy)
    {
        // Arrange
        var service = new SignalGeneratorService(_serviceProvider, _telegramService, _logger);
        Symbol symbol = "BTCUSDT";

        // Act
        await service.EnableSignalNotificationsAsync(symbol, strategy);
        var enabled = service.GetEnabledNotifications();

        // Assert
        enabled.Should().ContainKey(symbol);
        enabled[symbol].Should().Be(strategy);
    }
}
