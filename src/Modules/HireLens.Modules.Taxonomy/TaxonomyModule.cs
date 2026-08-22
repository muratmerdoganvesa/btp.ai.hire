using HireLens.Contracts.Taxonomy;
using HireLens.Modules.Taxonomy.Application;
using Microsoft.Extensions.DependencyInjection;

namespace HireLens.Modules.Taxonomy;

public static class TaxonomyModule
{
    public static IServiceCollection AddTaxonomyModule(this IServiceCollection services)
    {
        services.AddScoped<ITaxonomyNormalizer, TaxonomyNormalizer>();
        return services;
    }
}
