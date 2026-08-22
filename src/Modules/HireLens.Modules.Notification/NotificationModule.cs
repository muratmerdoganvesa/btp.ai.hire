using HireLens.Contracts.Notifications;
using HireLens.Infrastructure.Hosting;
using HireLens.Modules.Notification.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace HireLens.Modules.Notification;

public static class NotificationModule
{
    public static IServiceCollection AddNotificationModule(this IServiceCollection services)
    {
        services.AddScoped<NotificationService>();
        services.AddScoped<INotificationService>(sp => sp.GetRequiredService<NotificationService>());
        services.AddScoped<INotificationSink>(sp => sp.GetRequiredService<NotificationService>());
        return services;
    }

    public static IEndpointRouteBuilder MapNotificationModule(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/notifications", async (INotificationService notifications, CancellationToken ct) =>
            HttpResults.From(await notifications.ListAsync(ct)))
            .WithTags("Notifications")
            .RequireAuthorization();

        endpoints.MapPost("/api/notifications/reminders", async (INotificationService notifications, CancellationToken ct) =>
        {
            await notifications.RemindRecruitersAsync(ct);
            return Results.Accepted();
        }).WithTags("Notifications").RequireAuthorization();

        return endpoints;
    }
}
