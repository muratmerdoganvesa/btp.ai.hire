using HireLens.Contracts.Documents;
using HireLens.Modules.Documents.Application;
using HireLens.Modules.Documents.Endpoints;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace HireLens.Modules.Documents;

public static class DocumentsModule
{
    public static IServiceCollection AddDocumentsModule(this IServiceCollection services)
    {
        services.AddScoped<DocumentService>();
        services.AddScoped<IDocumentService>(sp => sp.GetRequiredService<DocumentService>());
        services.AddScoped<IDocumentTextPort>(sp => sp.GetRequiredService<DocumentService>());
        services.AddScoped<ParseCvJob>();
        return services;
    }

    public static IEndpointRouteBuilder MapDocumentsModule(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapDocumentEndpoints();
        return endpoints;
    }
}
