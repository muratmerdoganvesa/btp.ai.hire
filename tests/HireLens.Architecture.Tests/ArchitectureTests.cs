using FluentAssertions;
using Xunit;
using HireLens.Modules.Candidate;
using HireLens.Modules.Compliance;
using HireLens.Modules.Documents;
using HireLens.Modules.Evidence;
using HireLens.Modules.Identity;
using HireLens.Modules.Matching;
using HireLens.Modules.Analytics;
using HireLens.Modules.Configuration;
using HireLens.Modules.Integration;
using HireLens.Modules.Interview;
using HireLens.Modules.Metering;
using HireLens.Modules.Notification;
using HireLens.Modules.Privacy;
using HireLens.Modules.Recruiting;
using HireLens.Modules.Review;
using HireLens.Modules.Taxonomy;
using HireLens.Modules.Tenancy;
using HireLens.SharedKernel;
using NetArchTest.Rules;
using System.Reflection;

namespace HireLens.Architecture.Tests;

public sealed class ArchitectureTests
{
    private static readonly (string Name, Assembly Assembly)[] Modules =
    [
        ("Tenancy", typeof(TenancyModule).Assembly),
        ("Identity", typeof(IdentityModule).Assembly),
        ("Recruiting", typeof(RecruitingModule).Assembly),
        ("Candidate", typeof(CandidateModule).Assembly),
        ("Documents", typeof(DocumentsModule).Assembly),
        ("Evidence", typeof(EvidenceModule).Assembly),
        ("Matching", typeof(MatchingModule).Assembly),
        ("Review", typeof(ReviewModule).Assembly),
        ("Compliance", typeof(ComplianceModule).Assembly),
        ("Taxonomy", typeof(TaxonomyModule).Assembly),
        ("Privacy", typeof(PrivacyModule).Assembly),
        ("Interview", typeof(InterviewModule).Assembly),
        ("Notification", typeof(NotificationModule).Assembly),
        ("Configuration", typeof(ConfigurationModule).Assembly),
        ("Metering", typeof(MeteringModule).Assembly),
        ("Integration", typeof(IntegrationModule).Assembly),
        ("Analytics", typeof(AnalyticsModule).Assembly)
    ];

    [Fact]
    public void Modules_must_not_reference_another_module_inner_layers()
    {
        foreach (var (name, assembly) in Modules)
        {
            var forbidden = Modules
                .Where(module => module.Name != name)
                .SelectMany(module => new[]
                {
                    $"HireLens.Modules.{module.Name}.Domain",
                    $"HireLens.Modules.{module.Name}.Infrastructure"
                })
                .ToArray();

            var result = Types.InAssembly(assembly)
                .ShouldNot()
                .HaveDependencyOnAny(forbidden)
                .GetResult();

            result.IsSuccessful.Should().BeTrue($"{name}: {Format(result)}");
        }
    }

    [Fact]
    public void Domain_must_not_depend_on_EF_or_ASPNET()
    {
        foreach (var (name, assembly) in Modules)
        {
            var result = Types.InAssembly(assembly)
                .That().ResideInNamespace($"HireLens.Modules.{name}.Domain")
                .ShouldNot()
                .HaveDependencyOnAny("Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore")
                .GetResult();

            result.IsSuccessful.Should().BeTrue($"{name}: {Format(result)}");
        }
    }

    [Fact]
    public void SharedKernel_must_not_depend_on_modules()
    {
        var moduleNamespaces = Modules.Select(module => $"HireLens.Modules.{module.Name}").ToArray();
        var result = Types.InAssembly(typeof(ITenantEntity).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(moduleNamespaces)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(Format(result));
    }

    private static string Format(TestResult result) =>
        result.FailingTypes is { } types
            ? string.Join(", ", types.Select(t => t.FullName))
            : "architecture rule failed";
}
