namespace HireLens.Modules.Matching.Application;

public enum MatchConfidence
{
    High,
    Medium,
    Low,
    None
}

public sealed record RubricCriterion(string Id, decimal Weight);

public sealed record RubricScoring(IReadOnlyDictionary<MatchConfidence, decimal> ConfidencePenalty);

public sealed record Rubric(string Version, IReadOnlyList<RubricCriterion> Criteria, RubricScoring Scoring)
{
    public static Rubric FromWeights(string version, IEnumerable<(string Id, decimal Weight)> criteria) =>
        new(
            version,
            criteria.Select(c => new RubricCriterion(c.Id, c.Weight)).ToList(),
            new RubricScoring(new Dictionary<MatchConfidence, decimal>
            {
                [MatchConfidence.High] = 1.0m,
                [MatchConfidence.Medium] = 0.9m,
                [MatchConfidence.Low] = 0.75m,
                [MatchConfidence.None] = 0m
            }));
}

public sealed record CriterionMatchResult(
    string CriterionId,
    decimal? Score,
    MatchConfidence Confidence);

public sealed record CriteriaMatch(IReadOnlyList<CriterionMatchResult> Criteria);

public sealed record ScoreResult(
    decimal? Total,
    decimal CoverageRatio,
    IReadOnlyList<string> SkippedCriteria,
    string RubricVersion,
    bool IsInsufficient)
{
    public static ScoreResult Insufficient(IReadOnlyList<string> skipped, string rubricVersion) =>
        new(null, 0m, skipped, rubricVersion, true);
}

/// <summary>
/// Final score is never produced by the LLM. Confidence penalty and coverage
/// are applied here so the same criterion points always yield the same total.
/// </summary>
public static class ScoreCalculator
{
    public static ScoreResult Calculate(CriteriaMatch match, Rubric rubric)
    {
        decimal weightedSum = 0m;
        decimal usedWeight = 0m;
        var skipped = new List<string>();
        var totalWeight = rubric.Criteria.Sum(c => c.Weight);

        foreach (var c in rubric.Criteria)
        {
            var r = match.Criteria.FirstOrDefault(x => x.CriterionId == c.Id);

            // No evidence → drop from weight; do NOT treat as zero.
            if (r?.Score is null)
            {
                skipped.Add(c.Id);
                continue;
            }

            var penalty = rubric.Scoring.ConfidencePenalty.TryGetValue(r.Confidence, out var p)
                ? p
                : 0.75m;
            weightedSum += c.Weight * r.Score.Value * penalty;
            usedWeight += c.Weight;
        }

        if (usedWeight == 0m)
        {
            return ScoreResult.Insufficient(skipped, rubric.Version);
        }

        var coverage = totalWeight <= 0m ? 0m : Math.Round(usedWeight / totalWeight, 4);

        return new ScoreResult(
            Total: Math.Round(weightedSum / usedWeight, 1),
            CoverageRatio: coverage,
            SkippedCriteria: skipped,
            RubricVersion: rubric.Version,
            IsInsufficient: false);
    }

    public static MatchConfidence MapConfidence(double value) => value switch
    {
        >= 0.85 => MatchConfidence.High,
        >= 0.60 => MatchConfidence.Medium,
        > 0 => MatchConfidence.Low,
        _ => MatchConfidence.None
    };
}
