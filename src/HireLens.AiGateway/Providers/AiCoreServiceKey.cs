using System.Text.Json;

namespace HireLens.AiGateway.Providers;

/// <summary>
/// Normalizes AI Core service-key JSON from env or a local file.
/// PowerShell <c>cf set-env ... $compact</c> (unquoted) treats <c>{...}</c> as a
/// script block and the <c>$</c> inside clientsecret as a variable, so CF often
/// stores a value that starts with <c>$</c> instead of a JSON object.
/// </summary>
public static class AiCoreServiceKey
{
    public const string PowerShellCorruptionMessage =
        "AICORE_SERVICE_KEY bozuk: JSON yerine '$' ile başlayan bir değer var. "
        + "PowerShell, clientsecret içindeki $ karakterini değişken sandı. "
        + "cf set-env değerini Start-Process -ArgumentList ile veya çift tırnakla verin.";

    public static bool IsValidBindingJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        var trimmed = StripBom(json.Trim());
        if (trimmed[0] != '{')
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("clientid", out var id)
                && root.TryGetProperty("clientsecret", out var secret)
                && !string.IsNullOrWhiteSpace(id.GetString())
                && !string.IsNullOrWhiteSpace(secret.GetString());
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Prefer a valid env/config JSON; if it is empty or PowerShell-corrupted,
    /// fall back to a local key file. Invalid leftover env is returned only when
    /// no file exists so ParseBinding can raise a clear error on Cloud Foundry.
    /// </summary>
    public static string? Coalesce(string? envOrConfig, string? fileContents)
    {
        if (IsValidBindingJson(envOrConfig))
        {
            return StripBom(envOrConfig!.Trim());
        }

        if (IsValidBindingJson(fileContents))
        {
            return StripBom(fileContents!.Trim());
        }

        if (!string.IsNullOrWhiteSpace(envOrConfig))
        {
            return envOrConfig.Trim();
        }

        return string.IsNullOrWhiteSpace(fileContents) ? null : fileContents.Trim();
    }

    public static string RequireJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("AICORE_SERVICE_KEY / SapAiCore:ServiceKeyJson is not set.");
        }

        var trimmed = StripBom(json.Trim());
        if (trimmed.StartsWith('$'))
        {
            throw new InvalidOperationException(PowerShellCorruptionMessage);
        }

        var brace = trimmed.IndexOf('{');
        if (brace < 0)
        {
            throw new InvalidOperationException(
                "AICORE_SERVICE_KEY JSON nesnesi değil. clientid/clientsecret içeren service key bekleniyor.");
        }

        if (brace > 0)
        {
            trimmed = trimmed[brace..];
        }

        if (!IsValidBindingJson(trimmed))
        {
            throw new InvalidOperationException(
                "AICORE_SERVICE_KEY parse edilemedi. clientid, clientsecret, url ve serviceurls.AI_API_URL gerekli.");
        }

        return trimmed;
    }

    private static string StripBom(string value) =>
        value.Length > 0 && value[0] == '\uFEFF' ? value[1..] : value;
}
