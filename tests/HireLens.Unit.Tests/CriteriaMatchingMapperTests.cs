using FluentAssertions;
using HireLens.Contracts.Recruiting;
using HireLens.Modules.Matching.Application;
using Xunit;

namespace HireLens.Unit.Tests;

public sealed class CriteriaMatchingMapperTests
{
    [Fact]
    public void Maps_hosted_match_json_onto_position_criteria()
    {
        var csharp = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var sql = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var position = new PositionSnapshot(
            Guid.NewGuid(),
            "Backend",
            "C# and SQL",
            [
                new PositionCriterionDto(csharp, "C#", "Language", 60),
                new PositionCriterionDto(sql, "SQL", "Data", 40)
            ]);

        const string json = """
            {
              "criteria": [
                {
                  "criterionId": "C#",
                  "score": 80,
                  "confidence": "high",
                  "evidence": [{ "quote": "shipped C# APIs", "source": "cv", "startOffset": 0, "endOffset": 16 }]
                },
                { "criterionId": "SQL", "score": null, "confidence": "none", "evidence": [] }
              ],
              "recommendedAction": "human_review"
            }
            """;

        var mapped = CriteriaMatchingMapper.TryMap(json, position);
        mapped.Should().NotBeNull();
        mapped!.Should().HaveCount(2);
        mapped[0].Score.Should().Be(80);
        mapped[0].Evidence.Should().ContainSingle();
        mapped[1].Score.Should().BeNull();
    }

    [Fact]
    public void Stub_payload_is_not_mapped()
    {
        var position = new PositionSnapshot(
            Guid.NewGuid(),
            "Backend",
            "C#",
            [new PositionCriterionDto(Guid.NewGuid(), "C#", "Language", 100)]);
        CriteriaMatchingMapper.TryMap("""{"status":"unknown","note":"stub-provider"}""", position)
            .Should().BeNull();
    }
}
