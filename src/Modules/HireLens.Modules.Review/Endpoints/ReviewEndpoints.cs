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

        endpoints.MapGet("/api/offers", async (IOfferService offers, CancellationToken ct) =>
            HttpResults.From(await offers.ListAsync(ct)))
            .WithTags("Offers")
            .RequireAuthorization();

        endpoints.MapGet("/api/candidates/{candidateId:guid}/offers", async (
            Guid candidateId,
            IOfferService offers,
            CancellationToken ct) => HttpResults.From(await offers.ListForCandidateAsync(candidateId, ct)))
            .WithTags("Offers")
            .RequireAuthorization();

        endpoints.MapPost("/api/candidates/{candidateId:guid}/offers", async (
            Guid candidateId,
            CreateOfferRequest request,
            IOfferService offers,
            CancellationToken ct) =>
        {
            var result = await offers.CreateAsync(candidateId, request, ct);
            return result.IsSuccess
                ? Results.Created($"/api/offers/{result.Value.Id}", result.Value)
                : HttpResults.From(result);
        }).WithTags("Offers").RequireAuthorization();

        endpoints.MapPatch("/api/offers/{offerId:guid}", async (
            Guid offerId,
            UpdateOfferRequest request,
            IOfferService offers,
            CancellationToken ct) => HttpResults.From(await offers.UpdateDraftAsync(offerId, request, ct)))
            .WithTags("Offers")
            .RequireAuthorization();

        endpoints.MapPost("/api/offers/{offerId:guid}/send", async (
            Guid offerId,
            IOfferService offers,
            CancellationToken ct) => HttpResults.From(await offers.SendAsync(offerId, ct)))
            .WithTags("Offers")
            .RequireAuthorization();

        endpoints.MapPost("/api/offers/{offerId:guid}/accept", async (
            Guid offerId,
            IOfferService offers,
            CancellationToken ct) => HttpResults.From(await offers.AcceptAsync(offerId, ct)))
            .WithTags("Offers")
            .RequireAuthorization();

        endpoints.MapPost("/api/offers/{offerId:guid}/decline", async (
            Guid offerId,
            IOfferService offers,
            CancellationToken ct) => HttpResults.From(await offers.DeclineAsync(offerId, ct)))
            .WithTags("Offers")
            .RequireAuthorization();

        endpoints.MapPost("/api/offers/{offerId:guid}/withdraw", async (
            Guid offerId,
            IOfferService offers,
            CancellationToken ct) => HttpResults.From(await offers.WithdrawAsync(offerId, ct)))
            .WithTags("Offers")
            .RequireAuthorization();

        return endpoints;
    }
}
