using FluentAssertions;
using HireLens.Modules.Matching.Application;
using Xunit;

namespace HireLens.Unit.Tests;

public sealed class ScoreCalculatorTests
{
    private static Rubric SampleRubric() => Rubric.FromWeights(
        "test-v1",
        [
            ("a", 0.5m),
            ("b", 0.3m),
            ("c", 0.2m)
        ]);

    [Fact]
    public void Skips_criteria_without_evidence_from_weight()
    {
        var match = new CriteriaMatch(
        [
            new CriterionMatchResult("a", 80m, MatchConfidence.High),
            new CriterionMatchResult("b", null, MatchConfidence.None),
            new CriterionMatchResult("c", 100m, MatchConfidence.Medium)
        ]);

        var result = ScoreCalculator.Calculate(match, SampleRubric());

        result.IsInsufficient.Should().BeFalse();
        result.SkippedCriteria.Should().Equal("b");
        result.CoverageRatio.Should().Be(0.7m);
        // (0.5*80*1.0 + 0.2*100*0.9) / 0.7 = (40 + 18) / 0.7 = 82.857 → 82.9
        result.Total.Should().Be(82.9m);
    }

    [Fact]
    public void All_empty_returns_insufficient()
    {
        var match = new CriteriaMatch(
        [
            new CriterionMatchResult("a", null, MatchConfidence.None),
            new CriterionMatchResult("b", null, MatchConfidence.None),
            new CriterionMatchResult("c", null, MatchConfidence.None)
        ]);

        var result = ScoreCalculator.Calculate(match, SampleRubric());

        result.IsInsufficient.Should().BeTrue();
        result.Total.Should().BeNull();
        result.CoverageRatio.Should().Be(0m);
        result.SkippedCriteria.Should().HaveCount(3);
    }

    [Fact]
    public void Null_score_is_not_treated_as_zero()
    {
        var withNull = ScoreCalculator.Calculate(
            new CriteriaMatch([new CriterionMatchResult("a", null, MatchConfidence.None), new CriterionMatchResult("b", 100m, MatchConfidence.High)]),
            Rubric.FromWeights("v", [("a", 0.5m), ("b", 0.5m)]));

        var withZero = ScoreCalculator.Calculate(
            new CriteriaMatch([new CriterionMatchResult("a", 0m, MatchConfidence.High), new CriterionMatchResult("b", 100m, MatchConfidence.High)]),
            Rubric.FromWeights("v", [("a", 0.5m), ("b", 0.5m)]));

        withNull.Total.Should().Be(100m);
        withNull.CoverageRatio.Should().Be(0.5m);
        withZero.Total.Should().Be(50m);
        withZero.CoverageRatio.Should().Be(1.0m);
    }
}
