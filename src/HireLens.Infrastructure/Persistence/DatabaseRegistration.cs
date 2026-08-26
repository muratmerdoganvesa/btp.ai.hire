using HireLens.Infrastructure.Btp;
using HireLens.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Sap.EntityFrameworkCore.Hana;

namespace HireLens.Infrastructure.Persistence;

public static class DatabaseRegistration
{
    public static IServiceCollection AddHireLensPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<TenantContext>();
        services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
        services.AddScoped<SystemTenantScope>();
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddScoped<IAuditSink, NoOpAuditSink>();
        services.AddScoped<IAuditWriter, CompositeAuditWriter>();

        services.AddDbContext<HireLensDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<AuditSaveChangesInterceptor>();
            options.AddInterceptors(interceptor);

            // Integration/unit hosts must never pick up a machine/CI HANA_CONNECTION by accident —
            // that made interview tests hit real HANA SQL and fail CI while local InMemory passed.
            if (environment.IsEnvironment("Testing") || HanaConnection.UsesInMemory(configuration, environment))
            {
                var name = configuration["INMEMORY_DATABASE_NAME"] ?? "HireLens";
                options.UseInMemoryDatabase(name);
                return;
            }

            var hana = HanaConnection.Resolve(configuration);
            if (!string.IsNullOrWhiteSpace(hana))
            {
                options.UseHana(hana);
                return;
            }

            throw new InvalidOperationException(
                "HANA_CONNECTION or a bound HANA service with host/user/password is required outside Development and Testing. "
                + HanaConnection.DescribeMissing(configuration));
        });

        return services;
    }
}
