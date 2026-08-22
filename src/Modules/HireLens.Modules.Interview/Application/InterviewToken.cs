using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace HireLens.Modules.Interview.Application;

public sealed class InterviewTokenSigner(IConfiguration configuration)
{
    public string Issue(Guid tenantId, Guid sessionId)
    {
        var payload = $"{tenantId:N}.{sessionId:N}";
        return payload + "." + Sign(payload);
    }

    public bool TryRead(string token, out Guid tenantId, out Guid sessionId)
    {
        tenantId = Guid.Empty;
        sessionId = Guid.Empty;
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        var payload = $"{parts[0]}.{parts[1]}";
        if (!FixedEquals(Sign(payload), parts[2]))
        {
            return false;
        }

        return Guid.TryParseExact(parts[0], "N", out tenantId) && Guid.TryParseExact(parts[1], "N", out sessionId);
    }

    public string Hash(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }

    private string Sign(string payload)
    {
        var key = configuration["DEV_JWT_SIGNING_KEY"] ?? configuration["Interview:SigningKey"] ?? "HireLens-dev-only-signing-key-32b!";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }

    private static bool FixedEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
}
