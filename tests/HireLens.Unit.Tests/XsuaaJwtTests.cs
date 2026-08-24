using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using FluentAssertions;
using HireLens.Infrastructure.Btp;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace HireLens.Unit.Tests;

public sealed class XsuaaJwtTests
{
    private const string Authority = "https://vesacons.authentication.eu20.hana.ondemand.com";

    [Fact]
    public void Typ_bearer_token_with_kid_validates_against_binding_pem()
    {
        using var rsa = RSA.Create(2048);
        var pem = rsa.ExportSubjectPublicKeyInfoPem();
        var token = Issue(rsa, kid: "key-1", typ: "bearer", jku: "https://evil.example/token_keys");

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(token, XsuaaJwt.CreateParameters(Authority, pem, _ => throw new InvalidOperationException("untrusted jku")), out _);

        principal.FindFirst("sub")!.Value.Should().Be("P2005941039");
        principal.FindFirst("zid")!.Value.Should().Be("dev-2z4ga5d8");
    }

    [Fact]
    public void Trusted_jku_keys_are_used()
    {
        using var rsa = RSA.Create(2048);
        var token = Issue(
            rsa,
            kid: "tenant-key",
            typ: "bearer",
            jku: "https://dev-2z4ga5d8.authentication.eu20.hana.ondemand.com/token_keys");
        var keysJson = $$"""
            {"keys":[{"kty":"RSA","kid":"tenant-key","alg":"RS256","n":"{{Base64UrlEncoder.Encode(rsa.ExportParameters(false).Modulus!)}}","e":"{{Base64UrlEncoder.Encode(rsa.ExportParameters(false).Exponent!)}}"}]}
            """;

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(
            token,
            XsuaaJwt.CreateParameters(Authority, verificationKeyPem: null, _ => keysJson),
            out _);

        principal.Identity!.IsAuthenticated.Should().BeTrue();
    }

    [Theory]
    [InlineData("https://dev-2z4ga5d8.authentication.eu20.hana.ondemand.com/token_keys", true)]
    [InlineData("https://vesacons.authentication.eu20.hana.ondemand.com/token_keys", true)]
    [InlineData("https://evil.example/token_keys", false)]
    [InlineData("http://dev-2z4ga5d8.authentication.eu20.hana.ondemand.com/token_keys", false)]
    [InlineData("https://dev-2z4ga5d8.authentication.eu20.hana.ondemand.com/other", false)]
    public void Jku_host_must_be_xsuaa_landscape(string jku, bool trusted) =>
        XsuaaJwt.IsTrustedJku(new Uri(jku), Authority).Should().Be(trusted);

    [Fact]
    public void Nested_uaa_verificationkey_is_read()
    {
        var json = """
            {"xsuaa":[{"name":"hirelens-xsuaa","credentials":{"url":"https://zone.authentication.eu20.hana.ondemand.com","uaa":{"url":"https://zone.authentication.eu20.hana.ondemand.com","verificationkey":"-----BEGIN PUBLIC KEY-----\\nMIIB\\n-----END PUBLIC KEY-----"}}}]}
            """;

        var service = VcapServices.Find(json, "xsuaa");
        service.Should().NotBeNull();
        service!.Credentials.Extra["uaa.verificationkey"].Should().Contain("BEGIN PUBLIC KEY");
        service.Credentials.Extra["uaa.url"].Should().Contain("authentication.eu20");
    }

    private static string Issue(RSA rsa, string kid, string typ, string jku)
    {
        var header = new JwtHeader(new SigningCredentials(new RsaSecurityKey(rsa) { KeyId = kid }, SecurityAlgorithms.RsaSha256))
        {
            ["typ"] = typ,
            ["jku"] = jku
        };
        var payload = new JwtPayload(
            "https://dev-2z4ga5d8.authentication.eu20.hana.ondemand.com/oauth/token",
            "sb-hirelens!t173040",
            [
                new Claim("sub", "P2005941039"),
                new Claim("zid", "dev-2z4ga5d8")
            ],
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddHours(1));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(header, payload));
    }
}
