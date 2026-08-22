using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace HireLens.Infrastructure.Btp;

public static class HanaConnection
{
    public static string? Resolve(IConfiguration configuration)
    {
        var explicitConnection = configuration["HANA_CONNECTION"]
            ?? configuration.GetConnectionString("Hana");
        if (!string.IsNullOrWhiteSpace(explicitConnection))
        {
            return explicitConnection;
        }

        return FromVcap(configuration["VCAP_SERVICES"]);
    }

    public static string? FromVcap(string? vcapJson)
    {
        var binding = VcapServices.Find(
            vcapJson,
            "hana",
            "hana-schema",
            "hana-cloud",
            "hanatrial",
            "hana_dev");
        if (binding is null)
        {
            return null;
        }

        var extra = binding.Credentials.Extra;
        foreach (var key in new[] { "HANA_CONNECTION", "connectionstring", "connectionString", "connection" })
        {
            if (extra.TryGetValue(key, out var raw) && LooksLikeAdo(raw))
            {
                return raw;
            }
        }

        var host = First(extra, "host", "hostname") ?? binding.Credentials.Url;
        var user = First(extra, "user", "username", "hdi_user");
        var password = First(extra, "password", "hdi_password");
        if (string.IsNullOrWhiteSpace(host) ||
            string.IsNullOrWhiteSpace(user) ||
            string.IsNullOrWhiteSpace(password) ||
            host.StartsWith("jdbc:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var port = First(extra, "port") ?? "443";
        var schema = First(extra, "schema", "schema_name", "currentSchema", "current_schema");
        var builder = $"ServerNode={host}:{port};UID={user};PWD={password};encrypt=true;sslValidateCertificate=false;";
        if (!string.IsNullOrWhiteSpace(schema))
        {
            builder += $"CurrentSchema={schema};";
        }

        return builder;
    }

    public static bool UsesInMemory(IConfiguration configuration, IHostEnvironment environment) =>
        string.IsNullOrWhiteSpace(Resolve(configuration))
        && (environment.IsDevelopment()
            || environment.IsEnvironment("Testing")
            || configuration.GetValue("HireLens:EnableDevAuth", false));

    private static bool LooksLikeAdo(string value) =>
        value.Contains("ServerNode", StringComparison.OrdinalIgnoreCase)
        || value.Contains("UID=", StringComparison.OrdinalIgnoreCase);

    private static string? First(IReadOnlyDictionary<string, string> extra, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (extra.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim().Trim('"');
            }
        }

        return null;
    }
}
