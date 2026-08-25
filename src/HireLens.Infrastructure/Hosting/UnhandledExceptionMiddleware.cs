using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace HireLens.Infrastructure.Hosting;

/// <summary>
/// Returns JSON for unhandled exceptions so the SPA does not see http_500:empty_body.
/// </summary>
public sealed class UnhandledExceptionMiddleware(RequestDelegate next, ILogger<UnhandledExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
            if (context.Response.HasStarted)
            {
                throw;
            }

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = "internal_error",
                detail = ex.Message
            });
        }
    }
}
