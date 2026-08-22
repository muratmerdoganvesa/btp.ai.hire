using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace HireLens.Integration.Tests;

public sealed class HireLensApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["INMEMORY_DATABASE_NAME"] = $"HireLens-Tests-{Guid.NewGuid():N}",
                ["DEV_JWT_SIGNING_KEY"] = "HireLens-dev-only-signing-key-32b!"
            });
        });
    }
}
