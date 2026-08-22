using FluentAssertions;
using Xunit;
using HireLens.Contracts.Recruiting;
using HireLens.Modules.Matching.Application;

namespace HireLens.Unit.Tests;

public sealed class ScoreConsistencyTests
{
    [Fact]
    public void Same_cv_and_jd_scored_five_times_has_zero_variance()
    {
        var csharp = Guid.NewGuid();
        var sql = Guid.NewGuid();
        var position = new PositionSnapshot(
            Guid.NewGuid(),
            "Backend",
            "C# and SQL",
            [
                new PositionCriterionDto(csharp, "C#", "Language", 60),
                new PositionCriterionDto(sql, "SQL", "Data", 40)
            ]);
        const string cv = "Senior engineer. Five years of C# and daily SQL reviews.";

        var totals = Enumerable.Range(0, 5)
            .Select(_ => DeterministicMatcher.Overall(DeterministicMatcher.Score(cv, position)))
            .ToList();

        totals.Should().OnlyContain(score => score == totals[0]);
        (totals.Max() - totals.Min()).Should().BeLessThanOrEqualTo(3);
    }
}
