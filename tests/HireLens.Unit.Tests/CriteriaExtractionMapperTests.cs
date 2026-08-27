using FluentAssertions;
using HireLens.Modules.Recruiting.Application;
using Xunit;

namespace HireLens.Unit.Tests;

public sealed class CriteriaExtractionMapperTests
{
    [Fact]
    public void Maps_hosted_orchestration_rubric_and_interview_questions()
    {
        const string json = """
            {
              "jdStructured": { "title": "Teknopark Arge Uzmanı" },
              "rubric": {
                "weightTotal": 100,
                "criteria": [
                  {
                    "criterionId": "grant_program_management",
                    "name": "Hibe ve teşvik programı yönetimi",
                    "description": "Hibe ve teşvik programlarını yönetme deneyimi.",
                    "weight": 18,
                    "mandatory": true
                  },
                  {
                    "criterionId": "rnd_collaboration_management",
                    "name": "Ar-Ge işbirliği yönetimi",
                    "description": "Ar-Ge işbirliklerini koordine etme deneyimi.",
                    "weight": 16,
                    "mandatory": true
                  },
                  {
                    "name": "Proje çıktılarının ürünleştirilmesi",
                    "description": "Patent ve yayın.",
                    "weight": 15,
                    "mandatory": true
                  },
                  {
                    "name": "Teknopark portal yönetimi",
                    "description": "Portal ve muafiyet.",
                    "weight": 12,
                    "mandatory": true
                  },
                  {
                    "name": "Ar-Ge proje başvurusu",
                    "description": "Başvuru süreçleri.",
                    "weight": 14,
                    "mandatory": true
                  },
                  {
                    "name": "Ar-Ge süresi otomasyonu",
                    "description": "Süre hesaplama.",
                    "weight": 11,
                    "mandatory": true
                  },
                  {
                    "name": "İnovasyon kültürü",
                    "description": "Kültür güçlendirme.",
                    "weight": 8,
                    "mandatory": false
                  },
                  {
                    "name": "Ödüllendirme süreci",
                    "description": "Teşvik tasarımı.",
                    "weight": 6,
                    "mandatory": false
                  }
                ]
              },
              "interviewQuestions": [
                {
                  "questionId": "q1",
                  "criterionId": "grant_program_management",
                  "question": "Bir hibe programını nasıl yönettiniz?",
                  "whatToListenFor": ["Program adı", "Süreç adımları"]
                },
                {
                  "questionId": "q2",
                  "criterionId": "rnd_collaboration_management",
                  "question": "Bir Ar-Ge işbirliğini nasıl koordine ettiniz?",
                  "whatToListenFor": ["Taraflar"]
                }
              ],
              "warnings": []
            }
            """;

        var result = CriteriaExtractionMapper.Parse(json);

        result.Criteria.Should().HaveCount(8);
        result.Criteria[0].Label.Should().Be("Hibe ve teşvik programı yönetimi");
        result.Criteria[0].Mandatory.Should().BeTrue();
        result.TotalWeight.Should().Be(100);
        result.InterviewQuestions.Should().HaveCount(2);
        result.InterviewQuestions[0].Question.Should().Contain("hibe");
    }

    [Fact]
    public void Unwraps_orchestration_envelope_before_mapping()
    {
        const string raw = """
            {
              "module_results": {
                "prompt_templating": {
                  "choices": [
                    {
                      "message": {
                        "content": {
                          "rubric": {
                            "criteria": [
                              { "name": "SQL", "description": "Veri", "weight": 100, "mandatory": true }
                            ]
                          },
                          "interviewQuestions": []
                        }
                      }
                    }
                  ]
                }
              }
            }
            """;

        var result = CriteriaExtractionMapper.Parse(raw);

        result.Criteria.Should().ContainSingle();
        result.Criteria[0].Label.Should().Be("SQL");
        result.TotalWeight.Should().Be(100);
    }

    [Fact]
    public void Detects_stub_provider_payload()
    {
        CriteriaExtractionMapper.IsStubContent("""{"status":"unknown","note":"stub-provider"}""")
            .Should().BeTrue();
        CriteriaExtractionMapper.IsStubContent("""{"rubric":{"criteria":[{"name":"A","weight":100}]}}""")
            .Should().BeFalse();
    }
}
