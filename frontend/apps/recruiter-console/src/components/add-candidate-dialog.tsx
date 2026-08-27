import { Button, cn } from "@hirelens/ui";
import { ApiError } from "@hirelens/api-client";
import { useMutation } from "@tanstack/react-query";
import { useEffect, useId, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "../api";
import { uploadCandidateCv } from "./cv-upload-zone";
import { Field, TextInput } from "./field";

export function AddCandidateDialog({
  open,
  positionId,
  onClose,
  onCreated
}: {
  open: boolean;
  positionId: string;
  onClose: () => void;
  onCreated: (candidateId: string) => void;
}) {
  const { t } = useTranslation();
  const titleId = useId();
  const descId = useId();
  const inputRef = useRef<HTMLInputElement>(null);
  const [displayName, setDisplayName] = useState("");
  const [cvFile, setCvFile] = useState<File | null>(null);
  const [phase, setPhase] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const create = useMutation({
    mutationFn: async () => {
      if (!cvFile) {
        throw new Error(t("candidates.cvRequired"));
      }
      setPhase(null);
      const candidate = await api.createCandidate(positionId, displayName.trim());
      try {
        await uploadCandidateCv(positionId, candidate.id, cvFile, (p) => {
          if (p === "upload") setPhase(t("upload.phaseUpload"));
          else if (p === "parse") setPhase(t("upload.phaseParse"));
          else if (p.startsWith("match")) setPhase(`${t("upload.phaseMatch")} (${p.split(":")[1] ?? ""})`);
          else setPhase(t("upload.phaseMatch"));
        });
      } catch (err) {
        const message = err instanceof Error ? err.message : "";
        if (/scanned|could not be extracted|text could not/i.test(message)) {
          throw new Error(t("upload.scanned"));
        }
        if (err instanceof ApiError && message) {
          throw new Error(`${t("errors.generic")} (${message.replace(/^http_\d+:/, "")})`);
        }
        throw err instanceof Error ? err : new Error(t("errors.generic"));
      }
      return candidate;
    },
    onSuccess: (candidate) => {
      setPhase(null);
      onCreated(candidate.id);
      onClose();
    },
    onError: (err) => {
      setPhase(null);
      if (err instanceof ApiError) {
        setError(err.message);
        return;
      }
      setError(err instanceof Error ? err.message : t("errors.generic"));
    }
  });

  useEffect(() => {
    if (!open) {
      return;
    }
    setDisplayName("");
    setCvFile(null);
    setPhase(null);
    setError(null);
    const timer = window.setTimeout(() => inputRef.current?.focus(), 40);
    return () => window.clearTimeout(timer);
  }, [open]);

  useEffect(() => {
    if (!open) {
      return;
    }
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape" && !create.isPending) {
        onClose();
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [open, create.isPending, onClose]);

  if (!open) {
    return null;
  }

  const canSubmit = displayName.trim().length > 0 && Boolean(cvFile) && !create.isPending;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4" role="presentation">
      <button
        type="button"
        className="absolute inset-0 bg-slate-900/45 backdrop-blur-[1px]"
        aria-label={t("candidates.dialogDismiss")}
        disabled={create.isPending}
        onClick={() => {
          if (!create.isPending) {
            onClose();
          }
        }}
      />
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={descId}
        className={cn(
          "relative z-10 flex w-full max-w-lg flex-col overflow-hidden rounded-xl border border-border bg-surface shadow-[0_24px_64px_-24px_rgba(15,23,42,0.45)]"
        )}
      >
        <header className="border-b border-border px-5 py-4">
          <p className="text-[0.7rem] font-bold uppercase tracking-[0.14em] text-muted">
            {t("candidates.dialogKicker")}
          </p>
          <h2 id={titleId} className="mt-1 text-lg font-extrabold tracking-tight text-foreground">
            {t("candidates.addManualTitle")}
          </h2>
          <p id={descId} className="mt-1 text-sm leading-relaxed text-muted">
            {t("candidates.addManualBody")}
          </p>
        </header>

        <div className="flex flex-col gap-4 px-5 py-5">
          <Field label={t("candidates.displayName")}>
            <TextInput
              ref={inputRef}
              value={displayName}
              placeholder={t("candidates.displayNamePlaceholder")}
              autoComplete="name"
              disabled={create.isPending}
              onChange={(event) => {
                setError(null);
                setDisplayName(event.target.value);
              }}
            />
          </Field>

          <div className="flex flex-col gap-2">
            <span className="text-sm font-semibold text-foreground">{t("upload.title")}</span>
            <label
              className={cn(
                "flex cursor-pointer flex-col items-center justify-center rounded-xl border border-dashed px-4 py-6 text-center transition-colors",
                create.isPending
                  ? "cursor-not-allowed border-border bg-muted/20 opacity-70"
                  : "border-border bg-brand-1/40 hover:border-brand-4"
              )}
            >
              <span className="text-sm font-medium">
                {cvFile ? cvFile.name : t("candidates.cvRequired")}
              </span>
              <span className="mt-1 text-xs text-muted">{t("upload.hint")}</span>
              <input
                type="file"
                className="sr-only"
                disabled={create.isPending}
                accept=".pdf,.docx,.txt,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document,text/plain"
                onChange={(event) => {
                  setError(null);
                  setCvFile(event.target.files?.[0] ?? null);
                }}
              />
            </label>
          </div>

          {phase ? (
            <p className="text-sm text-muted" role="status">
              {phase}
            </p>
          ) : null}
          {error ? (
            <p className="text-sm font-medium text-danger" role="alert">
              {error}
            </p>
          ) : null}
        </div>

        <footer className="flex flex-wrap justify-end gap-2 border-t border-border bg-brand-0/40 px-5 py-3.5">
          <Button type="button" variant="outline" size="sm" disabled={create.isPending} onClick={onClose}>
            {t("candidates.dialogCancel")}
          </Button>
          <Button type="button" size="sm" disabled={!canSubmit} onClick={() => create.mutate()}>
            {create.isPending ? t("candidates.creatingWithCv") : t("candidates.createWithCv")}
          </Button>
        </footer>
      </div>
    </div>
  );
}
