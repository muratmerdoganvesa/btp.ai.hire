import type { Evaluation } from "@hirelens/api-client";
import { Card, CardContent, CardHeader, CardTitle } from "@hirelens/ui";
import { useTranslation } from "react-i18next";

/** HR-friendly coverage summary — no formulas, model names, or raw IDs. */
export function ScoreFormulaPanel({ evaluation }: { evaluation: Evaluation }) {
  const { t } = useTranslation();
  const rows = evaluation.scores;
  const withEvidence = rows.filter((r) => r.score !== null && r.evidenceStatus !== "Insufficient");
  const missing = rows.filter((r) => r.score === null || r.evidenceStatus === "Insufficient");
  const coveragePct = Math.round(evaluation.coverageRatio * 100);

  return (
    <Card className="border-border/80">
      <CardHeader className="pb-2">
        <CardTitle className="text-base font-extrabold tracking-tight">{t("evaluation.howCalculated")}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4 text-sm">
        <p className="leading-relaxed text-muted">{t("evaluation.formulaIntro")}</p>

        <div className="rounded-xl bg-brand-0/60 px-3 py-3">
          <div className="mb-1.5 flex items-center justify-between text-xs font-semibold">
            <span>{t("evaluation.coverage")}</span>
            <span className="tabular-nums">{coveragePct}%</span>
          </div>
          <div className="h-2 overflow-hidden rounded-full bg-white">
            <div
              className="h-full rounded-full bg-brand-6 transition-[width]"
              style={{ width: `${Math.min(100, Math.max(0, coveragePct))}%` }}
            />
          </div>
          <p className="mt-2 text-xs text-muted">
            {t("evaluation.evidenceCount", { found: withEvidence.length, total: rows.length })}
          </p>
        </div>

        {missing.length > 0 ? (
          <div>
            <p className="mb-2 text-xs font-bold uppercase tracking-wide text-muted">
              {t("evaluation.missingEvidence")}
            </p>
            <ul className="flex flex-col gap-1.5">
              {missing.map((row) => (
                <li
                  key={row.criterionId}
                  className="rounded-lg border border-border/80 bg-white px-3 py-2 text-sm font-medium"
                >
                  {row.criterionName}
                  <span className="ml-2 text-xs font-normal text-muted">({row.weight}%)</span>
                </li>
              ))}
            </ul>
          </div>
        ) : (
          <p className="text-sm text-muted">{t("evaluation.allCriteriaCovered")}</p>
        )}
      </CardContent>
    </Card>
  );
}
