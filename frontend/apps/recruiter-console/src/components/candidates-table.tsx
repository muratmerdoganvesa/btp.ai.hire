import type { Candidate } from "@hirelens/api-client";
import { Badge, Button, InitialsAvatar, cn } from "@hirelens/ui";
import { Link, useNavigate } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import { ScoringGlossary } from "./scoring-glossary";

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
  onSelect,
  onDelete,
  deletingId
}: {
  rows: Candidate[];
  selectedId?: string | null;
  onSelect?: (id: string) => void;
  onDelete?: (id: string) => void;
  deletingId?: string | null;
}) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const maxScore = Math.max(0, ...rows.map((row) => row.overallScore ?? 0));

  return (
    <div className="overflow-hidden rounded-2xl border border-border bg-surface">
      <div className="flex items-start justify-between gap-3 border-b border-border px-4 py-3 sm:px-5">
        <div>
          <h2 className="text-sm font-extrabold tracking-tight text-foreground">{t("candidates.rankingTitle")}</h2>
          <p className="text-xs text-muted">{t("candidates.rankingHint")}</p>
          <ScoringGlossary />
        </div>
        <p className="shrink-0 text-xs font-semibold tabular-nums text-muted">
          {rows.length} {t("candidates.count")}
        </p>
      </div>

      <ol className="divide-y divide-border">
        {rows.map((row, index) => {
          const rank = index + 1;
          const score = row.overallScore;
          const coverage = row.coverageRatio ?? null;
          const coveragePct = coverage === null ? null : Math.round(coverage * 100);
          const actionKey = row.recommendedAction ?? "processing";
          const selected = selectedId === row.id;
          const barWidth =
            score == null || maxScore <= 0 ? 0 : Math.max(6, Math.round((score / maxScore) * 100));
          const isTop = rank <= 3 && score != null;
          const prev = index > 0 ? rows[index - 1] : null;
          const sameScoreAsPrev =
            prev != null &&
            score != null &&
            prev.overallScore != null &&
            Math.round(score) === Math.round(prev.overallScore);
          const rankReason = rankReasonText({
            rank,
            score,
            coveragePct,
            prevScore: prev?.overallScore ?? null,
            prevCoveragePct:
              prev?.coverageRatio == null ? null : Math.round(prev.coverageRatio * 100),
            t
          });

          return (
            <li
              key={row.id}
              className={cn(
                "group relative flex cursor-pointer flex-col gap-3 px-4 py-4 transition-colors sm:flex-row sm:items-center sm:gap-4 sm:px-5",
                selected ? "bg-brand-0/80" : "hover:bg-brand-0/40",
                isTop && rank === 1 ? "bg-brand-0/35" : null
              )}
              onClick={() =>
                void navigate({ to: "/candidates/$candidateId", params: { candidateId: row.id } })
              }
            >
              <div className="flex min-w-0 flex-1 items-center gap-3 sm:gap-4">
                <RankMark rank={rank} highlighted={isTop} />
                <InitialsAvatar name={row.displayName} className="size-10 shrink-0 rounded-xl" />
                <div className="min-w-0 flex-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <Link
                      to="/candidates/$candidateId"
                      params={{ candidateId: row.id }}
                      className="truncate text-base font-bold tracking-tight text-foreground hover:text-brand-7"
                      onClick={(event) => event.stopPropagation()}
                    >
                      {row.displayName}
                    </Link>
                    <Badge tone={actionTone(actionKey)}>
                      {t(actionLabels[actionKey] ?? actionLabels.processing)}
                    </Badge>
                    {(row.riskFlagCount ?? 0) > 0 ? (
                      <span className="text-xs font-semibold text-danger">
                        {t("candidates.riskHint", { count: row.riskFlagCount })}
                      </span>
                    ) : null}
                  </div>

                  <p className="mt-1.5 text-sm font-semibold text-foreground">
                    {t("candidates.scoreAndCoverage", {
                      score: score == null ? "—" : Math.round(score),
                      coverage: coveragePct == null ? "—" : `%${coveragePct}`
                    })}
                  </p>
                  {rankReason ? (
                    <p
                      className={cn(
                        "mt-1 text-xs font-medium",
                        sameScoreAsPrev || rank === 1 ? "text-brand-7" : "text-muted"
                      )}
                    >
                      {rankReason}
                    </p>
                  ) : null}

                  <div className="mt-2 max-w-md">
                    <div className="h-1.5 overflow-hidden rounded-full bg-border/80">
                      <div
                        className={cn("h-full rounded-full transition-[width]", scoreBarClass(score))}
                        style={{ width: `${barWidth}%` }}
                      />
                    </div>
                  </div>
                </div>
              </div>

              <div className="flex shrink-0 items-center justify-between gap-3 sm:justify-end">
                <div className="text-right">
                  <p
                    className={cn(
                      "text-2xl font-extrabold tabular-nums leading-none tracking-tight",
                      score == null ? "text-muted" : "text-foreground"
                    )}
                  >
                    {score == null ? "—" : Math.round(score)}
                  </p>
                  <p className="mt-1 text-[0.7rem] font-semibold text-muted">{scoreLabel(score, t)}</p>
                  {coveragePct !== null ? (
                    <p className="mt-0.5 text-[0.7rem] font-semibold text-muted">
                      {t("candidates.coverageShort", { pct: coveragePct })}
                    </p>
                  ) : null}
                </div>

                <div className="flex items-center gap-2" onClick={(event) => event.stopPropagation()}>
                  <Button asChild size="sm">
                    <Link to="/candidates/$candidateId" params={{ candidateId: row.id }}>
                      {t("candidates.open")}
                    </Link>
                  </Button>
                  {onSelect ? (
                    <Button type="button" variant="outline" size="sm" onClick={() => onSelect(row.id)}>
                      {t("candidates.uploadCv")}
                    </Button>
                  ) : null}
                  {onDelete ? (
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      className="text-muted hover:text-danger"
                      disabled={deletingId === row.id}
                      onClick={() => {
                        if (!window.confirm(t("candidates.deleteConfirm"))) {
                          return;
                        }
                        onDelete(row.id);
                      }}
                    >
                      {deletingId === row.id ? t("candidates.deleting") : t("candidates.delete")}
                    </Button>
                  ) : null}
                </div>
              </div>
            </li>
          );
        })}
      </ol>
    </div>
  );
}

function RankMark({ rank, highlighted }: { rank: number; highlighted: boolean }) {
  return (
    <div
      className={cn(
        "flex size-9 shrink-0 items-center justify-center rounded-xl text-sm font-extrabold tabular-nums",
        highlighted
          ? rank === 1
            ? "bg-brand-6 text-white"
            : "bg-brand-1 text-brand-7"
          : "bg-brand-0 text-muted"
      )}
      aria-label={`#${rank}`}
    >
      {rank}
    </div>
  );
}

function actionTone(action: string): "default" | "muted" | "danger" {
  if (action === "error") {
    return "danger";
  }
  if (action === "shortlist") {
    return "default";
  }
  return "muted";
}

function scoreBarClass(score: number | null | undefined) {
  if (score == null) {
    return "bg-border";
  }
  if (score >= 75) {
    return "bg-score-strong";
  }
  if (score >= 60) {
    return "bg-score-solid";
  }
  if (score >= 45) {
    return "bg-score-partial";
  }
  return "bg-score-limited";
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
  if (score >= 45) {
    return t("score.partial");
  }
  return t("score.limited");
}

function rankReasonText({
  rank,
  score,
  coveragePct,
  prevScore,
  prevCoveragePct,
  t
}: {
  rank: number;
  score: number | null | undefined;
  coveragePct: number | null;
  prevScore: number | null;
  prevCoveragePct: number | null;
  t: (key: string, opts?: Record<string, string | number>) => string;
}): string | null {
  if (score == null) {
    return t("candidates.rankUnscored");
  }

  if (rank === 1) {
    if (coveragePct != null) {
      return t("candidates.rankFirstExplain", {
        score: Math.round(score),
        coverage: coveragePct
      });
    }
    return t("candidates.rankFirstScoreOnly", { score: Math.round(score) });
  }

  if (prevScore != null && Math.round(score) === Math.round(prevScore)) {
    if (coveragePct != null && prevCoveragePct != null && coveragePct < prevCoveragePct) {
      return t("candidates.rankTieCoverageLower", {
        score: Math.round(score),
        coverage: coveragePct,
        prevCoverage: prevCoveragePct
      });
    }
    return t("candidates.rankTieSameScore", { score: Math.round(score) });
  }

  if (prevScore != null && score < prevScore) {
    return t("candidates.rankLowerScore", {
      score: Math.round(score),
      prevScore: Math.round(prevScore)
    });
  }

  return null;
}
