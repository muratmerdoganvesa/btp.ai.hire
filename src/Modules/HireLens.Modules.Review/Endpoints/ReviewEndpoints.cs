using HireLens.Contracts.Review;
using HireLens.Infrastructure.Hosting;
using HireLens.Modules.Review.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HireLens.Modules.Review.Endpoints;

public static class ReviewEndpoints
{
    public static IEndpointRouteBuilder MapReviewEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/candidates/{candidateId:guid}/decisions", async (
            Guid candidateId,
            RecordDecisionRequest request,
            IReviewService review,
            CancellationToken ct) =>
        {
            var result = await review.DecideAsync(candidateId, request, ct);
            return result.IsSuccess ? Results.Created($"/api/candidates/{candidateId}/decisions/{result.Value.Id}", result.Value) : HttpResults.From(result);
        }).WithTags("Review").RequireAuthorization();

        endpoints.MapGet("/api/candidates/{candidateId:guid}/decisions", async (
            Guid candidateId,
            IReviewService review,
            CancellationToken ct) => HttpResults.From(await review.ListAsync(candidateId, ct)))
            .WithTags("Review")
            .RequireAuthorization();

        return endpoints;
    }
}
