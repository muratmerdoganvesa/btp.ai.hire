using FluentAssertions;
using HireLens.AiGateway.Providers;
using Xunit;

namespace HireLens.Unit.Tests;

public sealed class OrchestrationPlaceholderFilterTests
{
    [Fact]
    public void Drops_unused_aliases_and_application_data_for_criteria_prompt()
    {
        const string template = """
            {{?jd_title}}
            {{?jd_text}}
            """;

        var bag = OrchestrationPlaceholderFilter.ForTemplate(
            template,
            new Dictionary<string, string>
            {
                ["jd_title"] = "Backend",
                ["jd_text"] = "C# deneyimi.",
                ["job_title"] = "Backend",
                ["job_description"] = "C# deneyimi.",
                ["application_data"] = "yok"
            });

        bag.Keys.Should().BeEquivalentTo("jd_title", "jd_text");
    }

    [Fact]
    public void Keeps_application_data_when_cv_template_references_it()
    {
        const string template = "{{?cv_text}}\n{{?application_data}}";
        var bag = OrchestrationPlaceholderFilter.ForTemplate(
            template,
            new Dictionary<string, string> { ["cv_text"] = "masked" });

        bag.Should().ContainKey("cv_text");
        bag.Should().ContainKey("application_data");
        bag["application_data"].Should().Be("yok");
    }
}
