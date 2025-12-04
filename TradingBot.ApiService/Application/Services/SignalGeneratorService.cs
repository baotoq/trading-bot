using MediatR;
using TradingBot.ApiService.Application.Signals.DomainEvents;
using TradingBot.ApiService.Application.Strategies;
using TradingBot.ApiService.Domain;

namespace TradingBot.ApiService.Application.Services;

public interface ISignalGeneratorService
{
    Task GenerateSignalAsync(Symbol symbol, CancellationToken cancellationToken = default);
    Task EnableSignalNotificationsAsync(Symbol symbol, string strategy);
    Task DisableSignalNotificationsAsync(Symbol symbol);
    bool IsNotificationEnabled(Symbol symbol);
    IReadOnlyDictionary<Symbol, string> GetEnabledNotifications();
}

public class SignalGeneratorService : ISignalGeneratorService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SignalGeneratorService> _logger;
    private readonly Dictionary<Symbol, string> _enabledNotifications = new(); // symbol -> strategy name
    private readonly Dictionary<Symbol, DateTime> _lastSignalTime = new(); // symbol -> last signal time
    private readonly Dictionary<Symbol, SignalType> _lastSignalType = new(); // symbol -> last signal type
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly TimeSpan _cooldownPeriod = TimeSpan.FromMinutes(5); // Don't spam same signal within 5 minutes

    public SignalGeneratorService(
        IServiceProvider serviceProvider,
        ILogger<SignalGeneratorService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task GenerateSignalAsync(Symbol symbol, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            // Check if notifications are enabled for this symbol
            if (!_enabledNotifications.TryGetValue(symbol, out var strategyName))
            {
                _logger.LogDebug("Signal notifications not enabled for {Symbol}", symbol);
                return;
            }

            _logger.LogInformation("Generating signal for {Symbol} using {Strategy}", symbol, strategyName);

            // Create a scope to get scoped services
            await using var scope = _serviceProvider.CreateAsyncScope();

            // Get the strategy based on the strategy name
            IStrategy? strategy = strategyName.ToLower() switch
            {
                "emamomentumsca lper" or "ema" => scope.ServiceProvider.GetRequiredService<EmaMomentumScalperStrategy>(),
                _ => scope.ServiceProvider.GetRequiredService<EmaMomentumScalperStrategy>() // Default strategy
            };

            if (strategy == null)
            {
                _logger.LogWarning("Strategy {Strategy} not found for {Symbol}", strategyName, symbol);
                return;
            }

            // Analyze and generate signal
            var signal = await strategy.AnalyzeAsync(symbol, cancellationToken);

            // Check if signal is actionable (not HOLD)
            if (signal.Type == SignalType.Hold)
            {
                _logger.LogInformation("Signal for {Symbol} is HOLD, not sending notification", symbol);
                return;
            }

            // Check cooldown period to avoid spamming same signals
            if (ShouldSkipSignal(symbol, signal))
            {
                _logger.LogDebug("Skipping duplicate signal for {Symbol} (cooldown period)", symbol);
                return;
            }

            // Update last signal tracking
            _lastSignalTime[symbol] = DateTime.UtcNow;
            _lastSignalType[symbol] = signal.Type;

            _logger.LogInformation(
                "Signal generated for {Symbol}: {Type} (Confidence: {Confidence}%)",
                symbol, signal.Type, signal.Confidence * 100);

            // Publish domain event (handlers will take care of notifications)
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.Publish(new TradingSignalGeneratedDomainEvent(signal), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating signal for {Symbol}", symbol);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public Task EnableSignalNotificationsAsync(Symbol symbol, string strategy)
    {
        _enabledNotifications[symbol] = strategy;
        _logger.LogInformation("Enabled signal notifications for {Symbol} with strategy {Strategy}", symbol, strategy);
        return Task.CompletedTask;
    }

    public Task DisableSignalNotificationsAsync(Symbol symbol)
    {
        if (_enabledNotifications.Remove(symbol))
        {
            _lastSignalTime.Remove(symbol);
            _lastSignalType.Remove(symbol);
            _logger.LogInformation("Disabled signal notifications for {Symbol}", symbol);
        }
        return Task.CompletedTask;
    }

    public bool IsNotificationEnabled(Symbol symbol)
    {
        return _enabledNotifications.ContainsKey(symbol);
    }

    public IReadOnlyDictionary<Symbol, string> GetEnabledNotifications()
    {
        return _enabledNotifications;
    }

    private bool ShouldSkipSignal(Symbol symbol, TradingSignal signal)
    {
        // Check if we've sent a signal for this symbol recently
        if (!_lastSignalTime.TryGetValue(symbol, out var lastTime))
        {
            return false; // No previous signal, don't skip
        }

        // Check if we're still in cooldown period
        if (DateTime.UtcNow - lastTime < _cooldownPeriod)
        {
            // Only skip if it's the same signal type
            if (_lastSignalType.TryGetValue(symbol, out var lastType) && lastType == signal.Type)
            {
                return true;
            }
        }

        return false;
    }
}
