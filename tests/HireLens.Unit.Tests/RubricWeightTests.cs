using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace HireLens.Unit.Tests;

public sealed class RubricWeightTests
{
    [Fact]
    public void Sap_sf_consultant_weights_sum_to_one()
    {
        var path = FindRubric();
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var sum = doc.RootElement.GetProperty("criteria")
            .EnumerateArray()
            .Sum(c => c.GetProperty("weight").GetDecimal());

        sum.Should().Be(1.0m);
    }

    private static string FindRubric()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "rubric", "sap-sf-consultant-v1.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException("rubric/sap-sf-consultant-v1.json not found.");
    }
}
