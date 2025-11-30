using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text.Json;

namespace TradingBot.ApiService.BuildingBlocks.Pubsub.Dapr;

public class PubSubSubscription
{
    public required string Topic { get; init; }
    public required string Route { get; init; }
    public required Type EventType { get; init; }
}

public class PubSubRegistry
{
    public string Name => "pubsub";

    private readonly List<PubSubSubscription> _subscriptions = new();

    public void Add<TEvent>() where TEvent : IntegrationEvent
    {
        _subscriptions.Add(new PubSubSubscription
        {
            Topic = typeof(TEvent).Name.ToLower(),
            Route = $"subscribe/{typeof(TEvent).Name.ToLower()}",
            EventType = typeof(TEvent),
        });
    }

    public IReadOnlyList<PubSubSubscription> List() => _subscriptions;
}

public static class WebApplicationExtensions
{
    public static void MapPubSub(this WebApplication app)
    {
        var registry = app.Services.GetRequiredService<PubSubRegistry>();

        app.MapGet("/dapr/subscribe", () =>
            registry.List().Select(s => new
            {
                pubsubname = registry.Name,
                topic = s.Topic,
                route = s.Route
            })
        );

        foreach (var sub in registry.List())
        {
            app.MapPost(sub.Route, async(HttpContext context, [FromServices] IMediator mediator, [FromServices] ILoggerFactory loggerFactory) =>
            {
                var logger = loggerFactory.CreateLogger(sub.EventType);

                logger.BeginScope(new Dictionary<string, object>
                {
                    ["EventType"] = sub.EventType.Name,
                });

                using var doc = await JsonDocument.ParseAsync(context.Request.Body);
                var json = doc.RootElement;

                if (json.Deserialize(sub.EventType) is not IntegrationEvent message)
                {
                    logger.LogInformation("Received null or invalid message for event type {EventType}", sub.EventType.Name);
                    return Results.BadRequest();
                }

                logger.LogInformation("Handling message {@Message}", message);
                await mediator.Publish(message);
                logger.LogInformation("Handled message successfully");

                return Results.Ok();
            });
        }
    }
}