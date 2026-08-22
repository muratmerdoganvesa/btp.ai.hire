using HireLens.Contracts.Interview;
using HireLens.Infrastructure.Hosting;
using HireLens.Modules.Interview.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HireLens.Modules.Interview.Endpoints;

public static class InterviewEndpoints
{
    public static IEndpointRouteBuilder MapInterviewEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/interviews/invites", async (
            InterviewInviteRequest request,
            IInterviewService interviews,
            CancellationToken ct) =>
        {
            var result = await interviews.InviteAsync(request, ct);
            return result.IsSuccess
                ? Results.Accepted($"/api/candidates/{request.CandidateId}/interview", result.Value)
                : HttpResults.From(result);
        }).WithTags("Interview").RequireAuthorization();

        endpoints.MapGet("/api/candidates/{candidateId:guid}/interview", async (
            Guid candidateId,
            IInterviewService interviews,
            CancellationToken ct) => HttpResults.From(await interviews.GetForCandidateAsync(candidateId, ct)))
            .WithTags("Interview")
            .RequireAuthorization();

        var pub = endpoints.MapGroup("/api/interviews/public/{token}").WithTags("Interview").AllowAnonymous();
        pub.MapGet("/prep", async (string token, IInterviewService interviews, CancellationToken ct) =>
            HttpResults.From(await interviews.PrepAsync(token, ct)));
        pub.MapGet("/", async (string token, IInterviewService interviews, CancellationToken ct) =>
            HttpResults.From(await interviews.GetByTokenAsync(token, ct)));
        pub.MapPost("/disclose", async (string token, IInterviewService interviews, CancellationToken ct) =>
            HttpResults.From(await interviews.DiscloseAsync(token, ct)));
        pub.MapPost("/start", async (string token, IInterviewService interviews, CancellationToken ct) =>
            HttpResults.From(await interviews.StartAsync(token, ct)));
        pub.MapPost("/pause", async (string token, IInterviewService interviews, CancellationToken ct) =>
            HttpResults.From(await interviews.PauseAsync(token, ct)));
        pub.MapPost("/resume", async (string token, IInterviewService interviews, CancellationToken ct) =>
            HttpResults.From(await interviews.ResumeAsync(token, ct)));
        pub.MapPost("/answers", async (string token, InterviewAnswerRequest request, IInterviewService interviews, CancellationToken ct) =>
            HttpResults.From(await interviews.AnswerAsync(token, request, ct)));

        pub.MapGet("/stream", async (string token, IInterviewService interviews, HttpResponse response, CancellationToken ct) =>
        {
            var session = await interviews.GetByTokenAsync(token, ct);
            if (session.IsFailure)
            {
                return HttpResults.From(session);
            }

            response.Headers.ContentType = "text/event-stream";
            foreach (var turn in session.Value.Turns)
            {
                await response.WriteAsync($"data: {turn.Role}:{turn.Text}\n\n", ct);
                await response.Body.FlushAsync(ct);
            }

            return Results.Empty;
        });

        return endpoints;
    }
}
