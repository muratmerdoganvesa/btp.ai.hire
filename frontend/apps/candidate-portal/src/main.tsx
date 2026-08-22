import { Button, Card, CardContent, CardHeader, CardTitle } from "@hirelens/ui";
import { createI18n } from "@hirelens/i18n";
import { StrictMode, useEffect, useState } from "react";
import { createRoot } from "react-dom/client";
import { useTranslation } from "react-i18next";
import { AiDisclosureBanner } from "./components/ai-disclosure-banner";
import { InterviewChat } from "./components/interview-chat";
import "./styles.css";

createI18n();

type Session = {
  status: string;
  disclosureAccepted: boolean;
  questions: { criterionId: string; prompt: string }[];
  turns: { role: string; text: string }[];
  summary: string | null;
};

type Prep = { whatToExpect: string; estimatedMinutes: number; dataUse: string };

function InterviewApp() {
  const { t } = useTranslation();
  const token = window.location.pathname.split("/").filter(Boolean).pop() ?? "";
  const [prep, setPrep] = useState<Prep | null>(null);
  const [session, setSession] = useState<Session | null>(null);
  const [consent, setConsent] = useState(false);
  const [answer, setAnswer] = useState("");
  const [error, setError] = useState<string | null>(null);

  const call = async (path: string, method = "GET", body?: unknown) => {
    const response = await fetch(`/api/interviews/public/${token}${path}`, {
      method,
      headers: body ? { "Content-Type": "application/json" } : undefined,
      body: body ? JSON.stringify(body) : undefined
    });
    if (!response.ok) {
      throw new Error(`http_${response.status}`);
    }
    return (await response.json()) as never;
  };

  useEffect(() => {
    void call("/prep")
      .then((value) => setPrep(value as Prep))
      .catch(() => setError(t("errors.generic")));
  }, [t, token]);

  const discloseAndStart = async () => {
    try {
      await call("/disclose", "POST");
      const started = (await call("/start", "POST")) as Session;
      setSession(started);
    } catch {
      setError(t("errors.generic"));
    }
  };

  const sendAnswer = async () => {
    try {
      const next = (await call("/answers", "POST", { text: answer })) as Session;
      setSession(next);
      setAnswer("");
    } catch {
      setError(t("errors.generic"));
    }
  };

  return (
    <main className="mx-auto flex min-h-screen max-w-2xl flex-col gap-4 p-6">
      <h1 className="text-lg font-semibold">{t("interview.title")}</h1>
      <Card>
        <CardHeader>
          <CardTitle>{t("interview.disclosure")}</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-3">
          <AiDisclosureBanner />
          <p className="text-sm text-muted" aria-live="polite">
            {prep?.whatToExpect}
          </p>
          <p className="text-sm text-muted">
            {t("interview.duration")}: {prep?.estimatedMinutes}
          </p>
          <p className="text-sm text-muted">{prep?.dataUse}</p>
          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={consent} onChange={(event) => setConsent(event.target.checked)} />
            {t("interview.consent")}
          </label>
          <Button type="button" disabled={!consent} onClick={() => void discloseAndStart()}>
            {t("interview.start")}
          </Button>
        </CardContent>
      </Card>
      {session ? (
        <Card>
          <CardHeader>
            <CardTitle>{t("interview.title")}</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            <InterviewChat
              turns={session.turns}
              answer={answer}
              onAnswerChange={setAnswer}
              onSend={() => void sendAnswer()}
              onPause={() => void call("/pause", "POST").then((value) => setSession(value as Session))}
              onResume={() => void call("/resume", "POST").then((value) => setSession(value as Session))}
              status={session.status}
            />
            {session.summary ? <p className="text-sm text-muted">{session.summary}</p> : null}
          </CardContent>
        </Card>
      ) : null}
      {error ? (
        <p className="text-sm text-danger" role="alert">
          {error}
        </p>
      ) : null}
    </main>
  );
}

const root = document.getElementById("root");
if (!root) {
  throw new Error("Root element is missing.");
}

createRoot(root).render(
  <StrictMode>
    <InterviewApp />
  </StrictMode>
);
