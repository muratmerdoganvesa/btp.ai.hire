import { ApiError } from "@hirelens/api-client";
import { Badge, Button, Card, CardContent, CardHeader, CardTitle, ScoreBadge } from "@hirelens/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useParams } from "@tanstack/react-router";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "../api";
import { AppShell } from "../components/app-shell";
import { EvaluationAuditPanel } from "../components/evaluation-audit-panel";
import { ScoreFormulaPanel } from "../components/score-formula-panel";
import { DecisionPanel } from "../components/decision-panel";
import { EvidencePanel } from "../components/evidence-panel";
import { RiskFlagList } from "../components/risk-flag-list";
import { ScoreBreakdownTable } from "../components/score-breakdown-table";
import { AiDisclosureBanner } from "../components/ai-disclosure-banner";
import { GapCard } from "../components/gap-card";
import { InterviewTranscript } from "../components/interview-transcript";
import { InterviewFramesGallery } from "../components/interview-frames-gallery";
import { SourceHighlighter } from "../components/source-highlighter";

export function EvaluationPage() {
  const { t } = useTranslation();
  const { candidateId } = useParams({ from: "/candidates/$candidateId" });
  const queryClient = useQueryClient();
  const [quote, setQuote] = useState<string | null>(null);
  const [inviteUrl, setInviteUrl] = useState<string | null>(null);
  const [inviteExpiresAt, setInviteExpiresAt] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);
  const [inviteError, setInviteError] = useState<string | null>(null);

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
    queryKey: ["position", evaluation.data?.positionId],
    queryFn: () => api.getPosition(evaluation.data!.positionId),
    enabled: Boolean(evaluation.data?.positionId)
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
      const absolute =
        result.inviteUrl.startsWith("http")
          ? result.inviteUrl
          : `${window.location.origin}${result.inviteUrl}`;
      setInviteUrl(absolute);
      setInviteExpiresAt(result.expiresAt);
      setInviteError(null);
      await queryClient.invalidateQueries({ queryKey: ["interview", candidateId] });
    },
    onError: () => setInviteError(t("errors.generic"))
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

  const sourceText = (evaluation.data?.scores ?? [])
    .flatMap((score) => score.evidence.map((item) => item.quote))
    .join("\n");

  const latestDecision = decisions.data?.[0];
  const hasEvaluation = Boolean(evaluation.data);
  const evalMissing =
    evaluation.isError && evaluation.error instanceof ApiError && evaluation.error.status === 404;

  return (
    <AppShell>
      <div className="flex flex-wrap items-center gap-2 text-sm text-muted">
        <Link to="/positions" className="font-medium text-brand hover:text-brand-7">
          {t("nav.positions")}
        </Link>
        {position.data ? (
          <>
            <span aria-hidden="true">/</span>
            <Link
              to="/positions/$positionId"
              params={{ positionId: position.data.id }}
              className="font-medium text-brand hover:text-brand-7"
            >
              {position.data.title}
            </Link>
          </>
        ) : null}
        <span aria-hidden="true">/</span>
        <span>{candidate.data?.displayName ?? t("evaluation.title")}</span>
      </div>

      <header className="flex flex-col gap-5 rounded-2xl border border-border bg-surface p-6 shadow-card sm:flex-row sm:items-start sm:justify-between sm:gap-6 sm:p-7">
        <div className="min-w-0 flex-1">
          <div className="flex flex-wrap items-center gap-2">
            <Badge tone="muted">{t("evaluation.humanReview")}</Badge>
            {latestDecision ? <Badge>{latestDecision.outcome}</Badge> : null}
            {evaluation.data?.status ? <Badge tone="muted">{evaluation.data.status}</Badge> : null}
          </div>
          <h1 className="mt-3 text-3xl font-extrabold tracking-tight">
            {candidate.data?.displayName ?? t("evaluation.title")}
          </h1>
          <p className="mt-1 text-sm text-muted">
            {position.data?.title ?? t("evaluation.candidateSubtitle")}
          </p>
        </div>
        <div className="flex w-full shrink-0 flex-col gap-3 sm:w-auto sm:items-end">
          <div className="flex flex-col items-end gap-1">
            <ScoreBadge
              score={evaluation.data?.overallScore ?? candidate.data?.overallScore ?? null}
              label={
                evaluation.data?.overallScore == null && candidate.data?.overallScore == null
                  ? t("score.unknown")
                  : t("evaluation.overallOf100")
              }
            />
            {evaluation.data && evaluation.data.coverageRatio < 0.8 ? (
              <p className="max-w-xs text-right text-xs text-muted" title={t("evaluation.coverageWarning")}>
                ⚠ {t("evaluation.coverageWarning")}
              </p>
            ) : null}
          </div>
          <Button
            type="button"
            variant="outline"
            className="w-full sm:w-auto"
            disabled={invite.isPending || !hasEvaluation}
            onClick={() => invite.mutate()}
          >
            {invite.isPending ? t("interview.inviting") : t("interview.invite")}
          </Button>
        </div>
      </header>

      <Card className="border-brand-3/40 bg-gradient-to-br from-surface via-surface to-brand-0/70">
        <CardHeader>
          <CardTitle className="font-display text-xl">{t("interview.inviteCardTitle")}</CardTitle>
          <p className="text-sm text-muted">{t("interview.inviteCardHint")}</p>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <p className="text-sm leading-relaxed text-muted">{t("interview.inviteCardBody")}</p>

          {(inviteUrl || interview.data) ? (
            <div className="rounded-xl border border-border bg-white/80 px-4 py-3">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <p className="text-sm font-semibold text-foreground">{t("interview.invited")}</p>
                {inviteExpiresAt || interview.data?.expiresAt ? (
                  <p className="text-xs text-muted">
                    {t("interview.expires")}:{" "}
                    {new Date(inviteExpiresAt ?? interview.data!.expiresAt!).toLocaleString()}
                  </p>
                ) : null}
              </div>
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
              ) : (
                <p className="mt-2 text-sm text-muted">{t("interview.existingSession", { status: interview.data?.status })}</p>
              )}
            </div>
          ) : null}

          {inviteError ? (
            <p className="text-sm text-danger" role="alert">
              {inviteError}
            </p>
          ) : null}
        </CardContent>
      </Card>

      {hasEvaluation ? (
        <div className="grid gap-6 xl:grid-cols-[minmax(0,1.6fr)_minmax(0,22rem)]">
          <div className="flex flex-col gap-6">
            <ScoreBreakdownTable scores={evaluation.data!.scores} onSelect={setQuote} />
            <div className="grid gap-6 lg:grid-cols-2">
              <EvidencePanel scores={evaluation.data!.scores} onSelect={(selected) => setQuote(selected)} />
              <SourceHighlighter text={sourceText} quote={quote} />
            </div>
          </div>

          <aside className="flex flex-col gap-4 xl:sticky xl:top-24 xl:self-start">
            <ScoreFormulaPanel evaluation={evaluation.data!} />

            <Card className="border-border/80 bg-surface/95">
              <CardHeader>
                <CardTitle className="font-display text-xl">{t("evaluation.aiSummary")}</CardTitle>
              </CardHeader>
              <CardContent>
                <p className="text-sm leading-6 text-foreground">
                  {evaluation.data!.summary?.trim() || t("evaluation.noScore")}
                </p>
              </CardContent>
            </Card>

            <Card className="border-border/80 bg-surface/95">
              <CardHeader>
                <CardTitle className="font-display text-xl">{t("evaluation.followUps")}</CardTitle>
              </CardHeader>
              <CardContent>
                {(evaluation.data!.followUps ?? []).length === 0 ? (
                  <p className="text-sm text-muted">{t("evaluation.noFollowUps")}</p>
                ) : (
                  <ul className="flex flex-col gap-2">
                    {evaluation.data!.followUps.map((item) => (
                      <li
                        key={item}
                        className="rounded-lg border border-border/70 bg-brand-1/40 px-3 py-2 text-sm leading-6"
                      >
                        {item}
                      </li>
                    ))}
                  </ul>
                )}
              </CardContent>
            </Card>

            <RiskFlagList flags={[...(evaluation.data!.needsVerification ?? [])]} />

            <Card>
              <CardHeader>
                <CardTitle>{t("evaluation.recruiterActions")}</CardTitle>
              </CardHeader>
              <CardContent className="flex flex-col gap-3">
                <Button
                  type="button"
                  disabled={decide.isPending}
                  onClick={() =>
                    void decide.mutateAsync({
                      outcome: "advance",
                      rationale: t("decision.shortlistRationale")
                    })
                  }
                >
                  {t("decision.shortlist")}
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  disabled={invite.isPending || !hasEvaluation}
                  onClick={() => invite.mutate()}
                >
                  {t("decision.moreInterview")}
                </Button>
                <Button
                  type="button"
                  variant="outline"
                  disabled={decide.isPending}
                  onClick={() =>
                    void decide.mutateAsync({
                      outcome: "hold",
                      rationale: t("decision.holdRationale")
                    })
                  }
                >
                  {t("decision.hold")}
                </Button>
              </CardContent>
            </Card>
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

      {interview.data ? (
        <div className="grid gap-6 lg:grid-cols-2">
          <AiDisclosureBanner />
          <GapCard gaps={interview.data.questions.map((question) => question.prompt)} />
          <InterviewTranscript turns={interview.data.turns} />
          <InterviewFramesGallery frames={interview.data.frames ?? []} />
          {interview.data.summary ? (
            <Card>
              <CardHeader>
                <CardTitle>{t("interview.title")}</CardTitle>
              </CardHeader>
              <CardContent className="text-sm leading-6">{interview.data.summary}</CardContent>
            </Card>
          ) : null}
        </div>
      ) : null}

      <div className="grid gap-6 lg:grid-cols-2">
        <DecisionPanel
          onSubmit={async (input) => {
            await decide.mutateAsync(input);
          }}
          busy={decide.isPending}
        />
        {audit.data ? (
          <EvaluationAuditPanel audit={audit.data} decisions={decisions.data ?? []} />
        ) : null}
      </div>
    </AppShell>
  );
}
