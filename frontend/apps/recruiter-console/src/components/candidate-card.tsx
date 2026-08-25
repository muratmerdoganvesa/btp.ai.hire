import type { Candidate } from "@hirelens/api-client";
import { Badge, Card, CardContent, InitialsAvatar, ScoreBadge } from "@hirelens/ui";
import { Link } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";

export function CandidateCard({ candidate, selected = false }: { candidate: Candidate; selected?: boolean }) {
  const { t } = useTranslation();
  return (
    <Card
      className={
        selected
          ? "border-brand-6 ring-1 ring-brand-6/30"
          : "transition-colors hover:border-brand-4"
      }
    >
      <CardContent className="flex items-center justify-between gap-4 !py-4">
        <div className="flex min-w-0 items-center gap-3">
          <InitialsAvatar name={candidate.displayName} className="size-10 rounded-md" />
          <div className="min-w-0">
            <p className="truncate font-semibold tracking-tight">{candidate.displayName}</p>
            <Badge tone="muted" className="mt-1">
              {candidate.status}
            </Badge>
          </div>
        </div>
        <div className="flex shrink-0 items-center gap-3">
          <ScoreBadge
            score={candidate.overallScore}
            label={candidate.overallScoreLabel ?? t("score.unknown")}
          />
          <Link
            to="/candidates/$candidateId"
            params={{ candidateId: candidate.id }}
            className="text-sm font-semibold text-brand-7 hover:text-brand-9"
          >
            {t("candidates.open")}
          </Link>
        </div>
      </CardContent>
    </Card>
  );
}
