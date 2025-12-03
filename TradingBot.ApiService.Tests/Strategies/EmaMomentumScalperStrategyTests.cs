using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Application.Strategies;
using TradingBot.ApiService.Domain;
using TradingBot.ApiService.Infrastructure;

namespace TradingBot.ApiService.Tests.Strategies;

public class EmaMomentumScalperStrategyTests
{
    private readonly ApplicationDbContext _context;
    private readonly ITechnicalIndicatorService _indicatorService;
    private readonly IMarketAnalysisService _marketAnalysisService;
    private readonly ILogger<EmaMomentumScalperStrategy> _logger;

    public EmaMomentumScalperStrategyTests()
    {
        // Setup in-memory database
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _indicatorService = Substitute.For<ITechnicalIndicatorService>();
        _marketAnalysisService = Substitute.For<IMarketAnalysisService>();
        _logger = Substitute.For<ILogger<EmaMomentumScalperStrategy>>();
    }

    [Fact]
    public void Name_ReturnsCorrectStrategyName()
    {
        // Arrange
        var strategy = new EmaMomentumScalperStrategy(
            _context,
            _indicatorService,
            _marketAnalysisService,
            _logger);

        // Act & Assert
        strategy.Name.Should().Be("EMA Momentum Scalper");
    }

    [Fact]
    public async Task AnalyzeAsync_WithNoTrendAlignment_ReturnsHoldSignal()
    {
        // Arrange
        var strategy = new EmaMomentumScalperStrategy(
            _context,
            _indicatorService,
            _marketAnalysisService,
            _logger);

        var symbol = "BTCUSDT";

        _marketAnalysisService.CheckTrendAlignmentAsync(symbol, TradeSide.Long, Arg.Any<CancellationToken>())
            .Returns(false);
        _marketAnalysisService.CheckTrendAlignmentAsync(symbol, TradeSide.Short, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var signal = await strategy.AnalyzeAsync(symbol);

        // Assert
        signal.Type.Should().Be(SignalType.Hold);
        signal.Reason.Should().Contain("No clear trend alignment");
        signal.Confidence.Should().Be(0);
    }

    [Fact]
    public async Task AnalyzeAsync_WithInsufficientData_ReturnsHoldSignal()
    {
        // Arrange
        var strategy = new EmaMomentumScalperStrategy(
            _context,
            _indicatorService,
            _marketAnalysisService,
            _logger);

        var symbol = "BTCUSDT";

        _marketAnalysisService.CheckTrendAlignmentAsync(symbol, TradeSide.Long, Arg.Any<CancellationToken>())
            .Returns(true);

        // Add insufficient candles (less than 50)
        var candles = GenerateCandles(symbol, "5m", 30, 50000m);
        await _context.Candles.AddRangeAsync(candles);
        await _context.SaveChangesAsync();

        // Act
        var signal = await strategy.AnalyzeAsync(symbol);

        // Assert
        signal.Type.Should().Be(SignalType.Hold);
        signal.Reason.Should().Contain("Insufficient");
        signal.Confidence.Should().Be(0);
    }

    [Fact]
    public async Task AnalyzeAsync_WithNoCrossover_ReturnsHoldSignal()
    {
        // Arrange
        var strategy = new EmaMomentumScalperStrategy(
            _context,
            _indicatorService,
            _marketAnalysisService,
            _logger);

        var symbol = "BTCUSDT";

        _marketAnalysisService.CheckTrendAlignmentAsync(symbol, TradeSide.Long, Arg.Any<CancellationToken>())
            .Returns(true);

        // Add sufficient candles
        var candles = GenerateCandles(symbol, "5m", 100, 50000m);
        await _context.Candles.AddRangeAsync(candles);
        await _context.SaveChangesAsync();

        // Setup indicators - no crossover (EMA9 already above EMA21)
        _indicatorService.CalculateEMA(Arg.Any<List<Candle>>(), 9).Returns(50500m);
        _indicatorService.CalculateEMA(Arg.Any<List<Candle>>(), 21).Returns(49500m);

        // Act
        var signal = await strategy.AnalyzeAsync(symbol);

        // Assert
        signal.Type.Should().Be(SignalType.Hold);
        signal.Reason.Should().Contain("No EMA crossover");
    }

    [Fact]
    public async Task AnalyzeAsync_WithBullishCrossoverAndAllConditions_ReturnsStrongBuySignal()
    {
        // Arrange
        var strategy = new EmaMomentumScalperStrategy(
            _context,
            _indicatorService,
            _marketAnalysisService,
            _logger);

        var symbol = "BTCUSDT";
        var currentPrice = 50000m;

        _marketAnalysisService.CheckTrendAlignmentAsync(symbol, TradeSide.Long, Arg.Any<CancellationToken>())
            .Returns(true);
        _marketAnalysisService.CheckTrendAlignmentAsync(symbol, TradeSide.Short, Arg.Any<CancellationToken>())
            .Returns(false);

        // Add candles
        var candles = GenerateCandles(symbol, "5m", 100, currentPrice);
        await _context.Candles.AddRangeAsync(candles);
        await _context.SaveChangesAsync();

        // Setup bullish crossover: previous EMA9 <= EMA21, current EMA9 > EMA21
        _indicatorService.CalculateEMA(Arg.Is<List<Candle>>(c => c.Count == 100), 9).Returns(50100m);
        _indicatorService.CalculateEMA(Arg.Is<List<Candle>>(c => c.Count == 100), 21).Returns(49900m);
        _indicatorService.CalculateEMA(Arg.Is<List<Candle>>(c => c.Count == 99), 9).Returns(49900m);
        _indicatorService.CalculateEMA(Arg.Is<List<Candle>>(c => c.Count == 99), 21).Returns(50000m);

        // Setup indicators for long signal
        _indicatorService.CalculateRSI(Arg.Any<List<Candle>>(), 14).Returns(65m); // In range 50-75
        _indicatorService.CalculateMACD(Arg.Any<List<Candle>>()).Returns((150m, 100m, 50m)); // Bullish MACD
        _indicatorService.CalculateBollingerBands(Arg.Any<List<Candle>>(), 20, 2).Returns((51000m, 50000m, 49000m));
        _indicatorService.CalculateAverageVolume(Arg.Any<List<Candle>>(), 20).Returns(1000000m);
        _indicatorService.GetSwingHigh(Arg.Any<List<Candle>>(), 10).Returns(49800m);
        _indicatorService.GetSwingLow(Arg.Any<List<Candle>>(), 10).Returns(49000m);

        // Act
        var signal = await strategy.AnalyzeAsync(symbol);

        // Assert
        signal.Type.Should().BeOneOf(SignalType.Buy, SignalType.StrongBuy);
        signal.Confidence.Should().BeGreaterThan(0);
        signal.EntryPrice.Should().Be(currentPrice);
        signal.StopLoss.Should().BeLessThan(signal.EntryPrice);
        signal.TakeProfit1.Should().BeGreaterThan(signal.EntryPrice);
        signal.TakeProfit2.Should().BeGreaterThan(signal.TakeProfit1);
        signal.TakeProfit3.Should().BeGreaterThan(signal.TakeProfit2);
    }

    [Fact]
    public async Task AnalyzeAsync_WithBearishCrossoverAndAllConditions_ReturnsStrongSellSignal()
    {
        // Arrange
        var strategy = new EmaMomentumScalperStrategy(
            _context,
            _indicatorService,
            _marketAnalysisService,
            _logger);

        var symbol = "BTCUSDT";
        var currentPrice = 50000m;

        _marketAnalysisService.CheckTrendAlignmentAsync(symbol, TradeSide.Long, Arg.Any<CancellationToken>())
            .Returns(false);
        _marketAnalysisService.CheckTrendAlignmentAsync(symbol, TradeSide.Short, Arg.Any<CancellationToken>())
            .Returns(true);

        // Add candles
        var candles = GenerateCandles(symbol, "5m", 100, currentPrice);
        await _context.Candles.AddRangeAsync(candles);
        await _context.SaveChangesAsync();

        // Setup bearish crossover: previous EMA9 >= EMA21, current EMA9 < EMA21
        _indicatorService.CalculateEMA(Arg.Is<List<Candle>>(c => c.Count == 100), 9).Returns(49900m);
        _indicatorService.CalculateEMA(Arg.Is<List<Candle>>(c => c.Count == 100), 21).Returns(50100m);
        _indicatorService.CalculateEMA(Arg.Is<List<Candle>>(c => c.Count == 99), 9).Returns(50100m);
        _indicatorService.CalculateEMA(Arg.Is<List<Candle>>(c => c.Count == 99), 21).Returns(50000m);

        // Setup indicators for short signal
        _indicatorService.CalculateRSI(Arg.Any<List<Candle>>(), 14).Returns(35m); // In range 25-50
        _indicatorService.CalculateMACD(Arg.Any<List<Candle>>()).Returns((-150m, -100m, -50m)); // Bearish MACD
        _indicatorService.CalculateBollingerBands(Arg.Any<List<Candle>>(), 20, 2).Returns((51000m, 50000m, 49000m));
        _indicatorService.CalculateAverageVolume(Arg.Any<List<Candle>>(), 20).Returns(1000000m);
        _indicatorService.GetSwingHigh(Arg.Any<List<Candle>>(), 10).Returns(51000m);
        _indicatorService.GetSwingLow(Arg.Any<List<Candle>>(), 10).Returns(50200m);

        // Act
        var signal = await strategy.AnalyzeAsync(symbol);

        // Assert
        signal.Type.Should().BeOneOf(SignalType.Sell, SignalType.StrongSell);
        signal.Confidence.Should().BeGreaterThan(0);
        signal.EntryPrice.Should().Be(currentPrice);
        signal.StopLoss.Should().BeGreaterThan(signal.EntryPrice);
        signal.TakeProfit1.Should().BeLessThan(signal.EntryPrice);
        signal.TakeProfit2.Should().BeLessThan(signal.TakeProfit1);
        signal.TakeProfit3.Should().BeLessThan(signal.TakeProfit2);
    }

    [Fact]
    public async Task AnalyzeAsync_LongSignal_CalculatesCorrectRiskRewardRatios()
    {
        // Arrange
        var strategy = new EmaMomentumScalperStrategy(
            _context,
            _indicatorService,
            _marketAnalysisService,
            _logger);

        var symbol = "BTCUSDT";
        var currentPrice = 50000m;

        _marketAnalysisService.CheckTrendAlignmentAsync(symbol, TradeSide.Long, Arg.Any<CancellationToken>())
            .Returns(true);
        _marketAnalysisService.CheckTrendAlignmentAsync(symbol, TradeSide.Short, Arg.Any<CancellationToken>())
            .Returns(false);

        var candles = GenerateCandles(symbol, "5m", 100, currentPrice);
        await _context.Candles.AddRangeAsync(candles);
        await _context.SaveChangesAsync();

        // Setup bullish crossover
        _indicatorService.CalculateEMA(Arg.Is<List<Candle>>(c => c.Count == 100), 9).Returns(50100m);
        _indicatorService.CalculateEMA(Arg.Is<List<Candle>>(c => c.Count == 100), 21).Returns(49900m);
        _indicatorService.CalculateEMA(Arg.Is<List<Candle>>(c => c.Count == 99), 9).Returns(49900m);
        _indicatorService.CalculateEMA(Arg.Is<List<Candle>>(c => c.Count == 99), 21).Returns(50000m);

        _indicatorService.CalculateRSI(Arg.Any<List<Candle>>(), 14).Returns(65m);
        _indicatorService.CalculateMACD(Arg.Any<List<Candle>>()).Returns((150m, 100m, 50m));
        _indicatorService.CalculateBollingerBands(Arg.Any<List<Candle>>(), 20, 2).Returns((51000m, 50000m, 49000m));
        _indicatorService.CalculateAverageVolume(Arg.Any<List<Candle>>(), 20).Returns(1000000m);
        _indicatorService.GetSwingHigh(Arg.Any<List<Candle>>(), 10).Returns(49800m);
        _indicatorService.GetSwingLow(Arg.Any<List<Candle>>(), 10).Returns(49000m);

        // Act
        var signal = await strategy.AnalyzeAsync(symbol);

        // Assert
        signal.EntryPrice.Should().NotBeNull();
        signal.StopLoss.Should().NotBeNull();
        signal.TakeProfit1.Should().NotBeNull();
        signal.TakeProfit2.Should().NotBeNull();
        signal.TakeProfit3.Should().NotBeNull();

        var riskAmount = signal.EntryPrice!.Value - signal.StopLoss!.Value;
        var tp1Profit = signal.TakeProfit1!.Value - signal.EntryPrice.Value;
        var tp2Profit = signal.TakeProfit2!.Value - signal.EntryPrice.Value;
        var tp3Profit = signal.TakeProfit3!.Value - signal.EntryPrice.Value;

        // Verify risk:reward ratios (approximately 1.5R, 2.5R, 4.0R)
        (tp1Profit / riskAmount).Should().BeApproximately(1.5m, 0.1m);
        (tp2Profit / riskAmount).Should().BeApproximately(2.5m, 0.1m);
        (tp3Profit / riskAmount).Should().BeApproximately(4.0m, 0.1m);
    }

    [Fact]
    public async Task AnalyzeAsync_ShortSignal_CalculatesCorrectRiskRewardRatios()
    {
        // Arrange
        var strategy = new EmaMomentumScalperStrategy(
            _context,
            _indicatorService,
            _marketAnalysisService,
            _logger);

        var symbol = "BTCUSDT";
        var currentPrice = 50000m;

        _marketAnalysisService.CheckTrendAlignmentAsync(symbol, TradeSide.Long, Arg.Any<CancellationToken>())
            .Returns(false);
        _marketAnalysisService.CheckTrendAlignmentAsync(symbol, TradeSide.Short, Arg.Any<CancellationToken>())
            .Returns(true);

        var candles = GenerateCandles(symbol, "5m", 100, currentPrice);
        await _context.Candles.AddRangeAsync(candles);
        await _context.SaveChangesAsync();

        // Setup bearish crossover
        _indicatorService.CalculateEMA(Arg.Is<List<Candle>>(c => c.Count == 100), 9).Returns(49900m);
        _indicatorService.CalculateEMA(Arg.Is<List<Candle>>(c => c.Count == 100), 21).Returns(50100m);
        _indicatorService.CalculateEMA(Arg.Is<List<Candle>>(c => c.Count == 99), 9).Returns(50100m);
        _indicatorService.CalculateEMA(Arg.Is<List<Candle>>(c => c.Count == 99), 21).Returns(50000m);

        _indicatorService.CalculateRSI(Arg.Any<List<Candle>>(), 14).Returns(35m);
        _indicatorService.CalculateMACD(Arg.Any<List<Candle>>()).Returns((-150m, -100m, -50m));
        _indicatorService.CalculateBollingerBands(Arg.Any<List<Candle>>(), 20, 2).Returns((51000m, 50000m, 49000m));
        _indicatorService.CalculateAverageVolume(Arg.Any<List<Candle>>(), 20).Returns(1000000m);
        _indicatorService.GetSwingHigh(Arg.Any<List<Candle>>(), 10).Returns(51000m);
        _indicatorService.GetSwingLow(Arg.Any<List<Candle>>(), 10).Returns(50200m);

        // Act
        var signal = await strategy.AnalyzeAsync(symbol);

        // Assert
        signal.EntryPrice.Should().NotBeNull();
        signal.StopLoss.Should().NotBeNull();
        signal.TakeProfit1.Should().NotBeNull();
        signal.TakeProfit2.Should().NotBeNull();
        signal.TakeProfit3.Should().NotBeNull();

        var riskAmount = signal.StopLoss!.Value - signal.EntryPrice!.Value;
        var tp1Profit = signal.EntryPrice.Value - signal.TakeProfit1!.Value;
        var tp2Profit = signal.EntryPrice.Value - signal.TakeProfit2!.Value;
        var tp3Profit = signal.EntryPrice.Value - signal.TakeProfit3!.Value;

        // Verify risk:reward ratios (approximately 1.5R, 2.5R, 4.0R)
        (tp1Profit / riskAmount).Should().BeApproximately(1.5m, 0.1m);
        (tp2Profit / riskAmount).Should().BeApproximately(2.5m, 0.1m);
        (tp3Profit / riskAmount).Should().BeApproximately(4.0m, 0.1m);
    }

    [Fact]
    public async Task AnalyzeAsync_WithException_ReturnsHoldSignalWithError()
    {
        // Arrange
        var strategy = new EmaMomentumScalperStrategy(
            _context,
            _indicatorService,
            _marketAnalysisService,
            _logger);

        var symbol = "BTCUSDT";

        _marketAnalysisService.CheckTrendAlignmentAsync(symbol, Arg.Any<TradeSide>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("Test exception"));

        // Act
        var signal = await strategy.AnalyzeAsync(symbol);

        // Assert
        signal.Type.Should().Be(SignalType.Hold);
        signal.Confidence.Should().Be(0);
        signal.Reason.Should().Contain("Error");
    }

    private List<Candle> GenerateCandles(string symbol, string interval, int count, decimal basePrice)
    {
        var candles = new List<Candle>();
        var startTime = DateTime.UtcNow.AddMinutes(-count * 5);

        for (int i = 0; i < count; i++)
        {
            var price = basePrice + (i % 10 - 5) * 10; // Some price variation
            candles.Add(new Candle
            {
                Symbol = symbol,
                Interval = interval,
                OpenTime = startTime.AddMinutes(i * 5),
                CloseTime = startTime.AddMinutes((i + 1) * 5),
                OpenPrice = price,
                HighPrice = price + 50,
                LowPrice = price - 50,
                ClosePrice = price + (i % 2 == 0 ? 10 : -10),
                Volume = 1000000 + (i * 10000),
                QuoteVolume = (1000000 + (i * 10000)) * price,
                NumberOfTrades = 1000 + i
            });
        }

        return candles;
    }
}
