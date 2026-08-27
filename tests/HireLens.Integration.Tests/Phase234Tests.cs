using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Xunit;
using HireLens.Contracts;
using HireLens.Contracts.Analytics;
using HireLens.Contracts.Candidates;
using HireLens.Contracts.Configuration;
using HireLens.Contracts.Documents;
using HireLens.Contracts.Interview;
using HireLens.Contracts.Recruiting;

namespace HireLens.Integration.Tests;

public sealed class Phase234Tests : IClassFixture<HireLensApiFactory>
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HireLensApiFactory _factory;

    public Phase234Tests(HireLensApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Two_tenants_same_code_path_different_theme()
    {
        var clientA = await AuthAsync();
        var clientB = await AuthAsync();

        (await clientA.PutAsJsonAsync("/api/theme", new ThemeDto(200, null, 1, true, 20))).EnsureSuccessStatusCode();
        (await clientB.PutAsJsonAsync("/api/theme", new ThemeDto(40, null, 1.5, true, 40))).EnsureSuccessStatusCode();

        var themeA = await clientA.GetFromJsonAsync<ThemeDto>("/api/theme", Json);
        var themeB = await clientB.GetFromJsonAsync<ThemeDto>("/api/theme", Json);
        themeA!.BrandHue.Should().NotBe(themeB!.BrandHue);
        themeA.InterviewWeight.Should().Be(20);
        themeB.InterviewWeight.Should().Be(40);
    }

    [Fact]
    public async Task Interview_requires_disclosure_then_scores_from_transcript()
    {
        var client = await AuthAsync();
        var seeded = await SeedAsync(client, "Interview Candidate", "Senior engineer with C# and SQL.");
        using var invited = await client.PostAsJsonAsync("/api/interviews/invites", new InterviewInviteRequest(seeded.CandidateId, seeded.PositionId));
        invited.StatusCode.Should().Be(HttpStatusCode.Accepted);
        var invite = await invited.Content.ReadFromJsonAsync<InterviewInviteDto>(Json);
        var token = invite!.InviteUrl.Split('/').Last();

        using var anonymous = _factory.CreateClient();
        using var startDenied = await anonymous.PostAsync($"/api/interviews/public/{token}/start", null);
        startDenied.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        using var disclose = await anonymous.PostAsync($"/api/interviews/public/{token}/disclose", null);
        if (!disclose.IsSuccessStatusCode)
        {
            var body = await disclose.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"disclose failed {(int)disclose.StatusCode}: {body}");
        }

        using var start = await anonymous.PostAsync($"/api/interviews/public/{token}/start", null);
        if (!start.IsSuccessStatusCode)
        {
            var body = await start.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"start failed {(int)start.StatusCode}: {body}");
        }
        var session = await anonymous.GetFromJsonAsync<InterviewSessionDto>($"/api/interviews/public/{token}", Json);
        session!.Questions.Should().OnlyContain(q => q.CriterionId != Guid.Empty);

        foreach (var _ in session.Questions)
        {
            using var answered = await anonymous.PostAsJsonAsync(
                $"/api/interviews/public/{token}/answers",
                new InterviewAnswerRequest("I have shipped C# APIs and wrote SQL reviews weekly."));
            answered.EnsureSuccessStatusCode();
        }

        var completed = await anonymous.GetFromJsonAsync<InterviewSessionDto>($"/api/interviews/public/{token}", Json);
        completed!.Status.Should().Be("completed");
        completed.InterviewScore.Should().BeNull();

        using var evaluated = await client.PostAsync($"/api/candidates/{seeded.CandidateId}/interview/evaluate", null);
        evaluated.EnsureSuccessStatusCode();
        var scored = await evaluated.Content.ReadFromJsonAsync<InterviewSessionDto>(Json);
        scored!.InterviewScore.Should().NotBeNull();
    }

    [Fact]
    public async Task Bias_monitor_has_no_individual_names()
    {
        var client = await AuthAsync();
        await SeedAsync(client, "Named Person", "C# and SQL experience.");
        var bias = await client.GetFromJsonAsync<BiasBucketDto[]>("/api/analytics/bias", Json);
        var payload = JsonSerializer.Serialize(bias);
        payload.Should().NotContain("Named Person");
    }

    [Fact]
    public async Task Funnel_and_benchmark_are_reachable()
    {
        var client = await AuthAsync();
        (await client.GetAsync("/api/analytics/funnel")).EnsureSuccessStatusCode();
        (await client.PostAsync("/api/analytics/benchmark", null)).EnsureSuccessStatusCode();
        (await client.GetAsync("/api/metering/quota")).EnsureSuccessStatusCode();
    }

    private async Task<HttpClient> AuthAsync()
    {
        using var anonymous = _factory.CreateClient();
        var token = await TestAuth.IssueTokenAsync(anonymous, Guid.NewGuid(), "recruiter", Roles.Recruiter);
        return _factory.As(token);
    }

    private static async Task<(Guid PositionId, Guid CandidateId)> SeedAsync(HttpClient client, string name, string cv)
    {
        using var createdPosition = await client.PostAsJsonAsync("/api/positions", new UpsertPositionRequest(
            "Backend",
            "C# and SQL",
            [new UpsertCriterionRequest("C#", "Language", 60), new UpsertCriterionRequest("SQL", "Data", 40)]));
        createdPosition.EnsureSuccessStatusCode();
        var position = await createdPosition.Content.ReadFromJsonAsync<PositionDto>(Json)
            ?? throw new InvalidOperationException("position");
        using var createdCandidate = await client.PostAsJsonAsync(
            $"/api/positions/{position.Id}/candidates",
            new CreateCandidateRequest(name));
        createdCandidate.EnsureSuccessStatusCode();
        var candidate = await createdCandidate.Content.ReadFromJsonAsync<CandidateDto>(Json)
            ?? throw new InvalidOperationException("candidate");
        var bytes = Encoding.UTF8.GetBytes(cv);
        using var session = await client.PostAsJsonAsync(
            $"/api/positions/{position.Id}/candidates/{candidate.Id}/documents/upload-session",
            new UploadSessionRequest("cv.txt", "text/plain", bytes.Length));
        var upload = await session.Content.ReadFromJsonAsync<UploadSessionDto>(Json)
            ?? throw new InvalidOperationException("upload");
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
        (await client.PutAsync(upload.UploadUrl, content)).EnsureSuccessStatusCode();
        (await client.PostAsync($"/api/documents/{upload.DocumentId}/complete", null)).EnsureSuccessStatusCode();
        return (position.Id, candidate.Id);
    }
}
