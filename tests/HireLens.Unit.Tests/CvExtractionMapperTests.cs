using FluentAssertions;
using HireLens.Modules.Documents.Application;
using Xunit;

namespace HireLens.Unit.Tests;

public sealed class CvExtractionMapperTests
{
    [Fact]
    public void Accepts_fenced_candidate_profile_without_parseQuality()
    {
        const string json = """
            ```json
            {
              "candidate_profile": {
                "professional_summary": "Senior backend engineer.",
                "education": [
                  { "degree": "B.Sc.", "field": "Computer Engineering", "institution": "Example" }
                ],
                "skills": [
                  { "name": "C#", "evidenceQuote": "6 years C#" }
                ]
              }
            }
            ```
            """;

        CvExtractionMapper.IsStubContent(json).Should().BeFalse();
        CvExtractionMapper.IsUsable(json).Should().BeTrue();
    }

    [Fact]
    public void Rejects_stub_payload()
    {
        CvExtractionMapper.IsUsable("""{"status":"unknown","note":"stub-provider"}""").Should().BeFalse();
    }
}
