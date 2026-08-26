import { Button, Card, CardContent, CardHeader, CardTitle } from "@hirelens/ui";
import { ApiError } from "@hirelens/api-client";
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
      throw new ApiError(500, job.error ?? "job_failed");
    }
    await new Promise((resolve) => setTimeout(resolve, 1500));
  }
  throw new Error("job_timeout");
}

export async function uploadCandidateCv(
  positionId: string,
  candidateId: string,
  file: File,
  onPhase?: (phase: string) => void
): Promise<void> {
  onPhase?.("upload");
  const session = await api.startUpload(positionId, candidateId, file);
  await api.putObject(session.uploadUrl, file);
  onPhase?.("parse");
  const job = await api.completeUpload(session.documentId);
  onPhase?.("match");
  await waitForJob(job.jobId, (status) => onPhase?.(`match:${status}`));
}

export function CvUploadZone({
  positionId,
  candidateId,
  onCompleted,
  compact
}: {
  positionId: string;
  candidateId: string;
  onCompleted: () => void;
  compact?: boolean;
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
    try {
      await uploadCandidateCv(positionId, candidateId, file, (p) => {
        if (p === "upload") setPhase(t("upload.phaseUpload"));
        else if (p === "parse") setPhase(t("upload.phaseParse"));
        else if (p.startsWith("match")) setPhase(`${t("upload.phaseMatch")} (${p.split(":")[1] ?? ""})`);
        else setPhase(t("upload.phaseMatch"));
      });
      setPhase(null);
      setFile(null);
      onCompleted();
    } catch (err) {
      const message = err instanceof Error ? err.message : "";
      if (/scanned|could not be extracted|text could not/i.test(message)) {
        setError(t("upload.scanned"));
      } else if (err instanceof ApiError && message) {
        setError(`${t("errors.generic")} (${message.replace(/^http_\d+:/, "")})`);
      } else if (message) {
        setError(`${t("errors.generic")} (${message})`);
      } else {
        setError(t("errors.generic"));
      }
      setPhase(null);
    } finally {
      setBusy(false);
    }
  };

  const body = (
    <div className="flex flex-col gap-3">
      {!compact ? <p className="text-sm text-muted">{t("upload.hint")}</p> : null}
      <label className="flex cursor-pointer flex-col items-center justify-center rounded-xl border border-dashed border-border bg-brand-1/40 px-4 py-6 text-center transition-colors hover:border-brand-4">
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
      <Button type="button" size="sm" disabled={!file || busy} onClick={() => void upload()}>
        {busy ? t("upload.working") : t("upload.submit")}
      </Button>
    </div>
  );

  if (compact) {
    return body;
  }

  return (
    <Card className="border-border/80 bg-surface/95">
      <CardHeader>
        <CardTitle className="font-display text-xl">{t("upload.title")}</CardTitle>
      </CardHeader>
      <CardContent>{body}</CardContent>
    </Card>
  );
}
