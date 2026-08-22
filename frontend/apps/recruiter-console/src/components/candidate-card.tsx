import type { Candidate } from "@hirelens/api-client";
import { Badge, Card, CardContent, InitialsAvatar, ScoreBadge } from "@hirelens/ui";
import { Link } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";

export function CandidateCard({ candidate, selected = false }: { candidate: Candidate; selected?: boolean }) {
  const { t } = useTranslation();
  return (
    <Card className={selected ? "border-brand ring-2 ring-focus" : "hover:border-brand-4"}>
      <CardContent className="flex items-center justify-between gap-4 pt-4">
        <div className="flex items-center gap-3">
          <InitialsAvatar name={candidate.displayName} />
          <div>
            <p className="font-medium">{candidate.displayName}</p>
            <Badge tone="muted">{candidate.status}</Badge>
          </div>
        </div>
        <div className="flex items-center gap-3">
          <ScoreBadge
            score={candidate.overallScore}
            label={candidate.overallScoreLabel ?? t("score.unknown")}
          />
          <Link
            to="/candidates/$candidateId"
            params={{ candidateId: candidate.id }}
            className="text-sm font-medium text-brand"
          >
            {t("candidates.open")}
          </Link>
        </div>
      </CardContent>
    </Card>
  );
}
