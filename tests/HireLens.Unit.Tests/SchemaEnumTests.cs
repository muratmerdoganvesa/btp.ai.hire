using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace HireLens.Unit.Tests;

public sealed class SchemaEnumTests
{
    [Fact]
    public void Candidate_profile_rejects_unknown_precision_enum_in_schema()
    {
        var schemaPath = Find("schemas", "candidate-profile.schema.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(schemaPath));
        var precision = doc.RootElement
            .GetProperty("properties")
            .GetProperty("experience")
            .GetProperty("items")
            .GetProperty("properties")
            .GetProperty("precision")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(e => e.GetString())
            .ToList();

        precision.Should().BeEquivalentTo(["month", "year", "unknown"]);
        precision.Should().NotContain("day");
    }

    [Fact]
    public void Criteria_match_schema_excludes_reject_action()
    {
        var schemaPath = Find("schemas", "criteria-match.schema.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(schemaPath));
        var actions = doc.RootElement
            .GetProperty("properties")
            .GetProperty("recommendedAction")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(e => e.GetString())
            .ToList();

        actions.Should().NotContain("reject");
        actions.Should().Contain(["shortlist", "request_info", "human_review"]);
    }

    private static string Find(string folder, string file)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, folder, file);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException($"{folder}/{file} not found.");
    }
}
