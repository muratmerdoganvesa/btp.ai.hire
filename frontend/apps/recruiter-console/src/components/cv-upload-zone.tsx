import { Button, Card, CardContent, CardHeader, CardTitle } from "@hirelens/ui";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "../api";

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
  const [error, setError] = useState<string | null>(null);

  const upload = async () => {
    if (!file) {
      return;
    }

    setBusy(true);
    setError(null);
    try {
      const session = await api.startUpload(positionId, candidateId, file);
      await api.putObject(session.uploadUrl, file);
      await api.completeUpload(session.documentId);
      onCompleted();
    } catch {
      setError(t("errors.generic"));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("upload.title")}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <p className="text-sm text-muted">{t("upload.hint")}</p>
        <label className="flex cursor-pointer flex-col items-center justify-center rounded-2xl border border-dashed border-border bg-brand-1/40 px-4 py-8 text-center transition-colors hover:border-brand-4">
          <span className="text-sm font-medium">{file ? file.name : t("upload.title")}</span>
          <span className="mt-1 text-xs text-muted">{t("upload.hint")}</span>
          <input
            type="file"
            className="sr-only"
            accept=".pdf,.txt,application/pdf,text/plain"
            onChange={(event) => setFile(event.target.files?.[0] ?? null)}
          />
        </label>
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
