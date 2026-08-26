using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using HireLens.Contracts;
using HireLens.Contracts.Recruiting;
using Xunit;

namespace HireLens.Integration.Tests;

public sealed class PublicApplicationTests : IClassFixture<HireLensApiFactory>
{
    private const string ConsentVersion = "2026-08-01";

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HireLensApiFactory _factory;

    public PublicApplicationTests(HireLensApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Public_application_accepts_cv_and_returns_reference()
    {
        using var auth = await AuthenticatedClientAsync();
        using var createdPosition = await auth.PostAsJsonAsync("/api/positions", new UpsertPositionRequest(
            "Public apply role",
            "We need C# and SQL.",
            [new UpsertCriterionRequest("C#", "Language", 60), new UpsertCriterionRequest("SQL", "Data", 40)]));
        createdPosition.EnsureSuccessStatusCode();
        var position = await createdPosition.Content.ReadFromJsonAsync<PositionDto>(Json)
            ?? throw new InvalidOperationException("Position create returned an empty body.");
        position.Slug.Should().NotBeNullOrWhiteSpace();

        using var anonymous = _factory.CreateClient();
        using var jobResponse = await anonymous.GetAsync($"/api/public/jobs/{Uri.EscapeDataString(position.Slug!)}");
        jobResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var form = new MultipartFormDataContent
        {
            { new StringContent(position.Slug!), "slug" },
            { new StringContent("Ada Lovelace"), "displayName" },
            { new StringContent("ada@example.com"), "email" },
            { new StringContent("5551234567"), "phone" },
            { new StringContent(ConsentVersion), "consentVersion" },
            { new StringContent("true"), "consentAccepted" }
        };
        var cvBytes = Encoding.UTF8.GetBytes(
            "Senior engineer with five years of C# and daily SQL reviews on SAP BTP.");
        form.Add(new ByteArrayContent(cvBytes), "cv", "cv.txt");

        using var response = await anonymous.PostAsync("/api/public/applications", form);
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var payload = await response.Content.ReadFromJsonAsync<PublicApplicationResponse>(Json);
        payload.Should().NotBeNull();
        payload!.ReferenceNumber.Should().HaveLength(8);
        payload.UploadUrl.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Public_application_without_cv_is_rejected()
    {
        using var auth = await AuthenticatedClientAsync();
        using var createdPosition = await auth.PostAsJsonAsync("/api/positions", new UpsertPositionRequest(
            "No CV role",
            "Description",
            [new UpsertCriterionRequest("Skill", "Desc", 100)]));
        createdPosition.EnsureSuccessStatusCode();
        var position = await createdPosition.Content.ReadFromJsonAsync<PositionDto>(Json)
            ?? throw new InvalidOperationException("Position create returned an empty body.");

        using var form = new MultipartFormDataContent
        {
            { new StringContent(position.Slug!), "slug" },
            { new StringContent("No CV"), "displayName" },
            { new StringContent("nocv@example.com"), "email" },
            { new StringContent(ConsentVersion), "consentVersion" },
            { new StringContent("true"), "consentAccepted" }
        };

        using var anonymous = _factory.CreateClient();
        using var response = await anonymous.PostAsync("/api/public/applications", form);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<HttpClient> AuthenticatedClientAsync()
    {
        using var anonymous = _factory.CreateClient();
        var token = await TestAuth.IssueTokenAsync(anonymous, Guid.NewGuid(), "recruiter", Roles.Recruiter);
        return _factory.As(token);
    }
}
