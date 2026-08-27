using FluentAssertions;
using HireLens.Contracts.Recruiting;
using HireLens.Modules.Recruiting.Application;
using Xunit;

namespace HireLens.Unit.Tests;

public sealed class PositionInterviewQuestionsTests
{
    [Fact]
    public void Roundtrips_extracted_questions()
    {
        var json = PositionInterviewQuestions.Serialize(
        [
            new ExtractedInterviewQuestionDto(
                "q1",
                "C#",
                "Bir API'yi nasıl tasarladınız?",
                ["REST", "versiyonlama"])
        ]);

        var restored = PositionInterviewQuestions.Deserialize(json);
        restored.Should().ContainSingle();
        restored[0].Question.Should().Contain("API");
        restored[0].CriterionId.Should().Be("C#");
    }

    [Fact]
    public void Resolves_criterion_by_name()
    {
        var csharp = Guid.NewGuid();
        var sql = Guid.NewGuid();
        var criteria = new[]
        {
            new PositionCriterionDto(csharp, "C#", "Language", 60),
            new PositionCriterionDto(sql, "SQL", "Data", 40)
        };

        ExtractedInterviewQuestionDto.ResolveCriterionId(criteria, "sql").Should().Be(sql);
        ExtractedInterviewQuestionDto.ResolveCriterionId(criteria, "unknown").Should().Be(csharp);
    }

    [Fact]
    public void Roundtrips_unmeasurable_phrases()
    {
        var json = PositionExtractionNotes.Serialize(
            [new UnmeasurablePhraseDto("takım oyuncusu", "ölçülemez")],
            [new FlaggedPhraseDto("genç", "age", "ayrımcı")]);
        var notes = PositionExtractionNotes.Deserialize(json);
        notes.Unmeasurable.Should().ContainSingle(x => x.Phrase == "takım oyuncusu");
        notes.FlaggedPhrases.Should().ContainSingle(x => x.Phrase == "genç");
    }
}
