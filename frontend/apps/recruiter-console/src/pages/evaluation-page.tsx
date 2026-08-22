import { ApiError } from "@hirelens/api-client";
import { Button, Card, CardContent, CardHeader, CardTitle, ScoreBadge } from "@hirelens/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useParams } from "@tanstack/react-router";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "../api";
import { AppShell } from "../components/app-shell";
import { AuditTimeline } from "../components/audit-timeline";
import { DecisionPanel } from "../components/decision-panel";
import { EvidencePanel } from "../components/evidence-panel";
import { RiskFlagList } from "../components/risk-flag-list";
import { ScoreBreakdownTable } from "../components/score-breakdown-table";
import { AiDisclosureBanner } from "../components/ai-disclosure-banner";
import { GapCard } from "../components/gap-card";
import { InterviewTranscript } from "../components/interview-transcript";
import { SourceHighlighter } from "../components/source-highlighter";

export function EvaluationPage() {
  const { t } = useTranslation();
  const { candidateId } = useParams({ from: "/candidates/$candidateId" });
  const queryClient = useQueryClient();
  const [quote, setQuote] = useState<string | null>(null);
  const [inviteUrl, setInviteUrl] = useState<string | null>(null);

  const candidate = useQuery({
    queryKey: ["candidate", candidateId],
    queryFn: () => api.getCandidate(candidateId)
  });
  const evaluation = useQuery({
    queryKey: ["evaluation", candidateId],
    queryFn: () => api.getEvaluation(candidateId)
  });
  const decisions = useQuery({
    queryKey: ["decisions", candidateId],
    queryFn: () => api.listDecisions(candidateId)
  });
  const interview = useQuery({
    queryKey: ["interview", candidateId],
    queryFn: async () => {
      try {
        return await api.getInterview(candidateId);
      } catch (error) {
        if (error instanceof ApiError && error.status === 404) {
          return null;
        }

        throw error;
      }
    }
  });

  const decide = useMutation({
    mutationFn: (input: { outcome: "advance" | "hold" | "reject"; rationale: string }) =>
      api.recordDecision(candidateId, input),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["decisions", candidateId] });
    }
  });

  const sourceText = (evaluation.data?.scores ?? [])
    .flatMap((score) => score.evidence.map((item) => item.quote))
    .join("\n");

  return (
    <AppShell>
      <div className="flex flex-wrap items-start justify-between gap-4 rounded-lg border border-border bg-surface p-5">
        <div>
          <p className="text-sm text-brand">{t("evaluation.title")}</p>
          <h1 className="text-2xl font-semibold tracking-tight">{candidate.data?.displayName ?? t("evaluation.title")}</h1>
          <p className="mt-2 max-w-2xl text-sm text-muted">{evaluation.data?.summary ?? t("evaluation.noScore")}</p>
        </div>
        <Button
          type="button"
          variant="outline"
          onClick={() => {
            if (!evaluation.data) {
              return;
            }
            void api.inviteInterview(candidateId, evaluation.data.positionId).then((invite) => {
              setInviteUrl(invite.inviteUrl);
              void queryClient.invalidateQueries({ queryKey: ["interview", candidateId] });
            });
          }}
        >
          {t("interview.invite")}
        </Button>
        <ScoreBadge
          score={evaluation.data?.overallScore ?? null}
          label={evaluation.data?.overallScore == null ? t("score.unknown") : t("evaluation.overall")}
        />
      </div>

      {evaluation.data ? (
        <>
          <ScoreBreakdownTable scores={evaluation.data.scores} onSelect={setQuote} />
          <div className="grid gap-6 lg:grid-cols-2">
            <EvidencePanel scores={evaluation.data.scores} onSelect={(selected) => setQuote(selected)} />
            <SourceHighlighter text={sourceText} quote={quote} />
            <RiskFlagList flags={[...evaluation.data.followUps, ...evaluation.data.needsVerification]} />
            <Card>
              <CardHeader>
                <CardTitle>{t("evaluation.followUps")}</CardTitle>
              </CardHeader>
              <CardContent>
                <ul className="list-disc pl-5 text-sm">
                  {evaluation.data.followUps.map((item) => (
                    <li key={item}>{item}</li>
                  ))}
                </ul>
              </CardContent>
            </Card>
          </div>
        </>
      ) : (
        <p className="text-sm text-muted">{t("evaluation.noScore")}</p>
      )}

      {inviteUrl ? (
        <p className="text-sm text-muted">
          {t("interview.invited")}: {inviteUrl}
        </p>
      ) : null}

      {interview.data ? (
        <div className="grid gap-6 lg:grid-cols-2">
          <AiDisclosureBanner />
          <GapCard gaps={interview.data.questions.map((question) => question.prompt)} />
          <InterviewTranscript turns={interview.data.turns} />
        </div>
      ) : null}

      <div className="grid gap-6 lg:grid-cols-2">
        <DecisionPanel
          onSubmit={async (input) => {
            await decide.mutateAsync(input);
          }}
          busy={decide.isPending}
        />
        <AuditTimeline decisions={decisions.data ?? []} />
      </div>
    </AppShell>
  );
}
