using System.Text.Json;

namespace HireLens.Infrastructure.Btp;

public sealed record VcapCredentials(
    string? Url,
    string? ClientId,
    string? ClientSecret,
    string? IdentityZone,
    string? Certificate,
    IReadOnlyDictionary<string, string> Extra);

public sealed record VcapService(string Name, string Label, VcapCredentials Credentials);

public static class VcapServices
{
    public static IReadOnlyList<VcapService> Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        using var document = JsonDocument.Parse(json);
        var services = new List<VcapService>();

        foreach (var labelProperty in document.RootElement.EnumerateObject())
        {
            if (labelProperty.Value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var instance in labelProperty.Value.EnumerateArray())
            {
                var name = instance.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? labelProperty.Name : labelProperty.Name;
                var credentials = ReadCredentials(instance);
                services.Add(new VcapService(name, labelProperty.Name, credentials));
            }
        }

        return services;
    }

    public static VcapService? Find(string? json, params string[] labels) =>
        Parse(json).FirstOrDefault(s => labels.Any(l => s.Label.Equals(l, StringComparison.OrdinalIgnoreCase) ||
                                                        s.Name.Equals(l, StringComparison.OrdinalIgnoreCase)));

    private static VcapCredentials ReadCredentials(JsonElement instance)
    {
        var extra = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? url = null;
        string? clientId = null;
        string? clientSecret = null;
        string? identityZone = null;
        string? certificate = null;

        if (!instance.TryGetProperty("credentials", out var creds) || creds.ValueKind != JsonValueKind.Object)
        {
            return new VcapCredentials(url, clientId, clientSecret, identityZone, certificate, extra);
        }

        foreach (var property in creds.EnumerateObject())
        {
            var value = property.Value.ValueKind == JsonValueKind.String
                ? property.Value.GetString()
                : property.Value.GetRawText();

            if (value is null)
            {
                continue;
            }

            extra[property.Name] = value;

            switch (property.Name)
            {
                case "url":
                case "hostname":
                    url ??= value;
                    break;
                case "clientid":
                    clientId = value;
                    break;
                case "clientsecret":
                    clientSecret = value;
                    break;
                case "identityzone":
                    identityZone = value;
                    break;
                case "certificate":
                    certificate = value;
                    break;
            }
        }

        if (creds.TryGetProperty("uaa", out var uaa) && uaa.ValueKind == JsonValueKind.Object)
        {
            if (uaa.TryGetProperty("url", out var uaaUrl))
            {
                extra["uaa.url"] = uaaUrl.GetString() ?? string.Empty;
            }

            if (uaa.TryGetProperty("clientid", out var uaaId))
            {
                clientId ??= uaaId.GetString();
            }

            if (uaa.TryGetProperty("clientsecret", out var uaaSecret))
            {
                clientSecret ??= uaaSecret.GetString();
            }
        }

        return new VcapCredentials(url, clientId, clientSecret, identityZone, certificate, extra);
    }
}
