using Binance.Net.Enums;
using MediatR;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Application.Strategies;
using TradingBot.ApiService.Domain;
using TradingBot.ApiService.Infrastructure;

namespace TradingBot.ApiService.Application.Commands;

public record ExecuteTradeCommand(
    string Symbol,
    decimal AccountEquity,
    decimal RiskPercent = 2.5m) : IRequest<ExecuteTradeResult>;

public class ExecuteTradeResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? PositionId { get; set; }
    public string? SignalType { get; set; }
    public decimal? EntryPrice { get; set; }
    public decimal? StopLoss { get; set; }
    public decimal? TakeProfit1 { get; set; }
    public decimal? Confidence { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class ExecuteTradeCommandHandler : IRequestHandler<ExecuteTradeCommand, ExecuteTradeResult>
{
    private readonly IMarketAnalysisService _marketAnalysisService;
    private readonly IStrategy _strategy;
    private readonly IPositionCalculatorService _positionCalculatorService;
    private readonly IRiskManagementService _riskManagementService;
    private readonly IBinanceService _binanceService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ExecuteTradeCommandHandler> _logger;

    public ExecuteTradeCommandHandler(
        IMarketAnalysisService marketAnalysisService,
        EmaMomentumScalperStrategy strategy,
        IPositionCalculatorService positionCalculatorService,
        IRiskManagementService riskManagementService,
        IBinanceService binanceService,
        ApplicationDbContext context,
        ILogger<ExecuteTradeCommandHandler> logger)
    {
        _marketAnalysisService = marketAnalysisService;
        _strategy = strategy;
        _positionCalculatorService = positionCalculatorService;
        _riskManagementService = riskManagementService;
        _binanceService = binanceService;
        _context = context;
        _logger = logger;
    }

    public async Task<ExecuteTradeResult> Handle(ExecuteTradeCommand request, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Executing trade for {Symbol} with equity={Equity} and risk={Risk}%",
            request.Symbol, request.AccountEquity, request.RiskPercent);

        var result = new ExecuteTradeResult();

        try
        {
            // Validate risk percent (2% to 4% max)
            if (request.RiskPercent < 2m || request.RiskPercent > 4m)
            {
                result.Success = false;
                result.Message = $"Risk percent must be between 2% and 4%. Got: {request.RiskPercent}%";
                return result;
            }

            // PHASE 1: Pre-Market Analysis
            _logger.LogInformation("Phase 1: Analyzing market conditions");
            var marketCondition = await _marketAnalysisService.AnalyzeMarketConditionAsync(
                request.Symbol, cancellationToken);

            if (!marketCondition.CanTrade)
            {
                result.Success = false;
                result.Message = $"Market conditions unfavorable: {marketCondition.Reason}";
                return result;
            }

            _logger.LogInformation("Market condition: {Condition}", marketCondition.Reason);

            // PHASE 2: Signal Generation
            _logger.LogInformation("Phase 2: Generating trading signal");
            var signal = await _strategy.AnalyzeAsync(request.Symbol, cancellationToken);

            if (signal.Type == SignalType.Hold)
            {
                result.Success = false;
                result.Message = $"No trading signal: {signal.Reason}";
                return result;
            }

            _logger.LogInformation(
                "Signal detected: {Type} with {Confidence}% confidence - {Reason}",
                signal.Type, signal.Confidence * 100, signal.Reason);

            result.SignalType = signal.Type.ToString();
            result.Confidence = signal.Confidence;

            // PHASE 3: Position Calculation
            _logger.LogInformation("Phase 3: Calculating position parameters");
            var parameters = await _positionCalculatorService.CalculatePositionParametersAsync(
                signal, request.AccountEquity, request.RiskPercent, cancellationToken);

            if (!parameters.IsValid)
            {
                result.Success = false;
                result.Message = $"Invalid position parameters: {parameters.ValidationError}";
                return result;
            }

            result.EntryPrice = parameters.EntryPrice;
            result.StopLoss = parameters.StopLoss;
            result.TakeProfit1 = parameters.TakeProfit1;

            _logger.LogInformation(
                "Position calculated: Entry=${Entry}, SL=${SL}, TP1=${TP1}, Size={Size}, Leverage={Leverage}x",
                parameters.EntryPrice, parameters.StopLoss, parameters.TakeProfit1,
                parameters.PositionSize, parameters.RecommendedLeverage);

            // PHASE 4: Risk Management Validation
            _logger.LogInformation("Phase 4: Validating risk management rules");
            var riskCheck = await _riskManagementService.ValidateTradeAsync(
                signal, parameters, cancellationToken);

            if (!riskCheck.IsApproved)
            {
                result.Success = false;
                result.Message = $"Risk check failed: {riskCheck.RejectionReason}";
                result.Warnings = riskCheck.Violations;
                return result;
            }

            if (riskCheck.Warnings.Any())
            {
                result.Warnings = riskCheck.Warnings;
                _logger.LogWarning("Risk warnings: {Warnings}", string.Join(", ", riskCheck.Warnings));
            }

            // Check if can open new position
            if (!await _riskManagementService.CanOpenNewPositionAsync(request.Symbol, cancellationToken))
            {
                result.Success = false;
                result.Message = "Cannot open new position - existing position or max positions reached";
                return result;
            }

            // PHASE 5: Order Execution
            _logger.LogInformation("Phase 5: Executing orders on Binance");

            var isLong = signal.Type == SignalType.Buy || signal.Type == SignalType.StrongBuy;
            var side = isLong ? OrderSide.Buy : OrderSide.Sell;

            // Step 1: Set leverage
            var leverageSet = await _binanceService.SetLeverageAsync(
                request.Symbol, parameters.RecommendedLeverage, cancellationToken);

            if (!leverageSet)
            {
                _logger.LogWarning("Failed to set leverage, continuing with existing leverage");
            }

            // Step 2: Place Stop-Loss order first
            var stopLossSide = isLong ? OrderSide.Sell : OrderSide.Buy;
            var stopLossOrder = await _binanceService.PlaceFuturesOrderAsync(
                symbol: request.Symbol,
                side: stopLossSide,
                orderType: FuturesOrderType.StopMarket,
                quantity: parameters.Quantity,
                stopPrice: parameters.StopLoss,
                cancellationToken: cancellationToken);

            if (!stopLossOrder.IsSuccess)
            {
                result.Success = false;
                result.Message = $"Failed to place stop-loss: {stopLossOrder.ErrorMessage}";
                return result;
            }

            _logger.LogInformation("Stop-loss order placed: OrderId={OrderId}", stopLossOrder.OrderId);

            // Step 3: Place Entry order (Market order for immediate execution)
            var entryOrder = await _binanceService.PlaceFuturesOrderAsync(
                symbol: request.Symbol,
                side: side,
                orderType: FuturesOrderType.Market,
                quantity: parameters.Quantity,
                cancellationToken: cancellationToken);

            if (!entryOrder.IsSuccess)
            {
                // Cancel stop-loss if entry fails
                await _binanceService.CancelFuturesOrderAsync(
                    request.Symbol, stopLossOrder.OrderId, cancellationToken);

                result.Success = false;
                result.Message = $"Failed to place entry order: {entryOrder.ErrorMessage}";
                return result;
            }

            _logger.LogInformation("Entry order filled: OrderId={OrderId}, Price=${Price}",
                entryOrder.OrderId, entryOrder.Price);

            // Step 4: Place Take-Profit orders
            var takeProfitSide = isLong ? OrderSide.Sell : OrderSide.Buy;

            var tp1Order = await _binanceService.PlaceFuturesOrderAsync(
                symbol: request.Symbol,
                side: takeProfitSide,
                orderType: FuturesOrderType.Limit,
                quantity: parameters.Quantity * 0.5m, // 50% of position
                price: parameters.TakeProfit1,
                timeInForce: TimeInForce.GoodTillCanceled,
                cancellationToken: cancellationToken);

            var tp2Order = await _binanceService.PlaceFuturesOrderAsync(
                symbol: request.Symbol,
                side: takeProfitSide,
                orderType: FuturesOrderType.Limit,
                quantity: parameters.Quantity * 0.3m, // 30% of position
                price: parameters.TakeProfit2,
                timeInForce: TimeInForce.GoodTillCanceled,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Take-profit orders placed: TP1={TP1OrderId}, TP2={TP2OrderId}",
                tp1Order.OrderId, tp2Order.OrderId);

            // PHASE 6: Save Position to Database
            _logger.LogInformation("Phase 6: Saving position to database");

            var position = new Position
            {
                Id = Guid.NewGuid(),
                Symbol = request.Symbol,
                Side = isLong ? TradeSide.Long : TradeSide.Short,
                EntryPrice = entryOrder.Price ?? parameters.EntryPrice,
                Quantity = parameters.Quantity,
                StopLoss = parameters.StopLoss,
                TakeProfit1 = parameters.TakeProfit1,
                TakeProfit2 = parameters.TakeProfit2,
                TakeProfit3 = parameters.TakeProfit3,
                RiskAmount = parameters.RiskAmount,
                Leverage = parameters.RecommendedLeverage,
                Status = PositionStatus.Open,
                EntryOrderId = entryOrder.OrderId,
                StopLossOrderId = stopLossOrder.OrderId,
                TakeProfit1OrderId = tp1Order.IsSuccess ? tp1Order.OrderId : null,
                TakeProfit2OrderId = tp2Order.IsSuccess ? tp2Order.OrderId : null,
                RemainingQuantity = parameters.Quantity,
                RealizedPnL = 0,
                UnrealizedPnL = 0,
                IsBreakEven = false,
                EntryTime = DateTime.UtcNow,
                Strategy = signal.Strategy,
                SignalReason = signal.Reason
            };

            _context.Set<Position>().Add(position);

            // Save trade log
            var tradeLog = new TradeLog
            {
                Id = Guid.NewGuid(),
                PositionId = position.Id,
                Symbol = request.Symbol,
                Side = position.Side,
                EntryTime = position.EntryTime ?? DateTime.UtcNow,
                EntryPrice = position.EntryPrice,
                Quantity = position.Quantity,
                StopLoss = position.StopLoss,
                TakeProfit1 = position.TakeProfit1,
                TakeProfit2 = position.TakeProfit2,
                TakeProfit3 = position.TakeProfit3,
                AtrAtEntry = marketCondition.Atr,
                FundingRateAtEntry = marketCondition.FundingRate,
                VolumeAtEntry = signal.Indicators.GetValueOrDefault("Volume", 0),
                RsiAtEntry = signal.Indicators.GetValueOrDefault("RSI", 0),
                MacdAtEntry = signal.Indicators.GetValueOrDefault("MACD", 0),
                Strategy = signal.Strategy,
                SignalReason = signal.Reason,
                Indicators = signal.Indicators,
                Position = position
            };

            _context.Set<TradeLog>().Add(tradeLog);

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Position saved successfully: PositionId={PositionId}",
                position.Id);

            // Success result
            result.Success = true;
            result.Message = $"Trade executed successfully: {signal.Type} @ ${position.EntryPrice}";
            result.PositionId = position.Id;

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing trade for {Symbol}", request.Symbol);
            result.Success = false;
            result.Message = $"Execution error: {ex.Message}";
            return result;
        }
    }
}
