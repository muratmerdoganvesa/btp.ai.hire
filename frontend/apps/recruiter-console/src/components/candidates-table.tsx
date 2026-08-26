import type { Candidate } from "@hirelens/api-client";
import { Badge, ScoreBadge, cn } from "@hirelens/ui";
import { Link } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";

const actionLabels: Record<string, string> = {
  shortlist: "candidates.actionShortlist",
  request_info: "candidates.actionInfo",
  review: "candidates.actionReview",
  processing: "candidates.actionProcessing",
  error: "candidates.actionError"
};

export function CandidatesTable({
  rows,
  selectedId,
  onSelect
}: {
  rows: Candidate[];
  selectedId?: string | null;
  onSelect?: (id: string) => void;
}) {
  const { t } = useTranslation();

  return (
    <div className="overflow-x-auto rounded-xl border border-border bg-surface">
      <table className="w-full min-w-[760px] text-left text-sm">
        <thead className="border-b border-border bg-brand-0/50 text-[0.7rem] uppercase tracking-wide text-muted">
          <tr>
            <th className="px-3 py-2.5 font-bold sm:px-4">{t("candidates.colCandidate")}</th>
            <th className="px-3 py-2.5 font-bold">{t("candidates.colScore")}</th>
            <th className="px-3 py-2.5 font-bold">{t("candidates.colCoverage")}</th>
            <th className="px-3 py-2.5 font-bold">{t("candidates.colAction")}</th>
            <th className="px-3 py-2.5 font-bold">{t("candidates.colRisk")}</th>
            <th className="px-3 py-2.5 font-bold">{t("candidates.colStatus")}</th>
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => {
            const coverage = row.coverageRatio ?? null;
            const lowCoverage = coverage !== null && coverage < 0.5;
            const actionKey = row.recommendedAction ?? "processing";
            const selected = selectedId === row.id;
            return (
              <tr
                key={row.id}
                className={cn(
                  "border-b border-border/70 last:border-0",
                  selected ? "bg-brand-0/70" : "hover:bg-brand-0/35",
                  onSelect ? "cursor-pointer" : null
                )}
                onClick={() => onSelect?.(row.id)}
              >
                <td className="px-3 py-2 sm:px-4">
                  <Link
                    to="/candidates/$candidateId"
                    params={{ candidateId: row.id }}
                    className="font-semibold text-brand-6 hover:text-brand-7"
                    onClick={(event) => event.stopPropagation()}
                  >
                    {row.displayName}
                  </Link>
                  <p className="text-xs text-muted">{new Date(row.createdAt).toLocaleDateString()}</p>
                </td>
                <td className="px-3 py-2">
                  <ScoreBadge score={row.overallScore} label={scoreLabel(row.overallScore, t)} />
                </td>
                <td className="px-3 py-2 tabular-nums">
                  {coverage === null ? "—" : `${Math.round(coverage * 100)}%`}
                  {lowCoverage ? (
                    <span className="ml-1 text-xs text-muted" title={t("evaluation.coverageWarning")}>
                      !
                    </span>
                  ) : null}
                </td>
                <td className="px-3 py-2">
                  <Badge tone="muted">{t(actionLabels[actionKey] ?? actionLabels.processing)}</Badge>
                </td>
                <td className="px-3 py-2 tabular-nums">{row.riskFlagCount ?? 0}</td>
                <td className="px-3 py-2">
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
