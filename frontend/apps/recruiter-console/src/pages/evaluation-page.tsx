import { ApiError } from "@hirelens/api-client";
import { Badge, Button, Card, CardContent, CardHeader, CardTitle, ScoreBadge } from "@hirelens/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate, useParams } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "../api";
import { AppShell } from "../components/app-shell";
import { AiDisclosureBanner } from "../components/ai-disclosure-banner";
import { DecisionPanel } from "../components/decision-panel";
import { EvaluationAuditPanel } from "../components/evaluation-audit-panel";
import { GapCard } from "../components/gap-card";
import { InterviewFramesGallery } from "../components/interview-frames-gallery";
import { InterviewTranscript } from "../components/interview-transcript";
import { RiskFlagList } from "../components/risk-flag-list";
import { ScoreBreakdownTable } from "../components/score-breakdown-table";

const outcomeKeys: Record<string, string> = {
  advance: "decision.advance",
  hold: "decision.hold",
  reject: "decision.reject"
};

export function EvaluationPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { candidateId } = useParams({ from: "/candidates/$candidateId" });
  const queryClient = useQueryClient();
  const [inviteUrl, setInviteUrl] = useState<string | null>(null);
  const [inviteExpiresAt, setInviteExpiresAt] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);
  const [inviteError, setInviteError] = useState<string | null>(null);
  const [interviewOpen, setInterviewOpen] = useState(false);

  const candidate = useQuery({
    queryKey: ["candidate", candidateId],
    queryFn: () => api.getCandidate(candidateId)
  });
  const evaluation = useQuery({
    queryKey: ["evaluation", candidateId],
    queryFn: () => api.getEvaluation(candidateId),
    retry: (count, error) => {
      if (error instanceof ApiError && error.status === 404) {
        return false;
      }
      return count < 2;
    }
  });
  const audit = useQuery({
    queryKey: ["evaluation-audit", evaluation.data?.id],
    queryFn: () => api.getEvaluationAudit(evaluation.data!.id),
    enabled: Boolean(evaluation.data?.id)
  });
  const position = useQuery({
    queryKey: ["position", evaluation.data?.positionId ?? candidate.data?.positionId],
    queryFn: () => api.getPosition((evaluation.data?.positionId ?? candidate.data!.positionId)!),
    enabled: Boolean(evaluation.data?.positionId ?? candidate.data?.positionId)
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
      await queryClient.invalidateQueries({ queryKey: ["candidate", candidateId] });
    }
  });

  const invite = useMutation({
    mutationFn: async () => {
      const positionId = evaluation.data?.positionId ?? candidate.data?.positionId;
      if (!positionId) {
        throw new Error("missing_position");
      }
      return api.inviteInterview(candidateId, positionId);
    },
    onSuccess: async (result) => {
      const absolute = result.inviteUrl.startsWith("http")
        ? result.inviteUrl
        : `${window.location.origin}${result.inviteUrl}`;
      setInviteUrl(absolute);
      setInviteExpiresAt(result.expiresAt);
      setInviteError(null);
      setInterviewOpen(true);
      await queryClient.invalidateQueries({ queryKey: ["interview", candidateId] });
    },
    onError: () => setInviteError(t("errors.generic"))
  });

  const deleteInterview = useMutation({
    mutationFn: () => api.deleteInterview(candidateId),
    onSuccess: async () => {
      setInviteUrl(null);
      setInviteExpiresAt(null);
      await queryClient.invalidateQueries({ queryKey: ["interview", candidateId] });
    }
  });

  const deleteCandidate = useMutation({
    mutationFn: () => api.deleteCandidate(candidateId),
    onSuccess: async () => {
      const positionId = evaluation.data?.positionId ?? candidate.data?.positionId;
      await queryClient.invalidateQueries({ queryKey: ["candidates"] });
      await queryClient.invalidateQueries({ queryKey: ["positions"] });
      if (positionId) {
        await navigate({ to: "/positions/$positionId", params: { positionId } });
        return;
      }
      await navigate({ to: "/positions" });
    }
  });

  const copyInvite = async () => {
    if (!inviteUrl) {
      return;
    }
    try {
      await navigator.clipboard.writeText(inviteUrl);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1600);
    } catch {
      /* ignore */
    }
  };

  const criterionLabels = useMemo(() => {
    const map: Record<string, string> = {};
    for (const row of evaluation.data?.scores ?? []) {
      map[row.criterionId] = row.criterionName;
    }
    return map;
  }, [evaluation.data?.scores]);

  const missingCriteria = useMemo(
    () =>
      (evaluation.data?.scores ?? []).filter(
        (row) => row.score === null || row.evidenceStatus === "Insufficient"
      ),
    [evaluation.data?.scores]
  );

  const latestDecision = decisions.data?.[0];
  const hasEvaluation = Boolean(evaluation.data);
  const evalMissing =
    evaluation.isError && evaluation.error instanceof ApiError && evaluation.error.status === 404;
  const coveragePct = evaluation.data ? Math.round(evaluation.data.coverageRatio * 100) : null;

  return (
    <AppShell>
      <div className="flex flex-wrap items-center gap-2 text-sm text-muted">
        <Link to="/positions" className="font-medium text-brand-6 hover:underline">
          {t("nav.positions")}
        </Link>
        {position.data ? (
          <>
            <span aria-hidden="true">/</span>
            <Link
              to="/positions/$positionId"
              params={{ positionId: position.data.id }}
              className="font-medium text-brand-6 hover:underline"
            >
              {position.data.title}
            </Link>
          </>
        ) : null}
        <span aria-hidden="true">/</span>
        <span>{candidate.data?.displayName ?? t("evaluation.title")}</span>
      </div>

      <header className="flex flex-col gap-4 rounded-2xl border border-border bg-surface px-5 py-5 sm:flex-row sm:items-center sm:justify-between sm:px-6">
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <Badge tone="muted">{t("evaluation.humanReview")}</Badge>
            {latestDecision ? (
              <Badge>
                {outcomeKeys[latestDecision.outcome]
                  ? t(outcomeKeys[latestDecision.outcome])
                  : latestDecision.outcome}
              </Badge>
            ) : (
              <Badge tone="muted">{t("evaluation.awaitingDecision")}</Badge>
            )}
          </div>
          <h1 className="mt-2 truncate text-2xl font-extrabold tracking-tight sm:text-3xl">
            {candidate.data?.displayName ?? t("evaluation.title")}
          </h1>
          <p className="mt-1 text-sm text-muted">
            {position.data?.title ?? t("evaluation.candidateSubtitle")}
          </p>
        </div>
        <div className="flex shrink-0 flex-col items-stretch gap-2 sm:items-end">
          <ScoreBadge
            score={evaluation.data?.overallScore ?? candidate.data?.overallScore ?? null}
            label={
              evaluation.data?.overallScore == null && candidate.data?.overallScore == null
                ? t("score.unknown")
                : t("evaluation.overallOf100")
            }
          />
          {coveragePct !== null ? (
            <p className="text-xs text-muted">
              {t("evaluation.coverage")}: <span className="font-semibold tabular-nums">{coveragePct}%</span>
              {coveragePct < 80 ? (
                <span className="ml-1 text-danger">· {t("evaluation.coverageWarningShort")}</span>
              ) : null}
            </p>
          ) : null}
          <Button
            type="button"
            variant="ghost"
            size="sm"
            className="text-muted hover:text-danger"
            disabled={deleteCandidate.isPending}
            onClick={() => {
              if (!window.confirm(t("candidates.deleteConfirm"))) {
                return;
              }
              deleteCandidate.mutate();
            }}
          >
            {deleteCandidate.isPending ? t("candidates.deleting") : t("candidates.delete")}
          </Button>
        </div>
      </header>

      {hasEvaluation ? (
        <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_minmax(18rem,22rem)]">
          <div className="flex min-w-0 flex-col gap-5">
            <Card className="border-border/80">
              <CardHeader className="pb-2">
                <CardTitle className="text-base font-extrabold tracking-tight">
                  {t("evaluation.aiSummary")}
                </CardTitle>
                <p className="text-xs text-muted">{t("evaluation.summaryHint")}</p>
              </CardHeader>
              <CardContent>
                <p className="text-sm leading-7 text-foreground">
                  {evaluation.data!.summary?.trim() || t("evaluation.noScore")}
                </p>
              </CardContent>
            </Card>

            {missingCriteria.length > 0 ? (
              <div className="rounded-2xl border border-amber-200/80 bg-amber-50/50 px-4 py-3">
                <p className="text-sm font-bold text-amber-950">{t("evaluation.missingEvidence")}</p>
                <p className="mt-1 text-sm text-amber-900/80">
                  {missingCriteria.map((row) => row.criterionName).join(" · ")}
                </p>
              </div>
            ) : null}

            <ScoreBreakdownTable scores={evaluation.data!.scores} />

            {(evaluation.data!.followUps ?? []).length > 0 ? (
              <Card className="border-border/80">
                <CardHeader className="pb-2">
                  <CardTitle className="text-base font-extrabold tracking-tight">
                    {t("evaluation.followUps")}
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <ul className="flex flex-col gap-2">
                    {evaluation.data!.followUps.map((item) => (
                      <li
                        key={item}
                        className="rounded-xl border border-border/70 bg-brand-0/50 px-3 py-2.5 text-sm leading-6"
                      >
                        {item}
                      </li>
                    ))}
                  </ul>
                </CardContent>
              </Card>
            ) : null}

            <details
              className="rounded-2xl border border-border bg-surface open:pb-4"
              open={interviewOpen}
              onToggle={(event) => setInterviewOpen((event.target as HTMLDetailsElement).open)}
            >
              <summary className="cursor-pointer list-none px-5 py-4 text-sm font-extrabold tracking-tight marker:content-none [&::-webkit-details-marker]:hidden">
                <span className="flex items-center justify-between gap-3">
                  <span>{t("interview.inviteCardTitle")}</span>
                  <span className="text-xs font-semibold text-brand-6">{t("evaluation.toggleInterview")}</span>
                </span>
                <p className="mt-1 font-normal text-muted">{t("interview.inviteCardHint")}</p>
              </summary>
              <div className="flex flex-col gap-4 border-t border-border px-5 pt-4">
                <Button type="button" size="sm" className="w-fit" disabled={invite.isPending} onClick={() => invite.mutate()}>
                  {invite.isPending ? t("interview.inviting") : t("interview.invite")}
                </Button>
                {(inviteUrl || interview.data) ? (
                  <div className="rounded-xl border border-border bg-brand-0/40 px-4 py-3">
                    <p className="text-sm font-semibold">{t("interview.invited")}</p>
                    {inviteExpiresAt || interview.data?.expiresAt ? (
                      <p className="mt-1 text-xs text-muted">
                        {t("interview.expires")}:{" "}
                        {new Date(inviteExpiresAt ?? interview.data!.expiresAt!).toLocaleString()}
                      </p>
                    ) : null}
                    {inviteUrl ? (
                      <div className="mt-2 flex flex-wrap items-center gap-2">
                        <a
                          className="max-w-full truncate text-sm font-medium text-brand-6 underline-offset-2 hover:underline"
                          href={inviteUrl}
                          target="_blank"
                          rel="noreferrer"
                        >
                          {inviteUrl}
                        </a>
                        <Button type="button" variant="outline" size="sm" onClick={() => void copyInvite()}>
                          {copied ? t("interview.linkCopied") : t("interview.copyLink")}
                        </Button>
                      </div>
                    ) : interview.data ? (
                      <p className="mt-2 text-sm text-muted">
                        {t("interview.existingSession", {
                          status: interviewStatusLabel(interview.data.status, t)
                        })}
                      </p>
                    ) : null}
                    {interview.data ? (
                      <Button
                        type="button"
                        variant="ghost"
                        size="sm"
                        className="mt-2 w-fit text-danger"
                        disabled={deleteInterview.isPending}
                        onClick={() => {
                          if (!window.confirm(t("interview.deleteConfirm"))) {
                            return;
                          }
                          deleteInterview.mutate();
                        }}
                      >
                        {deleteInterview.isPending ? t("interview.deleting") : t("interview.delete")}
                      </Button>
                    ) : null}
                  </div>
                ) : null}
                {inviteError ? (
                  <p className="text-sm text-danger" role="alert">
                    {inviteError}
                  </p>
                ) : null}
              </div>
            </details>

            {interview.data ? (
              <div className="grid gap-5 lg:grid-cols-2">
                <AiDisclosureBanner />
                <GapCard gaps={interview.data.questions.map((question) => question.prompt)} />
                <InterviewTranscript turns={interview.data.turns} />
                <InterviewFramesGallery frames={interview.data.frames ?? []} />
              </div>
            ) : null}
          </div>

          <aside className="flex flex-col gap-4 xl:sticky xl:top-24 xl:self-start">
            <DecisionPanel
              busy={decide.isPending}
              inviteBusy={invite.isPending}
              onInviteInterview={() => {
                setInterviewOpen(true);
                invite.mutate();
              }}
              onSubmit={async (input) => {
                await decide.mutateAsync(input);
              }}
            />
            <RiskFlagList
              flags={[...(evaluation.data!.needsVerification ?? [])]}
              labelById={criterionLabels}
            />
            {audit.data ? (
              <EvaluationAuditPanel audit={audit.data} decisions={decisions.data ?? []} />
            ) : (
              <Card className="border-border/80">
                <CardHeader className="pb-2">
                  <CardTitle className="text-base font-extrabold tracking-tight">
                    {t("evaluation.audit")}
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <p className="text-sm text-muted">{t("evaluation.noAudit")}</p>
                </CardContent>
              </Card>
            )}
          </aside>
        </div>
      ) : (
        <Card>
          <CardContent className="flex flex-col items-center gap-5 px-6 py-12 text-center text-sm text-muted">
            <p>
              {evaluation.isLoading
                ? t("evaluation.loading")
                : evalMissing
                  ? t("evaluation.awaitingCv")
                  : t("evaluation.noScore")}
            </p>
            {candidate.data?.positionId ? (
              <Button asChild variant="outline">
                <Link to="/positions/$positionId" params={{ positionId: candidate.data.positionId }}>
                  {t("evaluation.backToCandidates")}
                </Link>
              </Button>
            ) : null}
          </CardContent>
        </Card>
      )}
    </AppShell>
  );
}

function interviewStatusLabel(status: string, t: (key: string) => string): string {
  const map: Record<string, string> = {
    invited: "interview.statusInvited",
    started: "interview.statusStarted",
    completed: "interview.statusCompleted",
    cancelled: "interview.statusCancelled",
    paused: "interview.statusPaused"
  };
  return map[status] ? t(map[status]) : status;
}
