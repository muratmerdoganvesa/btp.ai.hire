using HireLens.Contracts.Metering;
using HireLens.Infrastructure.Hosting;
using HireLens.Modules.Metering.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace HireLens.Modules.Metering;

public static class MeteringModule
{
    public static IServiceCollection AddMeteringModule(this IServiceCollection services)
    {
        services.AddScoped<MeteringService>();
        services.AddScoped<IMeteringService>(sp => sp.GetRequiredService<MeteringService>());
        services.AddScoped<IQuotaGuard>(sp => sp.GetRequiredService<MeteringService>());
        services.AddScoped<IQuotaBootstrap>(sp => sp.GetRequiredService<MeteringService>());
        return services;
    }

    public static IEndpointRouteBuilder MapMeteringModule(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/metering/quota", async (IMeteringService metering, CancellationToken ct) =>
            HttpResults.From(await metering.GetQuotaAsync(ct))).WithTags("Metering").RequireAuthorization();
        endpoints.MapGet("/api/metering/usage", async (IMeteringService metering, CancellationToken ct) =>
            HttpResults.From(await metering.ListUsageAsync(ct))).WithTags("Metering").RequireAuthorization();
        return endpoints;
    }
}
