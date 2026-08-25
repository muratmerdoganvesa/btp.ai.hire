using System.Globalization;
using System.Text.RegularExpressions;

namespace HireLens.Modules.Matching.Application;

public enum ExperienceConfidence
{
    Exact,
    Approximate
}

public sealed record ExperienceItem(
    string? StartDate,
    string? EndDate,
    string Precision = "month");

/// <summary>
/// Merges overlapping employment ranges so concurrent roles are not double-counted.
/// Year-precision rows assume January and mark the result Approximate.
/// </summary>
public static partial class ExperienceCalculator
{
    public static (int Months, ExperienceConfidence Confidence) TotalExperienceMonths(
        IEnumerable<ExperienceItem> items)
    {
        var approximate = false;
        var ranges = new List<(DateTime Start, DateTime End)>();

        foreach (var item in items)
        {
            if (item.StartDate is null)
            {
                continue;
            }

            if (string.Equals(item.Precision, "year", StringComparison.OrdinalIgnoreCase))
            {
                approximate = true;
            }

            var start = ParseMonth(item.StartDate, item.Precision);
            if (start is null)
            {
                continue;
            }

            var end = ParseMonth(item.EndDate, item.Precision) ?? DateTime.UtcNow;
            if (end < start.Value)
            {
                end = start.Value;
            }

            ranges.Add((start.Value, end));
        }

        if (ranges.Count == 0)
        {
            return (0, ExperienceConfidence.Exact);
        }

        ranges = ranges.OrderBy(r => r.Start).ToList();
        var merged = new List<(DateTime Start, DateTime End)>();
        foreach (var r in ranges)
        {
            if (merged.Count > 0 && r.Start <= merged[^1].End)
            {
                merged[^1] = (merged[^1].Start, r.End > merged[^1].End ? r.End : merged[^1].End);
            }
            else
            {
                merged.Add(r);
            }
        }

        var months = merged.Sum(m => (m.End.Year - m.Start.Year) * 12 + m.End.Month - m.Start.Month);
        return (months, approximate ? ExperienceConfidence.Approximate : ExperienceConfidence.Exact);
    }

    public static DateTime? ParseMonth(string? value, string precision = "month")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();

        if (YearOnly().IsMatch(value)
            || string.Equals(precision, "year", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(YearDigits().Match(value).Value, out var year) && year is >= 1950 and <= 2100)
            {
                return new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            }
        }

        if (DateTime.TryParseExact(
                value,
                ["yyyy-MM", "yyyy/MM", "MM/yyyy", "yyyy-MM-dd", "MMM yyyy", "MMMM yyyy"],
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var exact))
        {
            return new DateTime(exact.Year, exact.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var loose))
        {
            return new DateTime(loose.Year, loose.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        return null;
    }

    [GeneratedRegex(@"^\d{4}$")]
    private static partial Regex YearOnly();

    [GeneratedRegex(@"\d{4}")]
    private static partial Regex YearDigits();
}
