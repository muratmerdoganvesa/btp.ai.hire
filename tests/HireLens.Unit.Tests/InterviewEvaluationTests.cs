using System.Text.Json;
using FluentAssertions;
using HireLens.Contracts.Interview;
using HireLens.Contracts.Recruiting;
using HireLens.Modules.Interview.Application;
using Xunit;

namespace HireLens.Unit.Tests;

public sealed class InterviewEvaluationPlaceholdersTests
{
    [Fact]
    public void Sends_five_string_keys_and_does_not_stringify_transcript()
    {
        var request = Request(
            rubric: """
                {
                  "rubricId": "r1",
                  "rubricVersion": "v1",
                  "language": "tr",
                  "weightTotal": 100,
                  "criteria": [
                    {
                      "criterionId": "csharp",
                      "name": "C#",
                      "description": "Dil",
                      "weight": 100,
                      "mandatory": true,
                      "sourceQuote": "C# deneyimi",
                      "evidenceHints": ["proje"],
                      "anchors": { "100": "uzman", "70": "yeterli", "40": "kısmi", "0": "yok" }
                    }
                  ]
                }
                """,
            questions:
            [
                new ExtractedInterviewQuestionDto("q1", "csharp", "Bir API anlattırın.", ["somut örnek"])
            ],
            transcript: "[00:03:12] Mülakatçı: Anlatır mısınız?\n[00:03:20] Aday: C# API yazdım.",
            cvMatch: """
                {
                  "rubricId": "r1",
                  "rubricVersion": "v1",
                  "criteria": [
                    { "criterionId": "csharp", "score": 78, "confidence": "medium", "evidence": [], "reasoning": "hit" }
                  ]
                }
                """,
            jobTitle: "Backend");

        var built = InterviewEvaluationPlaceholders.TryBuild(request);
        built.IsSuccess.Should().BeTrue();

        var bag = built.Value;
        bag.Keys.Should().BeEquivalentTo(
            InterviewEvaluationPlaceholders.JobTitle,
            InterviewEvaluationPlaceholders.Rubric,
            InterviewEvaluationPlaceholders.InterviewQuestions,
            InterviewEvaluationPlaceholders.CvMatchResult,
            InterviewEvaluationPlaceholders.Transcript);

        bag.Values.Should().AllSatisfy(v => v.Should().NotBeNull());
        bag[InterviewEvaluationPlaceholders.JobTitle].Should().Be("Backend");
        bag[InterviewEvaluationPlaceholders.Transcript].Should().StartWith("[00:03:12]");
        bag[InterviewEvaluationPlaceholders.Transcript].Should().NotStartWith("\"");
        bag[InterviewEvaluationPlaceholders.Rubric].Should().Contain("\"anchors\"");
        bag[InterviewEvaluationPlaceholders.InterviewQuestions].Should().Contain("\"questionId\":\"q1\"");
        bag[InterviewEvaluationPlaceholders.CvMatchResult].Should().Contain("\"rubricId\":\"r1\"");
        JsonDocument.Parse(bag[InterviewEvaluationPlaceholders.Rubric]).Should().NotBeNull();
        JsonDocument.Parse(bag[InterviewEvaluationPlaceholders.InterviewQuestions]).Should().NotBeNull();
    }

    [Fact]
    public void Optional_fields_are_empty_strings_not_null()
    {
        var request = Request(
            rubric: """{"rubricId":"r1","criteria":[{"criterionId":"sql","name":"SQL","weight":100}]}""",
            questions: [new ExtractedInterviewQuestionDto("q1", "sql", "SQL örneği?", [])],
            transcript: "Aday: index kullandım.",
            cvMatch: null,
            jobTitle: null);

        var bag = InterviewEvaluationPlaceholders.TryBuild(request).Value;
        bag[InterviewEvaluationPlaceholders.CvMatchResult].Should().BeEmpty();
        bag[InterviewEvaluationPlaceholders.JobTitle].Should().BeEmpty();
    }

    [Fact]
    public void Skips_call_when_rubric_criteria_empty()
    {
        var request = Request(
            rubric: """{"rubricId":"r1","criteria":[]}""",
            questions: [new ExtractedInterviewQuestionDto("q1", "sql", "Soru?", [])],
            transcript: "metin",
            cvMatch: null,
            jobTitle: "x");

        var built = InterviewEvaluationPlaceholders.TryBuild(request);
        built.IsFailure.Should().BeTrue();
        built.Error.Code.Should().Be("validation");
    }

    [Fact]
    public void Rejects_mismatched_rubric_ids()
    {
        var request = Request(
            rubric: """{"rubricId":"r1","criteria":[{"criterionId":"a","name":"A","weight":100}]}""",
            questions: [new ExtractedInterviewQuestionDto("q1", "a", "Soru?", [])],
            transcript: "metin",
            cvMatch: """{"rubricId":"r2","criteria":[]}""",
            jobTitle: "");

        var built = InterviewEvaluationPlaceholders.TryBuild(request);
        built.IsFailure.Should().BeTrue();
        built.Error.Message.Should().Contain("rubricId");
    }

    private static EvaluateInterviewRequest Request(
        string rubric,
        IReadOnlyList<ExtractedInterviewQuestionDto> questions,
        string transcript,
        string? cvMatch,
        string? jobTitle) =>
        new(
            JsonSerializer.Deserialize<JsonElement>(rubric),
            questions,
            transcript,
            cvMatch is null ? default : JsonSerializer.Deserialize<JsonElement>(cvMatch),
            jobTitle);
}

public sealed class InterviewEvaluationMapperTests
{
    [Fact]
    public void Maps_scores_consistency_and_transcript_unusable_warning()
    {
        const string json = """
            {
              "rubricId": "r1",
              "rubricVersion": "v1",
              "overallScore": 70,
              "criteria": [
                {
                  "criterionId": "csharp",
                  "questionId": "q1",
                  "score": 70,
                  "confidence": "medium",
                  "status": "sufficient",
                  "reasoning": "Anchor 70",
                  "evidence": [
                    { "quote": "C# API yazdım", "source": "interview", "speaker": "Aday", "timestamp": "00:03:20" }
                  ]
                }
              ],
              "consistency": [
                { "criterionId": "csharp", "cvScore": 78, "interviewScore": 70, "aligned": true, "detail": "uyumlu" }
              ],
              "warnings": ["no_cv_match_result"],
              "summary": "Kanıtlı puan."
            }
            """;

        var result = InterviewEvaluationMapper.Parse(json);
        result.OverallScore.Should().Be(70);
        result.Criteria.Should().ContainSingle();
        result.Criteria[0].QuestionId.Should().Be("q1");
        result.Criteria[0].Evidence[0].Speaker.Should().Be("Aday");
        result.Consistency.Should().ContainSingle();
        result.Warnings.Should().Contain("no_cv_match_result");
    }

    [Fact]
    public void Transcript_unusable_is_a_valid_payload()
    {
        var result = InterviewEvaluationMapper.Parse("""{"warnings":["transcript_unusable"],"criteria":[],"consistency":[]}""");
        result.Warnings.Should().Contain("transcript_unusable");
        result.Criteria.Should().BeEmpty();
    }

    [Fact]
    public void Detects_stub_provider_payload()
    {
        InterviewEvaluationMapper.IsStubContent("""{"status":"unknown","note":"stub-provider"}""")
            .Should().BeTrue();
        InterviewEvaluationMapper.IsStubContent("""{"criteria":[{"criterionId":"a","score":10}]}""")
            .Should().BeFalse();
    }
}
