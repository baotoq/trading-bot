using Microsoft.EntityFrameworkCore;
using TradingBot.ApiService.Infrastructure.Data;
using TradingBot.ApiService.Models;
using TradingBot.ApiService.Models.Ids;

namespace TradingBot.ApiService.Endpoints;

public static class DeviceEndpoints
{
    public static WebApplication MapDeviceEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/devices")
            .AddEndpointFilter<ApiKeyEndpointFilter>();

        group.MapPost("/register", RegisterDeviceAsync);
        group.MapDelete("/{token}", UnregisterDeviceAsync);

        return app;
    }

    private static async Task<IResult> RegisterDeviceAsync(
        RegisterDeviceRequest request,
        TradingBotDbContext dbContext,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var existing = await dbContext.DeviceTokens
            .FirstOrDefaultAsync(d => d.Token == request.Token, ct);

        if (existing is not null)
        {
            existing.Platform = request.Platform;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            logger.LogInformation("Updated existing device token for platform {Platform}", request.Platform);
        }
        else
        {
            var deviceToken = new DeviceToken
            {
                Id = DeviceTokenId.New(),
                Token = request.Token,
                Platform = request.Platform
            };
            dbContext.DeviceTokens.Add(deviceToken);
            logger.LogInformation("Registered new device token for platform {Platform}", request.Platform);
        }

        await dbContext.SaveChangesAsync(ct);
        return Results.Ok();
    }

    private static async Task<IResult> UnregisterDeviceAsync(
        string token,
        TradingBotDbContext dbContext,
        ILogger<Program> logger,
        CancellationToken ct)
    {
        var existing = await dbContext.DeviceTokens
            .FirstOrDefaultAsync(d => d.Token == token, ct);

        if (existing is not null)
        {
            dbContext.DeviceTokens.Remove(existing);
            await dbContext.SaveChangesAsync(ct);
            logger.LogInformation("Unregistered device token");
        }

        return Results.NoContent();
    }
}

public record RegisterDeviceRequest(string Token, string Platform);
