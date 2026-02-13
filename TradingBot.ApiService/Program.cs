using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Templates;
using Serilog.Templates.Themes;
using TradingBot.ApiService.Application.BackgroundJobs;
using TradingBot.ApiService.Application.Health;
using TradingBot.ApiService.Application.Services;
using TradingBot.ApiService.Application.Services.HistoricalData;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Dapr;
using TradingBot.ApiService.Configuration;
using TradingBot.ApiService.Endpoints;
using TradingBot.ApiService.Infrastructure.CoinGecko;
using TradingBot.ApiService.Infrastructure.Data;
using TradingBot.ApiService.Infrastructure.Hyperliquid;
using TradingBot.ApiService.Infrastructure.Locking;
using TradingBot.ApiService.Infrastructure.Telegram;
using TradingBot.ServiceDefaults;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

AppContext.SetSwitch("System.Globalization.EnforceGenericTimeZone", true);
TimeZoneInfo.ClearCachedData();
Environment.SetEnvironmentVariable("TZ", "UTC");

try
{
    Log.Information("Starting application");

    var builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSerilog((services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(new ExpressionTemplate(
            // Include trace and span ids when present.
            "[{@t:HH:mm:ss} {@l:u3}{#if @tr is not null} ({substring(@tr,0,4)}:{substring(@sp,0,4)}){#end}] {@m}\n{@x}",
            theme: TemplateTheme.Code)));

    builder.Services.AddSerilog();
    builder.Services.AddProblemDetails();
    builder.Services.AddOpenApi();
    builder.AddServiceDefaults();

    // Add CORS
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowAll", policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
    });

    // Configuration binding with validation
    builder.Services.AddSingleton<IValidateOptions<DcaOptions>, DcaOptionsValidator>();
    builder.Services.AddOptions<DcaOptions>()
        .Bind(builder.Configuration.GetSection("DcaOptions"))
        .ValidateOnStart();
    builder.Services.AddOptions<HyperliquidOptions>()
        .Bind(builder.Configuration.GetSection("Hyperliquid"));

    // EF Core DbContext via Aspire
    builder.AddNpgsqlDbContext<TradingBotDbContext>("tradingbotdb");

    // PostgreSQL distributed lock
    builder.Services.AddPostgresDistributedLock();

    // Hyperliquid API client with EIP-712 signing
    builder.Services.AddHyperliquid(builder.Configuration);

    // Telegram notifications with MediatR
    builder.Services.AddTelegram(builder.Configuration);

    // DCA execution service (scoped — uses DbContext)
    builder.Services.AddScoped<IDcaExecutionService, DcaExecutionService>();

    // Price data service (scoped — uses DbContext)
    builder.Services.AddScoped<IPriceDataService, PriceDataService>();

    // DCA scheduler (runs daily at configured time)
    builder.Services.AddHostedService<DcaSchedulerBackgroundService>();

    // Price data refresh (runs daily at 00:05 UTC, bootstraps on startup)
    builder.Services.AddHostedService<PriceDataRefreshService>();

    // Weekly summary (runs Sunday 20:00-21:00 UTC)
    builder.Services.AddHostedService<WeeklySummaryService>();

    // Missed purchase verification (runs every 30 minutes)
    builder.Services.AddHostedService<MissedPurchaseVerificationService>();

    // CoinGecko API client for historical price data
    builder.Services.AddCoinGecko(builder.Configuration);

    // Historical data pipeline
    builder.Services.AddSingleton<IngestionJobQueue>();
    builder.Services.AddScoped<GapDetectionService>();
    builder.Services.AddScoped<DataIngestionService>();
    builder.Services.AddHostedService<DataIngestionBackgroundService>();

    // Health checks
    builder.Services.AddHealthChecks()
        .AddCheck<DcaHealthCheck>("dca-service");

    builder.AddRedisDistributedCache("redis");

    var app = builder.Build();

    // Run EF Core migrations
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<TradingBotDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();

    // Enable CORS
    app.UseCors("AllowAll");

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.MapGet("/", () => "Trading Bot API service is running...");
    app.MapPubSub();

    app.MapDefaultEndpoints();
    app.MapDataEndpoints();
    app.MapBacktestEndpoints();

    await app.RunAsync();

    Log.Information("Stopped cleanly");

    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "An unhandled exception occurred during bootstrapping");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}
