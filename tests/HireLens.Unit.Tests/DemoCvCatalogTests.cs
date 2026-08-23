using FluentAssertions;
using HireLens.Infrastructure.Seed;
using Xunit;

namespace HireLens.Unit.Tests;

public sealed class DemoCvCatalogTests
{
    [Fact]
    public void Catalog_has_500_cvs_across_20_departments()
    {
        DemoCvCatalog.Positions.Should().HaveCount(20);
        DemoCvCatalog.Cvs.Should().HaveCount(DemoCvCatalog.ExpectedCount);
        DemoCvCatalog.Cvs.Select(cv => cv.Department).Distinct().Should().HaveCount(20);
        DemoCvCatalog.Cvs.GroupBy(cv => cv.Department).Should().OnlyContain(g => g.Count() == 25);
        DemoCvCatalog.Cvs.Select(cv => cv.CandidateName).Should().OnlyHaveUniqueItems();
        DemoCvCatalog.Cvs.Should().OnlyContain(cv => cv.Text.Contains("2026", StringComparison.Ordinal));
        DemoCvCatalog.Positions.Should().OnlyContain(p => p.Criteria.Sum(c => c.Weight) == 100);
    }
}
