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

    [Fact]
    public void AbsorbLegacyCvText_appends_token_when_matching_prompt_omits_it()
    {
        const string user = "{{?jd_structured}}\n{{?candidate_profile}}";
        var (updated, values) = OrchestrationPlaceholderFilter.AbsorbLegacyCvText(
            "system",
            user,
            new Dictionary<string, string> { ["jd_structured"] = "{}", ["candidate_profile"] = "cv" });

        updated.Should().Contain("{{?cv_text}}");
        values.Should().ContainKey("cv_text");
        values["cv_text"].Should().BeEmpty();

        var bag = OrchestrationPlaceholderFilter.ForTemplate("system\n" + updated, values);
        bag.Keys.Should().BeEquivalentTo("jd_structured", "candidate_profile", "cv_text");
    }

    [Fact]
    public void AbsorbLegacyCvText_leaves_cv_extraction_prompt_unchanged()
    {
        const string user = "<cv_metni>{{?cv_text}}</cv_metni>";
        var (updated, values) = OrchestrationPlaceholderFilter.AbsorbLegacyCvText(
            "system",
            user,
            new Dictionary<string, string> { ["cv_text"] = "masked" });

        updated.Should().Be(user);
        values.Should().ContainKey("cv_text");
        values.Should().NotContainKey("application_data");
    }
}
