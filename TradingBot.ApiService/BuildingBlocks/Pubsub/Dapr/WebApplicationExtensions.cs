using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Text.Json;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Abstraction;
using TradingBot.ApiService.BuildingBlocks.Pubsub.Outbox.Abstraction;

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

public record DaprEvent
{
    public string Data { get; init; }
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
            app.MapPost(sub.Route, async (HttpContext context,
                [FromServices] IMediator mediator,
                [FromServices] JsonSerializerOptions jsonOptions,
                [FromServices] ILoggerFactory loggerFactory) =>
            {
                var logger = loggerFactory.CreateLogger(sub.EventType);

                logger.BeginScope(new Dictionary<string, object>
                {
                    ["EventType"] = sub.EventType.Name,
                });

                using var doc = await JsonDocument.ParseAsync(context.Request.Body);

                var daprEvent = doc.RootElement.Deserialize<DaprEvent>(jsonOptions);
                if (daprEvent == null)
                {
                    logger.LogInformation("Received null Dapr event");
                    return Results.BadRequest();
                }

                try
                {
                    if (JsonSerializer.Deserialize(daprEvent.Data, sub.EventType, jsonOptions) is not IntegrationEvent message)
                    {
                        logger.LogInformation("Received null or invalid message for event type {EventType}", sub.EventType.Name);
                        return Results.BadRequest();
                    }

                    logger.LogInformation("Handling message {@Message}", message);
                    await mediator.Publish(message);
                    logger.LogInformation("Handled message successfully");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error handling message for event type {EventType}", sub.EventType.Name);
                    return Results.Ok();
                }

                return Results.Ok();
            });
        }
    }
}