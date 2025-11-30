var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

var postgres = builder
    .AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin();;
var postgresdb = postgres.AddDatabase("tradingbotdb");

var apiService = builder.AddProject<Projects.TradingBot_ApiService>("apiservice")
    .WithReference(postgresdb)
    .WithHttpHealthCheck("/health");

builder.Build().Run();