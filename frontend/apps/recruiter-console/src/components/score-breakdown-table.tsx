import type { CriterionScore } from "@hirelens/api-client";
import { ScoreBadge, cn } from "@hirelens/ui";
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
    <section className="overflow-hidden rounded-2xl border border-border bg-surface">
      <header className="border-b border-border px-4 py-3 sm:px-5">
        <h2 className="text-base font-extrabold tracking-tight">{t("evaluation.criteriaTitle")}</h2>
        <p className="mt-0.5 text-sm text-muted">{t("evaluation.criteriaHint")}</p>
      </header>
      <ul className="divide-y divide-border">
        {scores.map((row) => {
          const noEvidence = row.score === null || row.evidenceStatus === "Insufficient";
          return (
            <li
              key={row.criterionId}
              className={cn("px-4 py-4 sm:px-5", noEvidence ? "bg-brand-0/25" : "bg-white")}
            >
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <p className="font-semibold text-foreground">{row.criterionName}</p>
                    <span className="rounded-md bg-brand-1 px-2 py-0.5 text-[0.7rem] font-bold tabular-nums text-brand-7">
                      {row.weight}%
                    </span>
                  </div>
                  <div className="mt-3 max-w-xs">
                    <p className="mb-1 text-[0.7rem] font-medium uppercase tracking-wide text-muted">
                      {t("evaluation.confidence")}
                    </p>
                    <ConfidenceMeter value={row.confidence} />
                  </div>
                </div>
                <ScoreBadge
                  score={row.score}
                  label={noEvidence ? t("score.insufficient") : t("score.solid")}
                />
              </div>
              <div className="mt-3">
                <p className="mb-1.5 text-[0.7rem] font-medium uppercase tracking-wide text-muted">
                  {t("evaluation.evidence")}
                </p>
                {row.evidence.length === 0 ? (
                  <p className="text-sm text-muted">{t("evaluation.noEvidence")}</p>
                ) : (
                  <div className="flex flex-wrap gap-2">
                    {row.evidence.map((item) => (
                      <EvidenceChip
                        key={`${item.startOffset}-${item.quote}`}
                        evidence={item}
                        onSelect={(e) => onSelect?.(e.quote)}
                      />
                    ))}
                  </div>
                )}
              </div>
            </li>
          );
        })}
      </ul>
    </section>
  );
}
