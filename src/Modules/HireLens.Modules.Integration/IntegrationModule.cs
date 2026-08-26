using HireLens.Contracts.Integration;
using HireLens.Infrastructure.Hosting;
using HireLens.Modules.Integration.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace HireLens.Modules.Integration;

public static class IntegrationModule
{
    public static IServiceCollection AddIntegrationModule(this IServiceCollection services)
    {
        services.AddScoped<IIntegrationService, IntegrationService>();
        return services;
    }

    public static IEndpointRouteBuilder MapIntegrationModule(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/integrations/successfactors/sync", async (
            SfSyncRequest request,
            IIntegrationService integration,
            CancellationToken ct) =>
        {
            var result = await integration.SyncSuccessFactorsAsync(request.Positions, request.Candidates, ct);
            return result.IsSuccess ? Results.Accepted($"/api/integrations/runs/{result.Value.Id}", result.Value) : HttpResults.From(result);
        }).WithTags("Integration").RequireAuthorization();

        endpoints.MapPost("/api/positions/{positionId:guid}/integrations/successfactors/pull", async (
            Guid positionId,
            SfPullRequest? request,
            IIntegrationService integration,
            CancellationToken ct) =>
            HttpResults.From(await integration.PullSuccessFactorsCandidatesAsync(
                positionId,
                request?.Candidates,
                ct))).WithTags("Integration").RequireAuthorization();

        endpoints.MapGet("/api/integrations/runs", async (IIntegrationService integration, CancellationToken ct) =>
            HttpResults.From(await integration.ListAsync(ct))).WithTags("Integration").RequireAuthorization();
        return endpoints;
    }
}

public sealed record SfSyncRequest(
    IReadOnlyList<SfPositionSync> Positions,
    IReadOnlyList<SfCandidateSync> Candidates);
