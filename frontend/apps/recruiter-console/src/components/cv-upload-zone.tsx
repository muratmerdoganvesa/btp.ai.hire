import { Button, Card, CardContent, CardHeader, CardTitle } from "@hirelens/ui";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "../api";

async function waitForJob(jobId: string, onTick: (status: string) => void): Promise<void> {
  const started = Date.now();
  while (Date.now() - started < 90_000) {
    const job = await api.getJob(jobId);
    onTick(job.status);
    const status = job.status.toLowerCase();
    if (status === "succeeded" || status === "completed" || status === "done") {
      return;
    }
    if (status === "failed" || status === "error") {
      throw new Error(job.error ?? "job_failed");
    }
    await new Promise((resolve) => setTimeout(resolve, 1500));
  }
  throw new Error("job_timeout");
}

export function CvUploadZone({
  positionId,
  candidateId,
  onCompleted
}: {
  positionId: string;
  candidateId: string;
  onCompleted: () => void;
}) {
  const { t } = useTranslation();
  const [file, setFile] = useState<File | null>(null);
  const [busy, setBusy] = useState(false);
  const [phase, setPhase] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const upload = async () => {
    if (!file) {
      return;
    }

    setBusy(true);
    setError(null);
    setPhase(t("upload.phaseUpload"));
    try {
      const session = await api.startUpload(positionId, candidateId, file);
      await api.putObject(session.uploadUrl, file);
      setPhase(t("upload.phaseParse"));
      const job = await api.completeUpload(session.documentId);
      setPhase(t("upload.phaseMatch"));
      await waitForJob(job.jobId, (status) => setPhase(`${t("upload.phaseMatch")} (${status})`));
      setPhase(null);
      setFile(null);
      onCompleted();
    } catch (err) {
      const message = err instanceof Error ? err.message : "";
      setError(
        /scanned|could not be extracted|text could not/i.test(message)
          ? t("upload.scanned")
          : t("errors.generic")
      );
      setPhase(null);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card className="border-border/80 bg-surface/95">
      <CardHeader>
        <CardTitle className="font-display text-xl">{t("upload.title")}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <p className="text-sm text-muted">{t("upload.hint")}</p>
        <label className="flex cursor-pointer flex-col items-center justify-center rounded-2xl border border-dashed border-border bg-brand-1/40 px-4 py-8 text-center transition-colors hover:border-brand-4">
          <span className="text-sm font-medium">{file ? file.name : t("upload.title")}</span>
          <span className="mt-1 text-xs text-muted">{t("upload.hint")}</span>
          <input
            type="file"
            className="sr-only"
            accept=".pdf,.docx,.txt,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document,text/plain"
            onChange={(event) => setFile(event.target.files?.[0] ?? null)}
          />
        </label>
        {phase ? (
          <p className="text-sm text-muted" role="status">
            {phase}
          </p>
        ) : null}
        {error ? (
          <p className="text-sm text-danger" role="alert">
            {error}
          </p>
        ) : null}
        <Button type="button" disabled={!file || busy} onClick={() => void upload()}>
          {busy ? t("upload.working") : t("upload.submit")}
        </Button>
      </CardContent>
    </Card>
  );
}
