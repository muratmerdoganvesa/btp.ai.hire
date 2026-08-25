using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Xunit;
using HireLens.Contracts;
using HireLens.Contracts.Candidates;
using HireLens.Contracts.Compliance;
using HireLens.Contracts.Documents;
using HireLens.Contracts.Matching;
using HireLens.Contracts.Recruiting;
using HireLens.Contracts.Review;

namespace HireLens.Integration.Tests;

public sealed class RecruitingFlowTests : IClassFixture<HireLensApiFactory>
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HireLensApiFactory _factory;

    public RecruitingFlowTests(HireLensApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Weights_not_summing_to_100_are_rejected()
    {
        using var client = await AuthenticatedClientAsync();
        using var response = await client.PostAsJsonAsync("/api/positions", new UpsertPositionRequest(
            "Backend",
            "Build APIs",
            [new UpsertCriterionRequest("C#", "Language", 40), new UpsertCriterionRequest("SQL", "Data", 40)]));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Decision_without_rationale_is_rejected()
    {
        using var client = await AuthenticatedClientAsync();
        var seeded = await SeedAnalyzedCandidateAsync(client, "Ada Lovelace", SampleCv);

        using var response = await client.PostAsJsonAsync(
            $"/api/candidates/{seeded.CandidateId}/decisions",
            new RecordDecisionRequest("advance", ""));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Cross_tenant_candidate_read_returns_404()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        using var anonymous = _factory.CreateClient();
        var tokenA = await TestAuth.IssueTokenAsync(anonymous, tenantA, "recruiter-a", Roles.Recruiter);
        var tokenB = await TestAuth.IssueTokenAsync(anonymous, tenantB, "recruiter-b", Roles.Recruiter);
        using var clientA = _factory.As(tokenA);
        using var clientB = _factory.As(tokenB);

        var seeded = await SeedAnalyzedCandidateAsync(clientA, "Grace Hopper", SampleCv);
        using var response = await clientB.GetAsync($"/api/candidates/{seeded.CandidateId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Same_cv_scored_five_times_stays_within_three_points()
    {
        using var client = await AuthenticatedClientAsync();
        var scores = new List<int?>();
        for (var i = 0; i < 5; i++)
        {
            var seeded = await SeedAnalyzedCandidateAsync(client, $"Candidate {i}", SampleCv);
            var evaluation = await client.GetFromJsonAsync<EvaluationDto>(
                $"/api/candidates/{seeded.CandidateId}/evaluation",
                Json);
            evaluation.Should().NotBeNull();
            evaluation!.Status.Should().Be("completed");
            scores.Add(evaluation.OverallScore);
        }

        scores.Should().OnlyContain(score => score == scores[0]);
        (scores.Max() - scores.Min()).Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task Missing_evidence_is_persisted_as_null_score()
    {
        using var client = await AuthenticatedClientAsync();
        var seeded = await SeedAnalyzedCandidateAsync(
            client,
            "No Match",
            "This resume talks about gardening and pottery for many seasons. " +
            "It covers soil preparation, greenhouse work, ceramic glazing, kiln firing, " +
            "and community workshop facilitation. There is no mention of software engineering, " +
            "programming languages, databases, cloud platforms, or enterprise applications.");
        var evaluation = await client.GetFromJsonAsync<EvaluationDto>(
            $"/api/candidates/{seeded.CandidateId}/evaluation",
            Json);

        evaluation.Should().NotBeNull();
        evaluation!.Scores.Should().OnlyContain(score => score.Score == null && score.Evidence.Count == 0);
        evaluation.OverallScore.Should().BeNull();
    }

    [Fact]
    public async Task Decision_and_kvkk_export_complete_the_loop()
    {
        using var client = await AuthenticatedClientAsync();
        var seeded = await SeedAnalyzedCandidateAsync(client, "Alan Turing", SampleCv);

        using var decided = await client.PostAsJsonAsync(
            $"/api/candidates/{seeded.CandidateId}/decisions",
            new RecordDecisionRequest("advance", "Quoted C# and SQL experience matches the requisition."));
        decided.StatusCode.Should().Be(HttpStatusCode.Created);

        using var export = await client.GetAsync($"/compliance/export/{seeded.CandidateId}");
        export.StatusCode.Should().Be(HttpStatusCode.OK);

        using var deletion = await client.PostAsJsonAsync(
            "/compliance/data-deletion-requests",
            new CreateDeletionRequest(seeded.CandidateId, "data_subject_request"));
        deletion.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        using var anonymous = _factory.CreateClient();
        var token = await TestAuth.IssueTokenAsync(anonymous, Guid.NewGuid(), "recruiter", Roles.Recruiter);
        return _factory.As(token);
    }

    private static async Task<(Guid PositionId, Guid CandidateId)> SeedAnalyzedCandidateAsync(
        HttpClient client,
        string displayName,
        string cvText)
    {
        using var createdPosition = await client.PostAsJsonAsync("/api/positions", new UpsertPositionRequest(
            "Backend engineer",
            "We need C# and SQL.",
            [new UpsertCriterionRequest("C#", "Language", 60), new UpsertCriterionRequest("SQL", "Data", 40)]));
        createdPosition.EnsureSuccessStatusCode();
        var position = await createdPosition.Content.ReadFromJsonAsync<PositionDto>(Json)
            ?? throw new InvalidOperationException("Position create returned an empty body.");

        using var createdCandidate = await client.PostAsJsonAsync(
            $"/api/positions/{position.Id}/candidates",
            new CreateCandidateRequest(displayName));
        createdCandidate.EnsureSuccessStatusCode();
        var candidate = await createdCandidate.Content.ReadFromJsonAsync<CandidateDto>(Json)
            ?? throw new InvalidOperationException("Candidate create returned an empty body.");

        var bytes = Encoding.UTF8.GetBytes(cvText);
        using var sessionResponse = await client.PostAsJsonAsync(
            $"/api/positions/{position.Id}/candidates/{candidate.Id}/documents/upload-session",
            new UploadSessionRequest("cv.txt", "text/plain", bytes.Length));
        sessionResponse.EnsureSuccessStatusCode();
        var session = await sessionResponse.Content.ReadFromJsonAsync<UploadSessionDto>(Json)
            ?? throw new InvalidOperationException("Upload session returned an empty body.");

        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        using var uploaded = await client.PutAsync(session.UploadUrl, content);
        uploaded.EnsureSuccessStatusCode();

        using var completed = await client.PostAsync($"/api/documents/{session.DocumentId}/complete", null);
        completed.StatusCode.Should().Be(HttpStatusCode.Accepted);
        completed.Headers.Location.Should().NotBeNull();

        return (position.Id, candidate.Id);
    }

    private const string SampleCv =
        "Senior engineer with five years of C# and daily SQL reviews on SAP BTP. " +
        "Delivered backend APIs, EF Core data access, HANA Cloud integrations, and recruiter-facing " +
        "services for multi-tenant SaaS. Comfortable with observability, CI pipelines, and code reviews.";
}
