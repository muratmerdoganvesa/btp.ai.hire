import type { Candidate } from "@hirelens/api-client";
import { Badge, ScoreBadge } from "@hirelens/ui";
import { Link } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";

const actionLabels: Record<string, string> = {
  shortlist: "candidates.actionShortlist",
  request_info: "candidates.actionInfo",
  review: "candidates.actionReview",
  processing: "candidates.actionProcessing",
  error: "candidates.actionError"
};

export function CandidatesTable({ rows }: { rows: Candidate[] }) {
  const { t } = useTranslation();

  return (
    <div className="overflow-x-auto rounded-2xl border border-border bg-surface shadow-card">
      <table className="w-full min-w-[880px] text-left text-sm">
        <thead className="border-b border-border bg-brand-0/60 text-xs uppercase tracking-wide text-muted">
          <tr>
            <th className="px-4 py-3">{t("candidates.colCandidate")}</th>
            <th className="px-4 py-3">{t("candidates.colScore")}</th>
            <th className="px-4 py-3">{t("candidates.colCoverage")}</th>
            <th className="px-4 py-3">{t("candidates.colAction")}</th>
            <th className="px-4 py-3">{t("candidates.colRisk")}</th>
            <th className="px-4 py-3">{t("candidates.colStatus")}</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => {
            const coverage = row.coverageRatio ?? null;
            const lowCoverage = coverage !== null && coverage < 0.5;
            const actionKey = row.recommendedAction ?? "processing";
            return (
              <tr key={row.id} className="border-b border-border/70 last:border-0 hover:bg-brand-0/40">
                <td className="px-4 py-3">
                  <Link
                    to="/candidates/$candidateId"
                    params={{ candidateId: row.id }}
                    className="font-semibold text-brand hover:text-brand-7"
                  >
                    {row.displayName}
                  </Link>
                  <p className="text-xs text-muted">{new Date(row.createdAt).toLocaleDateString()}</p>
                </td>
                <td className="px-4 py-3">
                  <ScoreBadge score={row.overallScore} label={scoreLabel(row.overallScore, t)} />
                </td>
                <td className="px-4 py-3">
                  <span className="inline-flex items-center gap-1 font-medium">
                    {coverage === null ? "—" : `${Math.round(coverage * 100)}%`}
                    {lowCoverage ? <span title={t("evaluation.coverageWarning")}>⚠</span> : null}
                  </span>
                </td>
                <td className="px-4 py-3">
                  <Badge tone="muted">{t(actionLabels[actionKey] ?? actionLabels.processing)}</Badge>
                </td>
                <td className="px-4 py-3">{row.riskFlagCount ?? 0}</td>
                <td className="px-4 py-3">
                  <Badge tone="muted">{row.evaluationStatus ?? row.status}</Badge>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

function scoreLabel(score: number | null | undefined, t: (key: string) => string) {
  if (score == null) {
    return t("score.unknown");
  }
  if (score >= 75) {
    return t("score.strong");
  }
  if (score >= 60) {
    return t("score.solid");
  }
  return t("score.limited");
}
