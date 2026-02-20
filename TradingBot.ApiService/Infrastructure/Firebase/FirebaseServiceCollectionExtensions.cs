using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;

namespace TradingBot.ApiService.Infrastructure.Firebase;

public static class FirebaseServiceCollectionExtensions
{
    public static IServiceCollection AddFirebase(this IServiceCollection services, IConfiguration configuration)
    {
        var json = configuration["Firebase:ServiceAccountKeyJson"];
        if (!string.IsNullOrEmpty(json))
        {
            FirebaseApp.Create(new AppOptions
            {
                Credential = GoogleCredential.FromJson(json)
            });
        }
        else
        {
            // Allow app to start without Firebase (dev mode)
            // FcmNotificationService will gracefully no-op
        }

        services.AddScoped<FcmNotificationService>();
        return services;
    }
}
