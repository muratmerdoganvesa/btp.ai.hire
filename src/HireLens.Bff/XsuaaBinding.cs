using System.Text.Json;

namespace HireLens.Bff;

public sealed record XsuaaClient(
    string Authority,
    string ClientId,
    string ClientSecret);

public static class XsuaaBinding
{
    public static XsuaaClient Read(IConfiguration configuration)
    {
        var json = configuration["VCAP_SERVICES"] ?? Environment.GetEnvironmentVariable("VCAP_SERVICES");
        string? url = null;
        string? clientId = null;
        string? clientSecret = null;

        if (!string.IsNullOrWhiteSpace(json))
        {
            using var document = JsonDocument.Parse(json);
            foreach (var label in document.RootElement.EnumerateObject())
            {
                if (label.Value.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var instance in label.Value.EnumerateArray())
                {
                    var name = instance.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : null;
                    if (!label.Name.Equals("xsuaa", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(name, "xsuaa", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(name, "hirelens-xsuaa", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!instance.TryGetProperty("credentials", out var creds) || creds.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    url = StringProp(creds, "url");
                    clientId = StringProp(creds, "clientid");
                    clientSecret = StringProp(creds, "clientsecret");
                    if (creds.TryGetProperty("uaa", out var uaa) && uaa.ValueKind == JsonValueKind.Object)
                    {
                        url = StringProp(uaa, "url") ?? url;
                        clientId ??= StringProp(uaa, "clientid");
                        clientSecret ??= StringProp(uaa, "clientsecret");
                    }

                    break;
                }
            }
        }

        url = FirstNonEmpty(configuration["XSUAA_URL"], url)?.TrimEnd('/');
        clientId = FirstNonEmpty(configuration["XSUAA_CLIENT_ID"], clientId);
        clientSecret = FirstNonEmpty(configuration["XSUAA_CLIENT_SECRET"], clientSecret);

        if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException("XSUAA url, clientid and clientsecret are required for the BFF.");
        }

        return new XsuaaClient(url, clientId, clientSecret);
    }

    private static string? StringProp(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
