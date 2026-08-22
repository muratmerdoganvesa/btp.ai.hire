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

            var hana = configuration["HANA_CONNECTION"]
                ?? configuration.GetConnectionString("Hana");

            if (!string.IsNullOrWhiteSpace(hana))
            {
                options.UseHana(hana);
                return;
            }

            if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
            {
                var name = configuration["INMEMORY_DATABASE_NAME"] ?? "HireLens";
                options.UseInMemoryDatabase(name);
                return;
            }

            throw new InvalidOperationException(
                "HANA_CONNECTION is required outside Development and Testing.");
        });

        return services;
    }
}
