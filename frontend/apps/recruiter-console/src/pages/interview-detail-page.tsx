import { Badge, Button, ScoreBadge } from "@hirelens/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useParams } from "@tanstack/react-router";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { ApiError } from "@hirelens/api-client";
import { api } from "../api";
import { InterviewFramesGallery } from "../components/interview-frames-gallery";
import { InterviewTranscript } from "../components/interview-transcript";
import { PageBody, PageHero } from "../components/page-hero";

export function InterviewDetailPage() {
  const { t } = useTranslation();
  const { sessionId } = useParams({ from: "/_app/interviews/$sessionId" });
  const queryClient = useQueryClient();
  const [evaluateError, setEvaluateError] = useState<string | null>(null);

  const session = useQuery({
    queryKey: ["interview-session", sessionId],
    queryFn: () => api.getInterviewSessionById(sessionId)
  });

  const data = session.data;
  const canEvaluate =
    Boolean(data) &&
    data!.questions.length > 0 &&
    data!.questions.every((question) =>
      data!.turns.some((turn) => turn.questionId === question.id && turn.role === "candidate")
    );

  const evaluate = useMutation({
    mutationFn: () => api.evaluateInterviewSession(sessionId),
    onSuccess: async () => {
      setEvaluateError(null);
      await queryClient.invalidateQueries({ queryKey: ["interview-session", sessionId] });
      await queryClient.invalidateQueries({ queryKey: ["interview", data?.candidateId] });
      await queryClient.invalidateQueries({ queryKey: ["interviews", data?.candidateId] });
      await queryClient.invalidateQueries({ queryKey: ["evaluation", data?.candidateId] });
      await queryClient.invalidateQueries({ queryKey: ["interviews-board"] });
    },
    onError: (err) => {
      const message = err instanceof Error ? err.message : "";
      const detail = err instanceof ApiError ? message : message.replace(/^http_\d+:/, "").replace(/^validation:/, "");
      setEvaluateError(detail ? `${t("errors.generic")} (${detail})` : t("errors.generic"));
    }
  });

  return (
    <>
      <PageHero
        kicker={data?.positionTitle ?? t("interviewsBoard.title")}
        title={data?.candidateName ?? t("interviewsBoard.detailTitle")}
        actions={
          <div className="flex flex-wrap items-center gap-2">
            {data ? (
              <ScoreBadge
                score={data.interviewScore}
                label={
                  data.interviewScore != null
                    ? t("evaluation.overallOf100")
                    : data.summary
                      ? t("score.insufficient")
                      : t("score.unknown")
                }
              />
            ) : null}
            <Button asChild variant="outline" size="sm" className="!border-white/40 !bg-white/10 !text-white hover:!bg-white/20 hover:!text-white">
              <Link to="/interviews">{t("interviewsBoard.back")}</Link>
            </Button>
          </div>
        }
      />
      <PageBody className="gap-4 pb-2">
        {session.isLoading ? (
          <p className="text-sm text-muted">{t("interviewsBoard.loading")}</p>
        ) : session.isError || !data ? (
          <p className="text-sm text-danger">{t("interviewsBoard.notFound")}</p>
        ) : (
          <>
            <section className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-border bg-surface px-4 py-3 shadow-card">
              <div className="flex flex-wrap items-center gap-2">
                <Badge className="!rounded-md">{statusLabel(data.status, t)}</Badge>
                <span className="text-sm text-muted">
                  {t("interviewsBoard.sentAt")}: {data.createdAt ? new Date(data.createdAt).toLocaleString() : "—"}
                </span>
                {data.expiresAt ? (
                  <span className="text-sm text-muted">
                    {t("interview.expires")}: {new Date(data.expiresAt).toLocaleString()}
                  </span>
                ) : null}
              </div>
              <div className="flex flex-wrap gap-2">
                {canEvaluate ? (
                  <Button
                    type="button"
                    size="sm"
                    disabled={evaluate.isPending}
                    onClick={() => evaluate.mutate()}
                  >
                    {evaluate.isPending
                      ? t("interview.evaluating")
                      : data.summary || data.interviewScore != null
                        ? t("interview.evaluateAgain")
                        : t("interview.evaluate")}
                  </Button>
                ) : null}
                <Button asChild size="sm" variant="outline">
                  <Link to="/candidates/$candidateId" params={{ candidateId: data.candidateId }}>
                    {t("interviewsBoard.openCandidate")}
                  </Link>
                </Button>
                <Button asChild size="sm" variant="outline">
                  <Link to="/positions/$positionId" params={{ positionId: data.positionId }}>
                    {t("interviewsBoard.openPosition")}
                  </Link>
                </Button>
              </div>
            </section>

            {evaluateError ? (
              <p className="text-sm text-danger" role="alert">
                {evaluateError}
              </p>
            ) : null}

            {data.summary ? (
              <section className="rounded-2xl border border-border bg-surface p-4 shadow-card">
                <h2 className="text-sm font-extrabold">{t("interviewsBoard.summary")}</h2>
                <p className="mt-2 text-sm leading-relaxed text-muted">{data.summary}</p>
              </section>
            ) : null}

            <section className="grid gap-4 lg:grid-cols-2">
              <div className="rounded-2xl border border-border bg-surface p-4 shadow-card">
                <h2 className="text-sm font-extrabold">{t("interviewsBoard.questions")}</h2>
                {data.questions.length === 0 ? (
                  <p className="mt-3 text-sm text-muted">{t("interviewsBoard.noQuestions")}</p>
                ) : (
                  <ol className="mt-3 flex flex-col gap-2">
                    {data.questions
                      .slice()
                      .sort((a, b) => a.order - b.order)
                      .map((question, index) => (
                        <li key={question.id} className="rounded-xl border border-border bg-white px-3 py-2.5 text-sm">
                          <p className="text-xs font-bold uppercase tracking-wide text-muted">
                            {t("interview.questionLabel", { current: index + 1, total: data.questions.length })}
                          </p>
                          <p className="mt-1 font-medium text-foreground">{question.prompt}</p>
                        </li>
                      ))}
                  </ol>
                )}
              </div>
              <div className="min-w-0">
                {data.turns.length === 0 ? (
                  <div className="rounded-2xl border border-border bg-surface p-4 shadow-card">
                    <h2 className="text-sm font-extrabold">{t("interview.transcript")}</h2>
                    <p className="mt-3 text-sm text-muted">{t("interviewsBoard.noTranscript")}</p>
                  </div>
                ) : (
                  <InterviewTranscript turns={data.turns} />
                )}
              </div>
            </section>

            <InterviewFramesGallery frames={data.frames ?? []} />
          </>
        )}
      </PageBody>
    </>
  );
}

function statusLabel(status: string, t: (key: string) => string): string {
  const map: Record<string, string> = {
    invited: "interview.statusInvited",
    disclosed: "interview.statusDisclosed",
    in_progress: "interview.statusStarted",
    started: "interview.statusStarted",
    paused: "interview.statusPaused",
    completed: "interview.statusCompleted",
    cancelled: "interview.statusCancelled"
  };
  return map[status] ? t(map[status]) : status;
}
