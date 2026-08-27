using FluentAssertions;
using HireLens.AiGateway.Providers;
using Xunit;

namespace HireLens.Unit.Tests;

public sealed class AiCoreServiceKeyTests
{
    private const string ValidJson = """
        {
          "clientid": "sb-example",
          "clientsecret": "11c17e62-acfa-4815-899c-1e756e315a64$ebakDt55KLrOYf2Plcu4gGjGZo7vUfwhzX7GGin7AMs=",
          "url": "https://dev-2z4ga5d8.authentication.eu20.hana.ondemand.com",
          "serviceurls": {
            "AI_API_URL": "https://api.ai.prod-eu20.westeurope.azure.ml.hana.ondemand.com"
          }
        }
        """;

    [Fact]
    public void ParseBinding_keeps_dollar_inside_clientsecret()
    {
        var binding = SapOrchestrationProvider.ParseBinding(ValidJson);

        binding.ClientSecret.Should().Contain("$");
        binding.ClientSecret.Should().StartWith("11c17e62");
        binding.TokenUrl.Should().EndWith("/oauth/token");
        binding.AiApiUrl.Should().Contain("api.ai.prod-eu20");
    }

    [Fact]
    public void Coalesce_prefers_valid_file_when_env_is_powershell_corrupted()
    {
        var chosen = AiCoreServiceKey.Coalesce("$ebakDt55KLrOYf2Plcu4gGjGZo7vUfwhzX7GGin7AMs=", ValidJson);

        chosen.Should().NotBeNull();
        AiCoreServiceKey.IsValidBindingJson(chosen).Should().BeTrue();
        SapOrchestrationProvider.ParseBinding(chosen).ClientId.Should().Be("sb-example");
    }

    [Fact]
    public void Coalesce_keeps_corrupted_env_when_no_file_exists()
    {
        var chosen = AiCoreServiceKey.Coalesce("$ebakDt55KLrOYf2Plcu4gGjGZo7vUfwhzX7GGin7AMs=", null);

        chosen.Should().StartWith("$");
        var act = () => SapOrchestrationProvider.ParseBinding(chosen);
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*PowerShell*");
    }

    [Fact]
    public void Empty_env_does_not_block_file()
    {
        AiCoreServiceKey.Coalesce("", ValidJson).Should().NotBeNull();
        AiCoreServiceKey.Coalesce("   ", ValidJson).Should().NotBeNull();
        AiCoreServiceKey.IsValidBindingJson("").Should().BeFalse();
    }
}
