import { ApiError, PublicApi } from "@hirelens/api-client";
import { Button, Card, CardContent, CardHeader, CardTitle } from "@hirelens/ui";
import { Link, useNavigate, useParams } from "@tanstack/react-router";
import { useMutation, useQuery } from "@tanstack/react-query";
import { useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { ApplyShell } from "./apply-shell";

export const publicApi = new PublicApi("");
const CONSENT_VERSION = "2026-08-01";
const MAX_BYTES = 10 * 1024 * 1024;

export function ApplyFormPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { slug } = useParams({ from: "/apply/$slug/form" });
  const abortRef = useRef<AbortController | null>(null);
  const [displayName, setDisplayName] = useState("");
  const [email, setEmail] = useState("");
  const [phone, setPhone] = useState("");
  const [linkedIn, setLinkedIn] = useState("");
  const [coverLetter, setCoverLetter] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [progress, setProgress] = useState<number | null>(null);

  useQuery({
    queryKey: ["public-job", slug],
    queryFn: () => publicApi.getPublicJob(slug)
  });

  const submit = useMutation({
    mutationFn: async () => {
      if (!file) {
        throw new Error("file_required");
      }
      const ext = file.name.split(".").pop()?.toLowerCase() ?? "";
      if (!["pdf", "docx", "txt"].includes(ext)) {
        throw new Error("bad_format");
      }
      if (file.size > MAX_BYTES) {
        throw new Error("too_large");
      }

      const form = new FormData();
      form.append("slug", slug);
      form.append("displayName", displayName.trim());
      form.append("email", email.trim());
      form.append("phone", phone.trim());
      form.append("consentVersion", CONSENT_VERSION);
      form.append("consentAccepted", "true");
      form.append("cv", file);
      if (linkedIn.trim()) {
        form.append("linkedIn", linkedIn.trim());
      }
      if (coverLetter.trim()) {
        form.append("coverLetter", coverLetter.trim());
      }

      setProgress(10);
      abortRef.current = new AbortController();
      const result = await publicApi.submitPublicApplication(form);
      setProgress(100);
      return result;
    },
    onSuccess: async (result) => {
      await navigate({
        to: "/apply/$slug/done",
        params: { slug },
        search: { ref: result.referenceNumber }
      });
    },
    onError: (err) => {
      setProgress(null);
      if (err instanceof Error && err.message === "bad_format") {
        setError(t("apply.badFormat"));
        return;
      }
      if (err instanceof Error && err.message === "too_large") {
        setError(t("apply.tooLarge"));
        return;
      }
      if (err instanceof ApiError) {
        setError(t("apply.submitFailed"));
        return;
      }
      setError(t("errors.generic"));
    }
  });

  const cancelUpload = () => {
    abortRef.current?.abort();
    setProgress(null);
    submit.reset();
  };

  const ready = displayName.trim() && email.trim() && phone.trim() && file;

  return (
    <ApplyShell>
      <Card>
        <CardHeader>
          <CardTitle>{t("apply.formTitle")}</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <Field label={t("apply.name")}>
            <input
              className="w-full rounded-lg border border-border px-3 py-2 text-sm"
              value={displayName}
              onChange={(e) => setDisplayName(e.target.value)}
            />
          </Field>
          <Field label={t("apply.email")}>
            <input
              type="email"
              className="w-full rounded-lg border border-border px-3 py-2 text-sm"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
          </Field>
          <Field label={t("apply.phone")}>
            <input
              className="w-full rounded-lg border border-border px-3 py-2 text-sm"
              value={phone}
              onChange={(e) => setPhone(e.target.value)}
            />
          </Field>
          <Field label={t("apply.cv")}>
            <div className="rounded-xl border border-dashed border-border bg-brand-0/40 p-6 text-center">
              <input
                type="file"
                accept=".pdf,.docx,.txt,application/pdf,text/plain,application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                onChange={(e) => setFile(e.target.files?.[0] ?? null)}
              />
              <p className="mt-2 text-xs text-muted">{t("apply.cvHint")}</p>
              {file ? <p className="mt-1 text-sm font-medium">{file.name}</p> : null}
            </div>
          </Field>
          <Field label={t("apply.linkedIn")}>
            <input
              className="w-full rounded-lg border border-border px-3 py-2 text-sm"
              value={linkedIn}
              onChange={(e) => setLinkedIn(e.target.value)}
            />
          </Field>
          <Field label={t("apply.coverLetter")}>
            <textarea
              className="min-h-24 w-full rounded-lg border border-border px-3 py-2 text-sm"
              value={coverLetter}
              onChange={(e) => setCoverLetter(e.target.value)}
            />
          </Field>
          {progress !== null ? (
            <div className="flex flex-col gap-2">
              <div className="h-2 overflow-hidden rounded-full bg-border">
                <div className="h-full bg-brand transition-all" style={{ width: `${progress}%` }} />
              </div>
              <Button type="button" variant="outline" size="sm" onClick={cancelUpload}>
                {t("apply.cancelUpload")}
              </Button>
            </div>
          ) : null}
          {error ? (
            <p className="text-sm text-danger" role="alert">
              {error}
            </p>
          ) : null}
          <div className="flex flex-wrap gap-3">
            <Button type="button" disabled={!ready || submit.isPending} onClick={() => submit.mutate()}>
              {submit.isPending ? t("apply.submitting") : t("apply.submit")}
            </Button>
            <Button asChild variant="outline">
              <Link to="/apply/$slug/consent" params={{ slug }}>
                {t("apply.back")}
              </Link>
            </Button>
          </div>
        </CardContent>
      </Card>
    </ApplyShell>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <label className="flex flex-col gap-1.5 text-sm">
      <span className="font-semibold">{label}</span>
      {children}
    </label>
  );
}
