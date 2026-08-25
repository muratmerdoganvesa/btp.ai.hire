import type { CriterionScore, Evaluation } from "@hirelens/api-client";
import { Card, CardContent, CardHeader, CardTitle } from "@hirelens/ui";
import { useTranslation } from "react-i18next";

export function ScoreFormulaPanel({ evaluation }: { evaluation: Evaluation }) {
  const { t } = useTranslation();
  const rows = buildRows(evaluation.scores);
  const usedWeight = rows.filter((r) => !r.skipped).reduce((sum, r) => sum + r.weight, 0);
  const totalContribution = rows.filter((r) => !r.skipped).reduce((sum, r) => sum + r.contribution, 0);
  const computed =
    usedWeight > 0 ? (totalContribution / (usedWeight / 100)).toFixed(1) : t("evaluation.noScore");

  const top = [...rows]
    .filter((r) => !r.skipped)
    .sort((a, b) => b.contribution - a.contribution)
    .slice(0, 3);

  return (
    <Card className="border-border/80 bg-surface/95">
      <CardHeader>
        <CardTitle className="font-display text-xl">{t("evaluation.howCalculated")}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4 text-sm">
        <p className="text-muted">{t("evaluation.formulaIntro")}</p>
        <ul className="space-y-2 font-mono text-xs">
          {rows.map((row) => (
            <li key={row.name} className={row.skipped ? "text-muted" : ""}>
              {row.skipped
                ? `${row.name} — ${t("evaluation.skippedNoEvidence")}`
                : `${row.name}  ${(row.weight / 100).toFixed(2)} × ${row.score} × ${row.confidence.toFixed(2)} = ${row.contribution.toFixed(2)}`}
            </li>
          ))}
        </ul>
        <p>
          {t("evaluation.totalContribution")}: <strong>{totalContribution.toFixed(2)}</strong> ·{" "}
          {t("evaluation.usedWeight")}: <strong>{usedWeight}%</strong>
        </p>
        <p>
          {t("evaluation.score")}: <strong>{computed}</strong>
        </p>
        {top.length > 0 ? (
          <div className="space-y-2">
            <p className="font-semibold">{t("evaluation.topCriteria")}</p>
            {top.map((row) => (
              <div key={row.name}>
                <div className="mb-1 flex justify-between text-xs">
                  <span>{row.name}</span>
                  <span>{row.contribution.toFixed(1)}</span>
                </div>
                <div className="h-2 rounded-full bg-border">
                  <div
                    className="h-full rounded-full bg-brand"
                    style={{ width: `${Math.min(100, (row.contribution / (top[0]?.contribution || 1)) * 100)}%` }}
                  />
                </div>
              </div>
            ))}
          </div>
        ) : null}
      </CardContent>
    </Card>
  );
}

function buildRows(scores: CriterionScore[]) {
  return scores.map((row) => {
    const skipped = row.score === null || row.evidenceStatus === "Insufficient";
    const confidence = row.confidence ?? 1;
    const contribution = skipped ? 0 : row.weight * (row.score! / 100) * confidence;
    return {
      name: row.criterionName,
      weight: row.weight,
      score: row.score ?? 0,
      confidence,
      contribution,
      skipped
    };
  });
}
