using FluentAssertions;
using HireLens.Modules.Matching.Application;
using Xunit;

namespace HireLens.Unit.Tests;

public sealed class ExperienceCalculatorTests
{
    [Fact]
    public void Overlapping_ranges_are_not_double_counted()
    {
        var items = new[]
        {
            new ExperienceItem("2020-01", "2022-01"),
            new ExperienceItem("2021-01", "2023-01")
        };

        var (months, confidence) = ExperienceCalculator.TotalExperienceMonths(items);

        // Jan 2020 → Jan 2023 = 36 months
        months.Should().Be(36);
        confidence.Should().Be(ExperienceConfidence.Exact);
    }

    [Fact]
    public void Adjacent_ranges_merge()
    {
        var items = new[]
        {
            new ExperienceItem("2018-01", "2020-01"),
            new ExperienceItem("2020-01", "2022-01")
        };

        var (months, _) = ExperienceCalculator.TotalExperienceMonths(items);
        months.Should().Be(48);
    }

    [Fact]
    public void Year_precision_marks_approximate()
    {
        var items = new[]
        {
            new ExperienceItem("2019", "2021", "year")
        };

        var (months, confidence) = ExperienceCalculator.TotalExperienceMonths(items);
        months.Should().Be(24);
        confidence.Should().Be(ExperienceConfidence.Approximate);
    }
}
