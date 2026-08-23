using HireLens.Api.Seed;
using HireLens.Infrastructure.Hosting;

namespace HireLens.Api.Endpoints;

public static class SeedEndpoints
{
    public static IEndpointRouteBuilder MapSeedEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/admin/seed-demo", async (IDemoSeedService seed, CancellationToken ct) =>
            HttpResults.From(await seed.SeedAsync(ct)))
            .WithTags("Seed")
            .RequireAuthorization();
        return endpoints;
    }
}
