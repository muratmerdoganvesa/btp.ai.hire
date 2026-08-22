using FluentAssertions;
using Xunit;
using HireLens.Contracts.Evidence;
using HireLens.Modules.Evidence.Domain;
using HireLens.SharedKernel;

namespace HireLens.Unit.Tests;

public sealed class EvidenceInvariantTests
{
    [Fact]
    public void Numeric_score_without_evidence_throws()
    {
        var act = () => CriterionScore.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            40,
            80,
            0.9,
            []);

        act.Should().Throw<DomainException>()
            .WithMessage("*evidence*");
    }

    [Fact]
    public void Missing_evidence_persists_null_score_as_insufficient()
    {
        var score = CriterionScore.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            40,
            null,
            0.2,
            []);

        score.Score.Should().BeNull();
        score.EvidenceStatus.Should().Be(EvidenceStatus.Insufficient);
        score.Evidence.Should().BeEmpty();
    }

    [Fact]
    public void Score_with_quote_is_admitted()
    {
        var score = CriterionScore.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            40,
            78,
            0.8,
            [new EvidenceDraft("cv", "five years of C#", 0, 16)]);

        score.Score.Should().Be(78);
        score.EvidenceStatus.Should().Be(EvidenceStatus.Sufficient);
        score.Evidence.Should().ContainSingle(item => item.Quote == "five years of C#");
    }
}
