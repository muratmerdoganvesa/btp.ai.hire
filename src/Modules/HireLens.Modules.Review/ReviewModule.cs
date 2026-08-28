using HireLens.Modules.Review.Application;
using HireLens.Modules.Review.Endpoints;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace HireLens.Modules.Review;

public static class ReviewModule
{
    public static IServiceCollection AddReviewModule(this IServiceCollection services)
    {
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IOfferService, OfferService>();
        return services;
    }

    public static IEndpointRouteBuilder MapReviewModule(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapReviewEndpoints();
        return endpoints;
    }
}
