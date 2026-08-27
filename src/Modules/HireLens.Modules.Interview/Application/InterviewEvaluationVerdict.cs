using HireLens.Contracts.Interview;

namespace HireLens.Modules.Interview.Application;

/// <summary>
/// Recruiter-facing interview verdict. Numeric score may stay null;
/// the summary must still say the evidence was insufficient.
/// </summary>
public static class InterviewEvaluationVerdict
{
    public static string RecruiterSummary(InterviewEvaluationResponse mapped, int? score)
    {
        if (!string.IsNullOrWhiteSpace(mapped.Summary))
        {
            return mapped.Summary.Trim();
        }

        if (score is not null)
        {
            return "Mülakat puanı interview-evaluation-v1 çıktısından yazıldı.";
        }

        if (HasWarning(mapped, "transcript_unusable"))
        {
            return "Transkript kullanılamadı. Mülakat kanıtı yetersiz; sayısal skor yazılmadı.";
        }

        if (HasWarning(mapped, "transcript_too_short"))
        {
            return "Transkript çok kısa veya soruların çoğunu kapsamıyor. Mülakat kanıtı yetersiz; sayısal skor yazılmadı.";
        }

        if (mapped.Criteria.Count == 0)
        {
            return "Mülakat değerlendirmesi çalıştı ama kriter puanı ve gerekçe okunamadı. Yeniden değerlendirin.";
        }

        var detail = mapped.Criteria
            .Select(c => c.Reasoning)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s!.Trim())
            .Distinct(StringComparer.Ordinal)
            .Take(3)
            .ToList();

        var why = detail.Count > 0
            ? string.Join(" ", detail)
            : "Adayın cevapları somut proje, çıktı veya araç anlatımı içermedi; savuşturma veya beyan düzeyinde kaldı.";

        return $"Yetersiz kanıt. {why} Bu nedenle sayısal mülakat skoru yazılmadı.";
    }

    private static bool HasWarning(InterviewEvaluationResponse mapped, string code) =>
        mapped.Warnings.Any(w => string.Equals(w, code, StringComparison.OrdinalIgnoreCase));
}
