using HireLens.Contracts.Interview;
using HireLens.Infrastructure.Hosting;
using HireLens.Modules.Interview.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HireLens.Modules.Interview.Endpoints;

public static class InterviewEvaluationEndpoints
{
    public static IEndpointRouteBuilder MapInterviewEvaluationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/interviews/evaluate", Evaluate)
            .WithTags("Interview")
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task<IResult> Evaluate(
        EvaluateInterviewRequest request,
        IInterviewEvaluationService svc,
        CancellationToken ct)
    {
        var result = await svc.EvaluateAsync(request, ct);
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        if (result.Error.Code == "validation")
        {
            return Results.BadRequest(new { error = result.Error.Message });
        }

        return Results.Json(
            new { error = result.Error.Message },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
}
