using HireLens.Contracts.Candidates;
using HireLens.Infrastructure.Hosting;
using HireLens.Modules.Candidate.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HireLens.Modules.Candidate.Endpoints;

public static class CandidateEndpoints
{
    public static IEndpointRouteBuilder MapCandidateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/positions/{positionId:guid}/candidates", async (
            Guid positionId,
            ICandidateService svc,
            CancellationToken ct) => HttpResults.From(await svc.ListAsync(positionId, ct)))
            .WithTags("Candidates")
            .RequireAuthorization();

        endpoints.MapPost("/api/positions/{positionId:guid}/candidates", async (
            Guid positionId,
            CreateCandidateRequest request,
            ICandidateService svc,
            CancellationToken ct) =>
        {
            var result = await svc.CreateAsync(positionId, request, ct);
            return result.IsSuccess
                ? Results.Created($"/api/candidates/{result.Value.Id}", result.Value)
                : HttpResults.From(result);
        }).WithTags("Candidates").RequireAuthorization();

        endpoints.MapGet("/api/candidates/{id:guid}", async (Guid id, ICandidateService svc, CancellationToken ct) =>
            HttpResults.From(await svc.GetAsync(id, ct)))
            .WithTags("Candidates")
            .RequireAuthorization();

        endpoints.MapDelete("/api/candidates/{id:guid}", async (Guid id, ICandidateService svc, CancellationToken ct) =>
            HttpResults.From(await svc.SoftDeleteAsync(id, ct)))
            .WithTags("Candidates")
            .RequireAuthorization();

        return endpoints;
    }
}
