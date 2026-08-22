import type { CriterionScore } from "@hirelens/api-client";
import { ScoreBadge } from "@hirelens/ui";
import { useTranslation } from "react-i18next";
import { ConfidenceMeter } from "./confidence-meter";
import { EvidenceChip } from "./evidence-chip";

export function ScoreBreakdownTable({
  scores,
  onSelect
}: {
  scores: CriterionScore[];
  onSelect?: (quote: string) => void;
}) {
  const { t } = useTranslation();
  return (
    <div className="overflow-x-auto rounded-lg border border-border bg-surface">
      <table className="w-full text-left text-sm">
        <thead className="border-b border-border text-muted">
          <tr>
            <th className="px-4 py-2">{t("positions.criterionName")}</th>
            <th className="px-4 py-2">{t("positions.weight")}</th>
            <th className="px-4 py-2">{t("evaluation.overall")}</th>
            <th className="px-4 py-2">{t("evaluation.confidence")}</th>
            <th className="px-4 py-2">{t("evaluation.source")}</th>
          </tr>
        </thead>
        <tbody>
          {scores.map((row) => (
            <tr key={row.criterionId} className="border-b border-border last:border-0">
              <td className="px-4 py-3 font-medium">{row.criterionName}</td>
              <td className="px-4 py-3">{row.weight}</td>
              <td className="px-4 py-3">
                <ScoreBadge
                  score={row.score}
                  label={row.score === null ? t("score.insufficient") : t("score.solid")}
                />
              </td>
              <td className="px-4 py-3">
                <ConfidenceMeter value={row.confidence} />
              </td>
              <td className="px-4 py-3">
                <div className="flex flex-wrap gap-2">
                  {row.evidence.map((item) => (
                    <EvidenceChip key={`${item.startOffset}-${item.quote}`} evidence={item} onSelect={(e) => onSelect?.(e.quote)} />
                  ))}
                </div>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
