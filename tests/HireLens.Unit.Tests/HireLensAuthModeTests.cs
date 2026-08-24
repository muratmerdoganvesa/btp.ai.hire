using FluentAssertions;
using HireLens.Infrastructure.Btp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace HireLens.Unit.Tests;

public sealed class HireLensAuthModeTests
{
    [Fact]
    public void Xsuaa_binding_disables_dev_auth_even_in_development()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["VCAP_SERVICES"] =
                """{"xsuaa":[{"name":"hirelens-xsuaa","label":"xsuaa","credentials":{"url":"https://dev-2z4ga5d8.authentication.eu20.hana.ondemand.com"}}]}""",
            ["HireLens:EnableDevAuth"] = "true"
        }).Build();

        HireLensAuthMode.UseDevTokens(new Env("Development"), config).Should().BeFalse();
        HireLensAuthMode.Name(new Env("Development"), config).Should().Be("xsuaa");
    }

    [Fact]
    public void Testing_without_xsuaa_keeps_dev_auth()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        HireLensAuthMode.UseDevTokens(new Env("Testing"), config).Should().BeTrue();
    }

    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
