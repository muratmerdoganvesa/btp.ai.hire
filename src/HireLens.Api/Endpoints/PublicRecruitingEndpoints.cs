using HireLens.Api.Application;
using HireLens.Contracts.Recruiting;
using HireLens.Infrastructure.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace HireLens.Api.Endpoints;

public static class PublicRecruitingEndpoints
{
    public static IEndpointRouteBuilder MapPublicRecruitingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var jobs = endpoints.MapGroup("/api/public/jobs").WithTags("Public").AllowAnonymous();
        jobs.MapGet("/{slug}", async (string slug, IPublicApplicationService svc, CancellationToken ct) =>
            HttpResults.From(await svc.GetJobAsync(slug, ct)));

        var applications = endpoints.MapGroup("/api/public/applications").WithTags("Public").AllowAnonymous();
        applications.MapPost("/", async (
            HttpRequest http,
            IPublicApplicationService svc,
            CancellationToken ct) =>
        {
            if (!http.HasFormContentType)
            {
                return Results.BadRequest(new { error = "multipart_form_required" });
            }

            var form = await http.ReadFormAsync(ct);
            var request = new PublicApplicationRequest(
                form["slug"].ToString(),
                form["displayName"].ToString(),
                form["email"].ToString(),
                string.IsNullOrWhiteSpace(form["phone"]) ? null : form["phone"].ToString(),
                form["consentVersion"].ToString(),
                bool.TryParse(form["consentAccepted"], out var accepted) && accepted);
            var cv = form.Files.GetFile("cv");
            var ip = http.HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await svc.ApplyAsync(request, cv, ip, ct);
            return result.IsSuccess
                ? Results.Created($"/api/public/applications/{result.Value.ReferenceNumber}", result.Value)
                : HttpResults.From(result);
        });

        applications.MapGet("/{reference}", async (
            string reference,
            IPublicApplicationService svc,
            CancellationToken ct) =>
            HttpResults.From(await svc.GetStatusAsync(reference, ct)));

        applications.MapPost("/{reference}/cv", async (
            string reference,
            HttpRequest http,
            IPublicApplicationService svc,
            CancellationToken ct) =>
        {
            if (!http.HasFormContentType)
            {
                return Results.BadRequest(new { error = "multipart_form_required" });
            }

            var form = await http.ReadFormAsync(ct);
            var cv = form.Files.GetFile("cv");
            if (cv is null)
            {
                return Results.BadRequest(new { error = "cv_required" });
            }

            var result = await svc.ReuploadCvAsync(reference, cv, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : HttpResults.From(result);
        });

        return endpoints;
    }
}
