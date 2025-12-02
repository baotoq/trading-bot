using MediatR;
using TradingBot.ApiService;
using Serilog;
using Serilog.Events;
using Serilog.Templates;
using Serilog.Templates.Themes;
using TradingBot.ApiService.Application;
using TradingBot.ApiService.BuildingBlocks;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Dapr;
using TradingBot.ApiService.Infrastructure;
using TradingBot.ServiceDefaults;

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
    builder.AddPubSubServices();
    builder.AddPersistentServices();

    var app = builder.Build();

    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.MapGet("/", () => "Trading Bot API service is running...");
    app.MapPubSub();

    app.MapDefaultEndpoints();

    app.MapGet("/backtest-ema-crossover", async (IMediator mediator) =>
    {
        var result = await mediator.Send(new RunEmaCrossoverStrategyCommand());
        return Results.Ok(result);
    });

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
