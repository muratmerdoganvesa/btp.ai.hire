using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace HireLens.Infrastructure.Btp;

/// <summary>
/// XSUAA access tokens use <c>typ: bearer</c>, a <c>kid</c>, and a tenant-specific
/// <c>jku</c> (<c>/token_keys</c>). A static binding PEM without that kid is not enough.
/// </summary>
public static class XsuaaJwt
{
    private static readonly HttpClient KeysClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    public static TokenValidationParameters CreateParameters(
        string authority,
        string? verificationKeyPem,
        Func<Uri, string?>? fetchTokenKeys = null)
    {
        var resolver = new KeyResolver(authority, verificationKeyPem, fetchTokenKeys ?? FetchTokenKeys);
        return new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            TryAllIssuerSigningKeys = true,
            ClockSkew = TimeSpan.FromMinutes(5),
            NameClaimType = "sub",
            RoleClaimType = ClaimTypes.Role,
            ValidTypes = ["JWT", "JWS", "at+jwt", "Bearer", "bearer"],
            IssuerSigningKeyResolver = (token, _, kid, _) => resolver.Resolve(token, kid)
        };
    }

    public static bool IsTrustedJku(Uri jku, string authority)
    {
        if (!jku.IsAbsoluteUri ||
            !string.Equals(jku.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(jku.Query) ||
            !string.Equals(jku.AbsolutePath.TrimEnd('/'), "/token_keys", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!Uri.TryCreate(authority, UriKind.Absolute, out var issuer) ||
            !string.Equals(issuer.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (jku.Host.Equals(issuer.Host, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var issuerLandscape = AuthenticationLandscape(issuer.Host);
        var jkuLandscape = AuthenticationLandscape(jku.Host);
        return issuerLandscape is not null &&
               jkuLandscape is not null &&
               issuerLandscape.Equals(jkuLandscape, StringComparison.OrdinalIgnoreCase);
    }

    public static string? FetchTokenKeys(Uri jku)
    {
        try
        {
            using var response = KeysClient.GetAsync(jku).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
        catch (Exception)
        {
            return null;
        }
    }

    internal static string? AuthenticationLandscape(string host)
    {
        const string marker = ".authentication.";
        var index = host.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return index < 0 ? null : host[(index + 1)..];
    }

    internal static IReadOnlyList<SecurityKey> ParseTokenKeys(string json, string? kid)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("keys", out var keys) || keys.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var parsed = new List<SecurityKey>();
        foreach (var key in keys.EnumerateArray())
        {
            var keyId = key.TryGetProperty("kid", out var kidEl) ? kidEl.GetString() : kid;
            if (key.TryGetProperty("n", out var n) &&
                key.TryGetProperty("e", out var e) &&
                !string.IsNullOrWhiteSpace(n.GetString()) &&
                !string.IsNullOrWhiteSpace(e.GetString()))
            {
                parsed.Add(new JsonWebKey
                {
                    Kty = "RSA",
                    N = n.GetString(),
                    E = e.GetString(),
                    Kid = keyId,
                    Use = "sig",
                    Alg = "RS256"
                });
                continue;
            }

            if (key.TryGetProperty("value", out var pem) && !string.IsNullOrWhiteSpace(pem.GetString()))
            {
                parsed.Add(RsaKeyFromPem(pem.GetString()!, keyId));
            }
        }

        return parsed;
    }

    internal static RsaSecurityKey RsaKeyFromPem(string pem, string? kid)
    {
        var rsa = RSA.Create();
        rsa.ImportFromPem(pem.Replace("\\n", "\n", StringComparison.Ordinal));
        return new RsaSecurityKey(rsa) { KeyId = kid };
    }

    internal static string? ReadJku(string token)
    {
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            return jwt.Header.TryGetValue("jku", out var jku) ? jku?.ToString() : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private sealed class KeyResolver(
        string authority,
        string? verificationKeyPem,
        Func<Uri, string?> fetchTokenKeys)
    {
        private readonly RsaSecurityKey? _bindingKey = string.IsNullOrWhiteSpace(verificationKeyPem)
            ? null
            : RsaKeyFromPem(verificationKeyPem, kid: null);
        private readonly ConcurrentDictionary<string, CacheEntry> _cache = new(StringComparer.OrdinalIgnoreCase);

        public IEnumerable<SecurityKey> Resolve(string token, string? kid)
        {
            var keys = new List<SecurityKey>();
            if (_bindingKey is not null)
            {
                keys.Add(_bindingKey);
                if (!string.IsNullOrWhiteSpace(kid) && _bindingKey.Rsa is RSA rsa)
                {
                    keys.Add(new RsaSecurityKey(rsa) { KeyId = kid });
                }
            }

            var jku = ReadJku(token);
            if (jku is not null &&
                Uri.TryCreate(jku, UriKind.Absolute, out var jkuUri) &&
                IsTrustedJku(jkuUri, authority))
            {
                keys.AddRange(KeysFromJku(jkuUri, kid));
            }

            return keys;
        }

        private IReadOnlyList<SecurityKey> KeysFromJku(Uri jku, string? kid)
        {
            var now = DateTimeOffset.UtcNow;
            if (_cache.TryGetValue(jku.AbsoluteUri, out var cached) && cached.Expires > now)
            {
                return cached.Keys;
            }

            var json = fetchTokenKeys(jku);
            IReadOnlyList<SecurityKey> keys = string.IsNullOrWhiteSpace(json)
                ? []
                : ParseTokenKeys(json, kid);
            _cache[jku.AbsoluteUri] = new CacheEntry(keys, now.AddMinutes(keys.Count == 0 ? 0.25 : 10));
            return keys;
        }
    }

    private sealed record CacheEntry(IReadOnlyList<SecurityKey> Keys, DateTimeOffset Expires);
}
