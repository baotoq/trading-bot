using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Serilog;
using Serilog.Templates;
using Serilog.Templates.Themes;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Aspire apphost");

    var builder = DistributedApplication.CreateBuilder(args);

    builder.Services.AddSerilog((services, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(new ExpressionTemplate(
            // Include trace and span ids when present.
            "[{@t:HH:mm:ss} {@l:u3}{#if @tr is not null} ({substring(@tr,0,4)}:{substring(@sp,0,4)}){#end}] {@m}\n{@x}",
            theme: TemplateTheme.Code)));

    var postgres = builder
        .AddPostgres("postgres")
        .WithLifetime(ContainerLifetime.Persistent)
        .WithDataVolume()
        .WithPgAdmin(c => c.WithLifetime(ContainerLifetime.Persistent).WithHostPort(5050));

    var postgresdb = postgres.AddDatabase("tradingbotdb");

    var redis = builder.AddRedis("redis")
        .WithLifetime(ContainerLifetime.Persistent)
        .WithDataVolume()
        .WithRedisInsight(c => c.WithLifetime(ContainerLifetime.Persistent).WithHostPort(5051));

    var redisHost = redis.Resource.PrimaryEndpoint.Property(EndpointProperty.Host);
    var redisPort = redis.Resource.PrimaryEndpoint.Property(EndpointProperty.Port);

    var pubSub = builder
        .AddDaprPubSub("pubsub")
        .WithMetadata("redisHost", ReferenceExpression.Create($"{redisHost}:{redisPort}"))
        .WaitFor(redis);
    if (redis.Resource.PasswordParameter is not null)
    {
        pubSub.WithMetadata("redisPassword", redis.Resource.PasswordParameter);
    }

    var dashboardApiKey = builder.AddParameter("dashboardApiKey", secret: true);

    var apiService = builder.AddProject<Projects.TradingBot_ApiService>("apiservice")
        .WithReference(postgresdb)
        .WithReference(redis)
        .WithDaprSidecar(sidecar =>
        {
            sidecar.WithReference(pubSub);
        })
        .WithHttpHealthCheck("/health")
        .WithEnvironment("Dashboard__ApiKey", dashboardApiKey);

    builder.Build().Run();

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


