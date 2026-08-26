import { Button } from "@hirelens/ui";
import { Link, useParams } from "@tanstack/react-router";
import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { PublicApi } from "@hirelens/api-client";
import { VideoAnswerRecorder } from "../../components/video-answer-recorder";

const publicApi = new PublicApi("");

type Session = {
  status: string;
  disclosureAccepted: boolean;
  questions: { criterionId: string; prompt: string }[];
  turns: { role: string; text: string }[];
  summary: string | null;
  expiresAt?: string | null;
};

type Prep = {
  whatToExpect: string;
  estimatedMinutes: number;
  dataUse: string;
  expiresAt?: string | null;
};

export function PublicInterviewPage() {
  const { t } = useTranslation();
  const { token } = useParams({ from: "/interview/$token" });
  const [prep, setPrep] = useState<Prep | null>(null);
  const [session, setSession] = useState<Session | null>(null);
  const [consent, setConsent] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const answeredCount = useMemo(
    () => session?.turns.filter((turn) => turn.role === "candidate").length ?? 0,
    [session]
  );
  const totalQuestions = session?.questions.length ?? 0;
  const progress = totalQuestions === 0 ? 0 : Math.min(100, Math.round((answeredCount / totalQuestions) * 100));

  const currentPrompt = useMemo(() => {
    if (!session || session.status !== "in_progress") {
      return null;
    }
    const assistantTurns = session.turns.filter((turn) => turn.role === "assistant");
    const lastAssistant = assistantTurns[assistantTurns.length - 1];
    return lastAssistant?.text ?? session.questions[answeredCount]?.prompt ?? null;
  }, [answeredCount, session]);

  useEffect(() => {
    let cancelled = false;
    void (async () => {
      try {
        const value = await publicApi.getInterviewPrep(token);
        if (!cancelled) {
          setPrep(value);
        }
        const existing = await publicApi.getInterviewSession(token);
        if (!cancelled && existing.disclosureAccepted) {
          setSession(existing);
          setConsent(true);
        }
      } catch (err) {
        if (!cancelled) {
          setError(mapInterviewError(err, t));
        }
      }
    })();
    return () => {
      cancelled = true;
    };
  }, [t, token]);

  const discloseAndStart = async () => {
    setBusy(true);
    setError(null);
    try {
      await publicApi.discloseInterview(token);
      const started = await publicApi.startInterview(token);
      setSession(started);
    } catch (err) {
      setError(mapInterviewError(err, t));
    } finally {
      setBusy(false);
    }
  };

  const submitAnswer = async (transcript: string, framesBase64: string[]) => {
    const next = await publicApi.answerInterview(token, transcript, framesBase64);
    setSession(next);
  };

  const expiresAt = session?.expiresAt ?? prep?.expiresAt ?? null;
  const completed = session?.status === "completed";

  return (
    <div className="min-h-screen bg-[radial-gradient(circle_at_top,_#e8ebff_0%,_#f3f5fb_45%,_#f3f5fb_100%)] text-foreground">
      <header className="border-b border-border/70 bg-surface/80 backdrop-blur">
        <div className="mx-auto flex max-w-3xl items-center justify-between px-4 py-4 sm:px-6">
          <div>
            <p className="text-lg font-extrabold tracking-tight text-brand-6">HireLens</p>
            <p className="text-xs font-semibold uppercase tracking-[0.14em] text-muted">{t("interview.candidateBadge")}</p>
          </div>
          {expiresAt ? (
            <p className="text-xs text-muted">
              {t("interview.expires")}: {new Date(expiresAt).toLocaleString()}
            </p>
          ) : null}
        </div>
      </header>

      <main className="mx-auto flex max-w-3xl flex-col gap-5 px-4 py-8 sm:px-6">
        <div>
          <h1 className="text-2xl font-extrabold tracking-tight sm:text-3xl">{t("interview.title")}</h1>
          <p className="mt-2 max-w-2xl text-sm leading-relaxed text-muted">{t("interview.candidateIntro")}</p>
        </div>

        {!session || !session.disclosureAccepted ? (
          <section className="rounded-2xl border border-border bg-surface p-5 shadow-card sm:p-6">
            <h2 className="text-lg font-extrabold">{t("interview.prep")}</h2>
            <ul className="mt-4 space-y-3 text-sm leading-relaxed text-muted">
              <li>
                <span className="font-semibold text-foreground">{t("interview.expect")}: </span>
                {prep?.whatToExpect ?? t("interview.disclosure")}
              </li>
              <li>
                <span className="font-semibold text-foreground">{t("interview.duration")}: </span>
                {prep ? t("interview.minutes", { count: prep.estimatedMinutes }) : "—"}
              </li>
              <li>
                <span className="font-semibold text-foreground">{t("interview.data")}: </span>
                {prep?.dataUse}
              </li>
              <li>{t("interview.flowSteps")}</li>
            </ul>
            <label className="mt-5 flex items-start gap-3 rounded-xl border border-border bg-brand-0/50 px-4 py-3 text-sm">
              <input
                type="checkbox"
                className="mt-1"
                checked={consent}
                onChange={(event) => setConsent(event.target.checked)}
              />
              <span>{t("interview.consent")}</span>
            </label>
            <Button type="button" className="mt-4" disabled={!consent || busy} onClick={() => void discloseAndStart()}>
              {busy ? t("interview.starting") : t("interview.start")}
            </Button>
          </section>
        ) : null}

        {session?.disclosureAccepted ? (
          <section className="rounded-2xl border border-border bg-surface p-5 shadow-card sm:p-6">
            <div className="flex flex-wrap items-end justify-between gap-3">
              <div>
                <h2 className="text-lg font-extrabold">{t("interview.videoAnswerTitle")}</h2>
                <p className="mt-1 text-sm text-muted">
                  {completed
                    ? t("interview.completed")
                    : t("interview.progress", { answered: answeredCount, total: Math.max(totalQuestions, 1) })}
                </p>
              </div>
              <span className="rounded-full bg-brand-1 px-3 py-1 text-xs font-bold text-brand-7">
                {completed ? t("interview.statusCompleted") : t("interview.statusLive")}
              </span>
            </div>

            {!completed && totalQuestions > 0 ? (
              <div className="mt-4 h-2 overflow-hidden rounded-full bg-brand-1">
                <div className="h-full rounded-full bg-brand-6 transition-all" style={{ width: `${progress}%` }} />
              </div>
            ) : null}

            {session.status === "in_progress" && currentPrompt ? (
              <div className="mt-5">
                <VideoAnswerRecorder
                  question={currentPrompt}
                  questionIndex={Math.min(answeredCount + 1, Math.max(totalQuestions, 1))}
                  questionTotal={Math.max(totalQuestions, 1)}
                  disabled={busy}
                  onSubmit={submitAnswer}
                />
              </div>
            ) : null}

            {completed ? (
              <div className="mt-5 space-y-3">
                {session.turns
                  .filter((turn) => turn.role === "candidate")
                  .map((turn, index) => (
                    <div key={index} className="rounded-xl border border-border bg-brand-0/50 px-4 py-3 text-sm">
                      <p className="text-xs font-bold uppercase tracking-[0.08em] text-muted">
                        {t("interview.answerN", { n: index + 1 })}
                      </p>
                      <p className="mt-1 leading-relaxed">{turn.text}</p>
                    </div>
                  ))}
                {session.summary ? <p className="text-sm text-muted">{session.summary}</p> : null}
              </div>
            ) : null}

            {!currentPrompt && !completed && session.status !== "in_progress" ? (
              <p className="mt-4 text-sm text-muted">{t("interview.waiting")}</p>
            ) : null}
          </section>
        ) : null}

        {error ? (
          <p className="rounded-xl border border-danger/30 bg-danger-bg px-4 py-3 text-sm text-danger" role="alert">
            {error}
          </p>
        ) : null}

        <p className="text-center text-xs text-muted">
          <Link to="/login" className="font-semibold text-brand-6 hover:underline">
            {t("interview.recruiterEntry")}
          </Link>
        </p>
      </main>
    </div>
  );
}

function mapInterviewError(err: unknown, t: (key: string) => string): string {
  const message = err instanceof Error ? err.message : "";
  if (/expired/i.test(message)) {
    return t("interview.expired");
  }
  if (/not found|http_404/i.test(message)) {
    return t("interview.notFound");
  }
  return t("errors.generic");
}
