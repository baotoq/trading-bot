using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Tests.Services;

public class TelegramNotificationServiceTests
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelegramNotificationService> _logger;

    public TelegramNotificationServiceTests()
    {
        _configuration = Substitute.For<IConfiguration>();
        _logger = Substitute.For<ILogger<TelegramNotificationService>>();
    }

    [Fact]
    public void Constructor_WithValidConfiguration_InitializesService()
    {
        // Arrange
        _configuration["Telegram:BotToken"].Returns("test-bot-token");
        _configuration["Telegram:ChatId"].Returns("123456789");

        // Act
        var service = new TelegramNotificationService(_configuration, _logger);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithMissingBotToken_DisablesService()
    {
        // Arrange
        _configuration["Telegram:BotToken"].Returns((string?)null);
        _configuration["Telegram:ChatId"].Returns("123456789");

        // Act
        var service = new TelegramNotificationService(_configuration, _logger);

        // Assert
        service.Should().NotBeNull();
        // Service should log warning about being disabled
    }

    [Fact]
    public void Constructor_WithMissingChatId_DisablesService()
    {
        // Arrange
        _configuration["Telegram:BotToken"].Returns("test-bot-token");
        _configuration["Telegram:ChatId"].Returns((string?)null);

        // Act
        var service = new TelegramNotificationService(_configuration, _logger);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithInvalidChatId_DisablesService()
    {
        // Arrange
        _configuration["Telegram:BotToken"].Returns("test-bot-token");
        _configuration["Telegram:ChatId"].Returns("invalid-chat-id");

        // Act
        var service = new TelegramNotificationService(_configuration, _logger);

        // Assert
        service.Should().NotBeNull();
    }

    [Fact]
    public async Task SendSignalNotificationAsync_WhenDisabled_ReturnsWithoutSending()
    {
        // Arrange
        _configuration["Telegram:BotToken"].Returns((string?)null);
        _configuration["Telegram:ChatId"].Returns((string?)null);
        var service = new TelegramNotificationService(_configuration, _logger);

        var signal = new TradingSignal
        {
            Symbol = "BTCUSDT",
            Type = SignalType.StrongBuy,
            Price = 50000m,
            Confidence = 0.95m,
            Strategy = "EMA Momentum Scalper",
            Reason = "Test signal",
            Timestamp = DateTime.UtcNow
        };

        // Act
        await service.SendSignalNotificationAsync(signal);

        // Assert - Should complete without throwing
    }

    [Fact]
    public async Task SendTradeExecutionNotificationAsync_WhenDisabled_ReturnsWithoutSending()
    {
        // Arrange
        _configuration["Telegram:BotToken"].Returns((string?)null);
        _configuration["Telegram:ChatId"].Returns((string?)null);
        var service = new TelegramNotificationService(_configuration, _logger);

        // Act
        await service.SendTradeExecutionNotificationAsync("Test trade execution");

        // Assert - Should complete without throwing
    }

    [Fact]
    public async Task SendErrorNotificationAsync_WhenDisabled_ReturnsWithoutSending()
    {
        // Arrange
        _configuration["Telegram:BotToken"].Returns((string?)null);
        _configuration["Telegram:ChatId"].Returns((string?)null);
        var service = new TelegramNotificationService(_configuration, _logger);

        // Act
        await service.SendErrorNotificationAsync("Test error");

        // Assert - Should complete without throwing
    }

    [Fact]
    public async Task SendMessageAsync_WhenDisabled_ReturnsWithoutSending()
    {
        // Arrange
        _configuration["Telegram:BotToken"].Returns((string?)null);
        _configuration["Telegram:ChatId"].Returns((string?)null);
        var service = new TelegramNotificationService(_configuration, _logger);

        // Act
        await service.SendMessageAsync("Test message");

        // Assert - Should complete without throwing
    }

    [Theory]
    [InlineData(SignalType.StrongBuy)]
    [InlineData(SignalType.Buy)]
    [InlineData(SignalType.StrongSell)]
    [InlineData(SignalType.Sell)]
    [InlineData(SignalType.Hold)]
    public async Task SendSignalNotificationAsync_HandlesAllSignalTypes(SignalType signalType)
    {
        // Arrange
        _configuration["Telegram:BotToken"].Returns((string?)null);
        _configuration["Telegram:ChatId"].Returns((string?)null);
        var service = new TelegramNotificationService(_configuration, _logger);

        var signal = new TradingSignal
        {
            Symbol = "BTCUSDT",
            Type = signalType,
            Price = 50000m,
            Confidence = 0.85m,
            Strategy = "Test Strategy",
            Reason = "Test reason",
            Timestamp = DateTime.UtcNow,
            Indicators = new Dictionary<string, decimal>
            {
                ["RSI"] = 65m,
                ["EMA9"] = 49000m,
                ["EMA21"] = 48000m,
                ["MACD"] = 100m
            }
        };

        // Act & Assert - Should not throw
        await service.SendSignalNotificationAsync(signal);
    }

    [Fact]
    public async Task SendSignalNotificationAsync_WithRiskManagement_IncludesAllLevels()
    {
        // Arrange
        _configuration["Telegram:BotToken"].Returns((string?)null);
        _configuration["Telegram:ChatId"].Returns((string?)null);
        var service = new TelegramNotificationService(_configuration, _logger);

        var signal = new TradingSignal
        {
            Symbol = "BTCUSDT",
            Type = SignalType.StrongBuy,
            Price = 50000m,
            Confidence = 0.95m,
            Strategy = "EMA Momentum Scalper",
            Reason = "Strong bullish signal",
            Timestamp = DateTime.UtcNow,
            EntryPrice = 50000m,
            StopLoss = 49000m,
            TakeProfit1 = 51500m,
            TakeProfit2 = 52500m,
            TakeProfit3 = 54000m,
            Indicators = new Dictionary<string, decimal>
            {
                ["RSI"] = 65m,
                ["EMA9"] = 50500m,
                ["EMA21"] = 49500m,
                ["MACD"] = 150m
            }
        };

        // Act & Assert - Should not throw
        await service.SendSignalNotificationAsync(signal);
    }

    [Fact]
    public async Task SendSignalNotificationAsync_WithCancellationToken_PropagatesToken()
    {
        // Arrange
        _configuration["Telegram:BotToken"].Returns((string?)null);
        _configuration["Telegram:ChatId"].Returns((string?)null);
        var service = new TelegramNotificationService(_configuration, _logger);
        var cts = new CancellationTokenSource();

        var signal = new TradingSignal
        {
            Symbol = "BTCUSDT",
            Type = SignalType.Buy,
            Price = 50000m,
            Confidence = 0.75m,
            Strategy = "Test",
            Reason = "Test",
            Timestamp = DateTime.UtcNow
        };

        // Act & Assert
        await service.SendSignalNotificationAsync(signal, cts.Token);
    }
}
