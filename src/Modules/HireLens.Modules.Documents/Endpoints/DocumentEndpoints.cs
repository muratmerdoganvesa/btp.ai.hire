using HireLens.Contracts.Documents;
using HireLens.Infrastructure.Hosting;
using HireLens.Modules.Documents.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HireLens.Modules.Documents.Endpoints;

public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/positions/{positionId:guid}/candidates/{candidateId:guid}/documents/upload-session", async (
            Guid positionId,
            Guid candidateId,
            UploadSessionRequest request,
            IDocumentService docs,
            CancellationToken ct) =>
        {
            var result = await docs.StartUploadAsync(candidateId, positionId, request, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : HttpResults.From(result);
        }).WithTags("Documents").RequireAuthorization();

        endpoints.MapPut("/api/object-store/{*objectKey}", async (
            string objectKey,
            HttpRequest http,
            IDocumentService docs,
            CancellationToken ct) =>
        {
            var stored = await docs.StoreBytesAsync(Uri.UnescapeDataString(objectKey), http.Body, http.ContentType ?? "application/octet-stream", ct);
            return stored.IsSuccess ? Results.NoContent() : HttpResults.From(stored);
        }).WithTags("Documents").RequireAuthorization();

        endpoints.MapPost("/api/documents/{documentId:guid}/complete", async (
            Guid documentId,
            IDocumentService docs,
            CancellationToken ct) =>
        {
            var result = await docs.CompleteAsync(documentId, ct);
            if (result.IsFailure)
            {
                return HttpResults.From(result);
            }

            return Results.Accepted($"/api/jobs/{result.Value.JobId}", result.Value);
        }).WithTags("Documents").RequireAuthorization();

        endpoints.MapGet("/api/jobs/{jobId:guid}", async (Guid jobId, IDocumentService docs, CancellationToken ct) =>
            HttpResults.From(await docs.GetJobAsync(jobId, ct)))
            .WithTags("Documents")
            .RequireAuthorization();

        return endpoints;
    }
}
