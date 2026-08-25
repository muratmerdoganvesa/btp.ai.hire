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

        // hana-cloud "hana-free" instance bindings typically expose host/port/jdbc URL + uaa only —
        // no DB user/password. Those require HANA_CONNECTION or a schema/HDI binding.
        var host = First(extra, "host", "hostname");
        var port = First(extra, "port");
        if (string.IsNullOrWhiteSpace(host) || host.StartsWith("jdbc:", StringComparison.OrdinalIgnoreCase))
        {
            if (TryParseJdbc(First(extra, "url") ?? binding.Credentials.Url, out var jdbcHost, out var jdbcPort))
            {
                host = jdbcHost;
                port ??= jdbcPort;
            }
        }

        var user = First(extra, "user", "username", "hdi_user", "db_user", "DB_USER");
        var password = First(extra, "password", "hdi_password", "db_password", "DB_PASSWORD");
        if (string.IsNullOrWhiteSpace(host) ||
            string.IsNullOrWhiteSpace(user) ||
            string.IsNullOrWhiteSpace(password) ||
            host.StartsWith("jdbc:", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        port ??= "443";
        var schema = First(extra, "schema", "schema_name", "currentSchema", "current_schema");
        var builder = $"ServerNode={host}:{port};UID={user};PWD={password};encrypt=true;sslValidateCertificate=false;";
        if (!string.IsNullOrWhiteSpace(schema))
        {
            builder += $"CurrentSchema={schema};";
        }

        return builder;
    }

    /// <summary>
    /// Explains why Resolve returned null (for actionable startup / DI errors). No secrets.
    /// </summary>
    public static string DescribeMissing(IConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(configuration["HANA_CONNECTION"]) ||
            !string.IsNullOrWhiteSpace(configuration.GetConnectionString("Hana")))
        {
            return "HANA_CONNECTION is set but empty after trim.";
        }

        var vcap = configuration["VCAP_SERVICES"];
        var binding = VcapServices.Find(
            vcap,
            "hana",
            "hana-schema",
            "hana-cloud",
            "hanatrial",
            "hana_dev");
        if (binding is null)
        {
            return "No HANA service binding found in VCAP_SERVICES (expected label/name hana, hana-cloud, or hana_dev).";
        }

        var keys = string.Join(", ", binding.Credentials.Extra.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase));
        var hasUser = First(binding.Credentials.Extra, "user", "username", "hdi_user", "db_user", "DB_USER") is not null;
        var hasPassword = First(binding.Credentials.Extra, "password", "hdi_password", "db_password", "DB_PASSWORD") is not null;
        var hasHost = First(binding.Credentials.Extra, "host", "hostname") is not null
            || TryParseJdbc(First(binding.Credentials.Extra, "url") ?? binding.Credentials.Url, out _, out _);

        return
            $"Bound service '{binding.Name}' (label '{binding.Label}') has keys [{keys}]. " +
            $"host={(hasHost ? "yes" : "no")}, user={(hasUser ? "yes" : "no")}, password={(hasPassword ? "yes" : "no")}. " +
            "SAP HANA Cloud instance bindings (hana-free) usually omit DB user/password — " +
            "create a DB user in HANA Cloud Central and set env HANA_CONNECTION=" +
            "ServerNode=<host>:443;UID=<user>;PWD=<password>;encrypt=true;sslValidateCertificate=false;";
    }

    private static bool TryParseJdbc(string? url, out string host, out string? port)
    {
        host = string.Empty;
        port = null;
        if (string.IsNullOrWhiteSpace(url) ||
            !url.StartsWith("jdbc:sap://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var remainder = url["jdbc:sap://".Length..];
        var q = remainder.IndexOf('?', StringComparison.Ordinal);
        if (q >= 0)
        {
            remainder = remainder[..q];
        }

        var colon = remainder.LastIndexOf(':');
        if (colon > 0 && colon < remainder.Length - 1)
        {
            host = remainder[..colon];
            port = remainder[(colon + 1)..];
            return !string.IsNullOrWhiteSpace(host);
        }

        host = remainder;
        return !string.IsNullOrWhiteSpace(host);
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
