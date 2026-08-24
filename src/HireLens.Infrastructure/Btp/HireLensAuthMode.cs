using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace HireLens.Infrastructure.Btp;

public static class HireLensAuthMode
{
    public const string Dev = "dev";
    public const string Xsuaa = "xsuaa";

    public static string? ReadVcap(IConfiguration configuration) =>
        configuration["VCAP_SERVICES"] ?? Environment.GetEnvironmentVariable("VCAP_SERVICES");

    public static bool HasXsuaa(IConfiguration configuration) =>
        VcapServices.Find(ReadVcap(configuration), "xsuaa") is not null
        || !string.IsNullOrWhiteSpace(configuration["XSUAA_URL"])
        || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("XSUAA_URL"));

    public static bool UseDevTokens(IHostEnvironment environment, IConfiguration configuration)
    {
        if (HasXsuaa(configuration))
        {
            return false;
        }

        return environment.IsDevelopment()
            || environment.IsEnvironment("Testing")
            || configuration.GetValue("HireLens:EnableDevAuth", false);
    }

    public static string Name(IHostEnvironment environment, IConfiguration configuration) =>
        UseDevTokens(environment, configuration) ? Dev : Xsuaa;
}
