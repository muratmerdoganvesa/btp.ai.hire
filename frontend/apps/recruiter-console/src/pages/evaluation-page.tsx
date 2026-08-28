import { ApiError, type Evaluation } from "@hirelens/api-client";
import { Badge, Button, Card, CardContent, CardHeader, CardTitle, ScoreBadge, cn } from "@hirelens/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate, useParams } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "../api";
import { DecisionPanel } from "../components/decision-panel";
import { EvaluationAuditPanel } from "../components/evaluation-audit-panel";
import { InterviewFramesGallery } from "../components/interview-frames-gallery";
import { InterviewTranscript } from "../components/interview-transcript";
import { OfferPanel } from "../components/offer-panel";
import { PageBody, PageHero } from "../components/page-hero";
import { RiskFlagList } from "../components/risk-flag-list";
import { ScoreBreakdownTable } from "../components/score-breakdown-table";

export function EvaluationPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { candidateId } = useParams({ from: "/_app/candidates/$candidateId" });
  const queryClient = useQueryClient();
  const [inviteUrl, setInviteUrl] = useState<string | null>(null);
  const [inviteExpiresAt, setInviteExpiresAt] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);
  const [inviteError, setInviteError] = useState<string | null>(null);
  const [evaluateError, setEvaluateError] = useState<string | null>(null);
  const [interviewOpen, setInterviewOpen] = useState(false);
  const [tab, setTab] = useState<"cv" | "interview">("cv");

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
      await queryClient.invalidateQueries({ queryKey: ["candidates-board"] });
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
      setInviteUrl(toPublicInterviewUrl(result.inviteUrl));
      setInviteExpiresAt(result.expiresAt);
      setInviteError(null);
      setInterviewOpen(true);
      setTab("interview");
      await queryClient.invalidateQueries({ queryKey: ["interview", candidateId] });
    },
    onError: (err) => {
      const message = err instanceof Error ? err.message : "";
      setInviteError(message ? `${t("errors.generic")} (${message.replace(/^http_\d+:/, "")})` : t("errors.generic"));
    }
  });

  const deleteInterview = useMutation({
    mutationFn: () => api.deleteInterview(candidateId),
    onSuccess: async () => {
      setInviteUrl(null);
      setInviteExpiresAt(null);
      setEvaluateError(null);
      await queryClient.invalidateQueries({ queryKey: ["interview", candidateId] });
    }
  });

  const evaluateInterview = useMutation({
    mutationFn: () => api.evaluateInterview(candidateId),
    onSuccess: async () => {
      setEvaluateError(null);
      await queryClient.invalidateQueries({ queryKey: ["interview", candidateId] });
      await queryClient.invalidateQueries({ queryKey: ["evaluation", candidateId] });
      await queryClient.invalidateQueries({ queryKey: ["candidates-board"] });
    },
    onError: (err) => {
      const message = err instanceof Error ? err.message : "";
      setEvaluateError(
        message ? `${t("errors.generic")} (${message.replace(/^http_\d+:/, "").replace(/^validation:/, "")})` : t("errors.generic")
      );
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

  const shareInviteOnWhatsApp = async () => {
    let url = inviteUrl;
    if (!url) {
      try {
        const result = await invite.mutateAsync();
        url = toPublicInterviewUrl(result.inviteUrl);
      } catch {
        return;
      }
    }

    const name = candidate.data?.displayName?.trim();
    const text = t("interview.whatsappMessage", {
      name: name ? ` ${name}` : "",
      position: position.data?.title?.trim() || t("interview.title"),
      url
    });
    window.open(`https://wa.me/?text=${encodeURIComponent(text)}`, "_blank", "noopener,noreferrer");
  };

  const criterionLabels = useMemo(() => {
    const map: Record<string, string> = {};
    for (const row of evaluation.data?.scores ?? []) {
      map[row.criterionId] = row.criterionName;
    }
    return map;
  }, [evaluation.data?.scores]);

  const latestDecision = useMemo(() => {
    const list = decisions.data ?? [];
    if (list.length === 0) {
      return null;
    }
    return [...list].sort(
      (a, b) => new Date(b.decidedAt).getTime() - new Date(a.decidedAt).getTime()
    )[0]!;
  }, [decisions.data]);

  const decisionOutcome = latestDecision?.outcome ?? null;
  const isRejected = decisionOutcome === "reject";
  const isAdvanced = decisionOutcome === "advance";
  const isHeld = decisionOutcome === "hold";

  const hasEvaluation = Boolean(evaluation.data);
  const evalMissing =
    evaluation.isError && evaluation.error instanceof ApiError && evaluation.error.status === 404;
  const coveragePct = evaluation.data ? Math.round(evaluation.data.coverageRatio * 100) : null;
  const summary = evaluation.data ? recruiterSummaryParts(evaluation.data) : null;
  const interviewEvidenceScores = (evaluation.data?.scores ?? []).filter((row) =>
    row.evidence.some((item) => item.source.trim().toLowerCase() === "interview")
  );

  return (
    <>
      <PageHero
        kicker={position.data?.title ?? t("evaluation.title")}
        title={candidate.data?.displayName ?? t("evaluation.title")}
        actions={
          <Button
            type="button"
            variant="outline"
            size="sm"
            className="!border-white/40 !bg-white/10 !text-white hover:!bg-white/20 hover:!text-white"
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
        }
      />
      <PageBody>
      <div className="flex flex-wrap items-center justify-between gap-3">
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
        <div className="flex flex-col items-end gap-1">
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
        </div>
      </div>

      {isRejected ? (
        <div
          className="flex flex-col gap-1 rounded-2xl border border-rose-300 bg-rose-50 px-5 py-4 text-rose-950 sm:flex-row sm:items-center sm:justify-between"
          role="status"
        >
          <div>
            <p className="text-sm font-extrabold tracking-tight">{t("evaluation.rejectedBannerTitle")}</p>
            <p className="mt-0.5 text-sm text-rose-900/80">
              {latestDecision?.rationale?.trim()
                ? latestDecision.rationale
                : t("evaluation.rejectedBannerBody")}
            </p>
            {latestDecision ? (
              <p className="mt-1 text-xs text-rose-800/70">
                {t("evaluation.decidedAt", {
                  date: new Date(latestDecision.decidedAt).toLocaleString()
                })}
              </p>
            ) : null}
          </div>
          <span className="shrink-0 self-start rounded-full bg-rose-600 px-3 py-1 text-xs font-bold uppercase tracking-wide text-white">
            {t("decision.reject")}
          </span>
        </div>
      ) : null}

      {isAdvanced ? (
        <div
          className="rounded-2xl border border-emerald-300 bg-emerald-50 px-5 py-3 text-emerald-950"
          role="status"
        >
          <p className="text-sm font-extrabold">{t("evaluation.advancedBannerTitle")}</p>
          {latestDecision?.rationale ? (
            <p className="mt-0.5 text-sm text-emerald-900/80">{latestDecision.rationale}</p>
          ) : null}
        </div>
      ) : null}

      {isHeld ? (
        <div
          className="rounded-2xl border border-amber-300 bg-amber-50 px-5 py-3 text-amber-950"
          role="status"
        >
          <p className="text-sm font-extrabold">{t("evaluation.heldBannerTitle")}</p>
          {latestDecision?.rationale ? (
            <p className="mt-0.5 text-sm text-amber-900/80">{latestDecision.rationale}</p>
          ) : null}
        </div>
      ) : null}

      <div className="flex flex-wrap items-center gap-2">
        {isRejected ? (
          <Badge tone="danger">{t("evaluation.statusRejected")}</Badge>
        ) : isAdvanced ? (
          <Badge className="!bg-emerald-100 !text-emerald-800">{t("evaluation.statusAdvanced")}</Badge>
        ) : isHeld ? (
          <Badge className="!bg-amber-100 !text-amber-900">{t("evaluation.statusHeld")}</Badge>
        ) : (
          <Badge tone="muted">{t("evaluation.awaitingDecision")}</Badge>
        )}
        {!isRejected && !isAdvanced && !isHeld ? (
          <Badge tone="muted">{t("evaluation.humanReview")}</Badge>
        ) : null}
      </div>

      {hasEvaluation ? (
        <div className="grid gap-6 xl:grid-cols-[minmax(0,1fr)_minmax(18rem,22rem)]">
          <div className="flex min-w-0 flex-col gap-5">
            <div
              className="flex rounded-xl border border-border bg-white p-0.5"
              role="tablist"
              aria-label={t("evaluation.title")}
            >
              <button
                type="button"
                role="tab"
                aria-selected={tab === "cv"}
                className={cn(
                  "h-9 flex-1 rounded-lg px-3 text-sm font-bold transition-colors",
                  tab === "cv" ? "bg-brand-6 text-white" : "text-muted hover:bg-brand-0 hover:text-foreground"
                )}
                onClick={() => setTab("cv")}
              >
                {t("evaluation.tabCv")}
              </button>
              <button
                type="button"
                role="tab"
                aria-selected={tab === "interview"}
                className={cn(
                  "h-9 flex-1 rounded-lg px-3 text-sm font-bold transition-colors",
                  tab === "interview" ? "bg-brand-6 text-white" : "text-muted hover:bg-brand-0 hover:text-foreground"
                )}
                onClick={() => setTab("interview")}
              >
                {t("evaluation.tabInterview")}
              </button>
            </div>

            {tab === "cv" ? (
              <>
            <Card className="border-border/80">
              <CardHeader className="pb-2">
                <CardTitle className="text-base font-extrabold tracking-tight">
                  {t("evaluation.aiSummary")}
                </CardTitle>
                <p className="text-xs text-muted">{t("evaluation.summaryHint")}</p>
              </CardHeader>
              <CardContent className="flex flex-col gap-4">
                <p className="text-lg font-extrabold tracking-tight text-foreground">
                  {evaluation.data!.overallScore == null
                    ? t("evaluation.noScore")
                    : t("evaluation.summaryScoreLine", { score: evaluation.data!.overallScore })}
                  {coveragePct != null ? (
                    <span className="ml-2 text-sm font-semibold text-muted">
                      {t("evaluation.summaryCoverageLine", { coverage: coveragePct })}
                    </span>
                  ) : null}
                </p>
                {summary?.note ? (
                  <p className="text-sm leading-6 text-muted">{summary.note}</p>
                ) : null}
                <div className="grid gap-3 sm:grid-cols-2">
                  <div className="rounded-xl border border-emerald-200/80 bg-emerald-50/50 px-3.5 py-3">
                    <p className="text-[0.7rem] font-extrabold uppercase tracking-wide text-emerald-800">
                      {t("evaluation.summaryStrengthsTitle")}
                    </p>
                    {summary && summary.strengths.length > 0 ? (
                      <ul className="mt-2 flex flex-col gap-1.5">
                        {summary.strengths.map((item) => (
                          <li key={item.id} className="text-sm font-semibold leading-5 text-emerald-950">
                            {item.name}
                          </li>
                        ))}
                      </ul>
                    ) : (
                      <p className="mt-2 text-sm text-emerald-900/70">{t("evaluation.summaryNoStrengths")}</p>
                    )}
                  </div>
                  <div className="rounded-xl border border-amber-200/80 bg-amber-50/60 px-3.5 py-3">
                    <p className="text-[0.7rem] font-extrabold uppercase tracking-wide text-amber-800">
                      {t("evaluation.summaryGapsTitle")}
                    </p>
                    {summary && summary.gaps.length > 0 ? (
                      <ul className="mt-2 flex flex-col gap-1.5">
                        {summary.gaps.map((item) => (
                          <li key={item.id} className="text-sm font-medium leading-5 text-amber-950/90">
                            {item.name}
                          </li>
                        ))}
                      </ul>
                    ) : (
                      <p className="mt-2 text-sm text-amber-900/70">{t("evaluation.summaryNoGaps")}</p>
                    )}
                  </div>
                </div>
              </CardContent>
            </Card>

            <ScoreBreakdownTable scores={evaluation.data!.scores} evidenceSource="cv" />
              </>
            ) : (
              <>
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
                        <Button type="button" variant="outline" size="sm" onClick={() => void shareInviteOnWhatsApp()}>
                          {t("interview.sendWhatsApp")}
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
              <div className="flex flex-col gap-5">
                {interview.data.status === "completed" ? (
                  <div className="flex flex-col gap-3 rounded-2xl border border-border bg-surface px-5 py-4">
                    <div className="flex flex-wrap items-center justify-between gap-3">
                      <div>
                        <p className="text-sm font-extrabold tracking-tight">{t("interview.evaluate")}</p>
                        <p className="mt-1 text-sm text-muted">{t("interview.evaluateHint")}</p>
                      </div>
                      <div className="flex flex-wrap items-center gap-2">
                        <ScoreBadge
                          score={interview.data.interviewScore}
                          label={
                            interview.data.interviewScore != null
                              ? t("evaluation.overallOf100")
                              : interview.data.summary
                                ? t("score.insufficient")
                                : t("interview.scorePending")
                          }
                        />
                        <Button
                          type="button"
                          size="sm"
                          disabled={evaluateInterview.isPending}
                          onClick={() => evaluateInterview.mutate()}
                        >
                          {evaluateInterview.isPending
                            ? t("interview.evaluating")
                            : interview.data.interviewScore != null || interview.data.summary
                              ? t("interview.evaluateAgain")
                              : t("interview.evaluate")}
                        </Button>
                      </div>
                    </div>
                    {interview.data.summary ? (
                      <p className="text-sm leading-6 text-foreground">{interview.data.summary}</p>
                    ) : null}
                    {interview.data.interviewScore == null && interview.data.summary ? (
                      <p className="text-sm text-muted">{t("interview.scoreInsufficientHint")}</p>
                    ) : null}
                    {evaluateError ? (
                      <p className="text-sm text-danger" role="alert">
                        {evaluateError}
                      </p>
                    ) : null}
                  </div>
                ) : null}

                <div className="flex flex-col gap-5">
                  <Card>
                    <CardHeader>
                      <CardTitle>{t("interview.askedQuestions")}</CardTitle>
                    </CardHeader>
                    <CardContent>
                      {interview.data.questions.length === 0 ? (
                        <p className="text-sm text-muted">{t("interviewsBoard.noQuestions")}</p>
                      ) : (
                        <ol className="flex flex-col gap-2">
                          {interview.data.questions.map((question, index) => (
                            <li
                              key={`${question.criterionId}-${index}`}
                              className="rounded-xl border border-border/70 bg-brand-0/50 px-3 py-2.5 text-sm leading-6"
                            >
                              <p className="text-xs font-bold uppercase tracking-wide text-muted">
                                {t("interview.questionLabel", {
                                  current: index + 1,
                                  total: interview.data!.questions.length
                                })}
                              </p>
                              <p className="mt-1">{question.prompt}</p>
                            </li>
                          ))}
                        </ol>
                      )}
                    </CardContent>
                  </Card>
                  <div className="grid gap-5 lg:grid-cols-2">
                    <InterviewTranscript turns={interview.data.turns} />
                    <InterviewFramesGallery frames={interview.data.frames ?? []} />
                  </div>
                </div>
              </div>
            ) : null}

            {(evaluation.data!.followUps ?? []).length > 0 ? (
              <Card className="border-border/80">
                <CardHeader className="pb-2">
                  <CardTitle className="text-base font-extrabold tracking-tight">
                    {t("evaluation.followUps")}
                  </CardTitle>
                </CardHeader>
                <CardContent>
                  <ul className="flex flex-col gap-2">
                    {evaluation.data!.followUps.map((item, index) => (
                      <li
                        key={`${index}-${item.slice(0, 24)}`}
                        className="rounded-xl border border-border/70 bg-brand-0/50 px-3 py-2.5 text-sm leading-6"
                      >
                        {item}
                      </li>
                    ))}
                  </ul>
                </CardContent>
              </Card>
            ) : null}

            {interviewEvidenceScores.length > 0 ? (
              <ScoreBreakdownTable scores={interviewEvidenceScores} evidenceSource="interview" />
            ) : null}
              </>
            )}
          </div>

          <aside className="flex flex-col gap-4 xl:sticky xl:top-24 xl:self-start">
            <DecisionPanel
              busy={decide.isPending}
              inviteBusy={invite.isPending}
              onInviteInterview={() => {
                setTab("interview");
                setInterviewOpen(true);
                invite.mutate();
              }}
              onInviteWhatsApp={() => {
                setTab("interview");
                setInterviewOpen(true);
                void shareInviteOnWhatsApp();
              }}
              onSubmit={async (input) => {
                await decide.mutateAsync(input);
              }}
            />
            <OfferPanel candidateId={candidateId} />
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
      </PageBody>
    </>
  );
}

function recruiterSummaryParts(evaluation: Evaluation): {
  strengths: { id: string; name: string }[];
  gaps: { id: string; name: string }[];
  note: string | null;
} {
  const stored = evaluation.summary?.trim() ?? "";
  const placeholder =
    stored.length === 0
    || /Evidence-bound scores are ready/i.test(stored)
    || /Insufficient evidence for an overall score/i.test(stored)
    || /Skor \d+ \/ 100/.test(stored)
    || /Score \d+ \/ 100/.test(stored);

  const strengths = evaluation.scores
    .filter((row) => row.score != null && row.evidenceStatus !== "Insufficient")
    .slice()
    .sort((a, b) => (b.score ?? 0) - (a.score ?? 0))
    .slice(0, 5)
    .map((row) => ({ id: row.criterionId, name: row.criterionName }));

  const strengthIds = new Set(strengths.map((row) => row.id));
  const gaps = evaluation.scores
    .filter(
      (row) =>
        !strengthIds.has(row.criterionId) &&
        (row.score == null || row.evidenceStatus === "Insufficient")
    )
    .map((row) => ({ id: row.criterionId, name: row.criterionName }));

  return {
    strengths,
    gaps,
    note: placeholder ? null : stored
  };
}

function toPublicInterviewUrl(inviteUrl: string): string {
  const origin = window.location.origin;
  try {
    if (inviteUrl.startsWith("http://") || inviteUrl.startsWith("https://")) {
      const parsed = new URL(inviteUrl);
      return `${origin}${parsed.pathname}${parsed.search}`;
    }
  } catch {
    /* relative path */
  }

  const path = inviteUrl.startsWith("/") ? inviteUrl : `/interview/${inviteUrl}`;
  return `${origin}${path}`;
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
