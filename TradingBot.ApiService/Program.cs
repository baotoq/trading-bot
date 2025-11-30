using TradingBot.ApiService;
using TradingBot.ApiService.Endpoints;
using Serilog;
using Serilog.Events;
using Serilog.Templates;
using Serilog.Templates.Themes;
using TradingBot.ApiService.Persistent;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

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

    builder.AddApplicationServices();
    builder.AddPersistentServices();

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.MapGet("/", () => "Trading Bot API service is running. Visit /binance, /trading, or /realtime endpoints.");
    app.MapDefaultEndpoints();

    app.MapBinanceEndpoints();
    app.MapTradingEndpoints();
    app.MapRealTimeTradingEndpoints();

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
