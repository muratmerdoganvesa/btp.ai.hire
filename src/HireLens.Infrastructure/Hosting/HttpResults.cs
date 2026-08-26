using HireLens.SharedKernel;
using Microsoft.AspNetCore.Http;

namespace HireLens.Infrastructure.Hosting;

public static class HttpResults
{
    public static IResult From<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return FromError(result.Error);
    }

    public static IResult From(Result result)
    {
        if (result.IsSuccess)
        {
            return Results.NoContent();
        }

        return FromError(result.Error);
    }

    public static IResult FromError(Error error) =>
        error.Code switch
        {
            "not_found" => Results.NotFound(),
            "validation" => Results.BadRequest(new { error = "validation", detail = error.Message }),
            "conflict" => Results.Conflict(new { error = error.Message }),
            "unauthorized" => Results.Unauthorized(),
            "forbidden" => Results.Json(new { error = error.Message }, statusCode: 403),
            _ => Results.Problem(error.Message)
        };
}
