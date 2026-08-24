using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HireLens.Infrastructure.Hosting;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHireLensHealth(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = Write
        }).AllowAnonymous();

        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = Write
        }).AllowAnonymous();

        return endpoints;
    }

    private static Task Write(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsJsonAsync(new
        {
            status = report.Status.ToString(),
            gitSha = Environment.GetEnvironmentVariable("GIT_SHA"),
            checks = report.Entries.ToDictionary(
                e => e.Key,
                e => new { status = e.Value.Status.ToString(), e.Value.Description })
        });
    }
}
