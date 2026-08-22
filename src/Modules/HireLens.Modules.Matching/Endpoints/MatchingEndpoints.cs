using HireLens.Modules.Matching.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HireLens.Modules.Matching.Endpoints;

public static class MatchingEndpoints
{
    public static IEndpointRouteBuilder MapMatchingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/candidates/{candidateId:guid}/evaluation", async (
            Guid candidateId,
            IEvaluationService evaluations,
            CancellationToken ct) =>
        {
            var evaluation = await evaluations.GetForCandidateAsync(candidateId, ct);
            return evaluation is null ? Results.NotFound() : Results.Ok(evaluation);
        }).WithTags("Matching").RequireAuthorization();

        return endpoints;
    }
}
