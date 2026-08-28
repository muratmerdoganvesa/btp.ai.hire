using FluentAssertions;
using HireLens.Contracts.Evidence;
using HireLens.Modules.Matching.Application;
using Xunit;

namespace HireLens.Unit.Tests;

public sealed class EvaluationNarrativeTests
{
    [Fact]
    public void Placeholder_english_is_detected()
    {
        EvaluationNarrative.IsGenericPlaceholder("Evidence-bound scores are ready for human review.")
            .Should().BeTrue();
        EvaluationNarrative.IsGenericPlaceholder("Skor 59 / 100, kapsam %28. CV’de öne çıkan: Ar-Ge.")
            .Should().BeFalse();
    }

    [Fact]
    public void Builds_turkish_summary_from_scores_and_gaps()
    {
        var arge = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var english = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var names = new Dictionary<Guid, string>
        {
            [arge] = "Ar-Ge Proje Yönetimi",
            [english] = "İngilizce"
        };
        var text = EvaluationNarrative.Build(
            59,
            0.28m,
            [
                new ProposedCriterionScore(arge, 10, 35, 0.7, []),
                new ProposedCriterionScore(english, 10, null, 0.2, [])
            ],
            names);

        text.Should().Contain("59");
        text.Should().Contain("%28");
        text.Should().Contain("Ar-Ge Proje Yönetimi");
        text.Should().Contain("İngilizce");
    }
}
