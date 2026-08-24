using FluentAssertions;
using HireLens.Bff;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace HireLens.Unit.Tests;

public sealed class BffHostTests
{
    [Theory]
    [InlineData("dev-2z4ga5d8-hirelens-web.cfapps.eu20-002.hana.ondemand.com", true)]
    [InlineData("hirelens-web.cfapps.eu20-002.hana.ondemand.com", false)]
    [InlineData("hirelens-api.cfapps.eu20-002.hana.ondemand.com", false)]
    public void Tenant_app_host_is_alias(string host, bool alias) =>
        CanonicalHost.IsAlias(host, CanonicalHost.Default).Should().Be(alias);

    [Fact]
    public void Xsuaa_reads_nested_uaa_url()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["VCAP_SERVICES"] =
                """{"xsuaa":[{"name":"hirelens-xsuaa","credentials":{"url":"https://ignored.example","clientid":"id","clientsecret":"secret","uaa":{"url":"https://dev-2z4ga5d8.authentication.eu20.hana.ondemand.com","clientid":"id","clientsecret":"secret"}}}]}"""
        }).Build();

        var client = XsuaaBinding.Read(config);
        client.Authority.Should().Be("https://dev-2z4ga5d8.authentication.eu20.hana.ondemand.com");
        client.ClientId.Should().Be("id");
    }
}
