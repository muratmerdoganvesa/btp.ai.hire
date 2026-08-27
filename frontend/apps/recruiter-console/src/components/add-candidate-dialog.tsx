import { Button, cn } from "@hirelens/ui";
import { ApiError } from "@hirelens/api-client";
import { useMutation } from "@tanstack/react-query";
import { useEffect, useId, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "../api";
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
  const [error, setError] = useState<string | null>(null);

  const create = useMutation({
    mutationFn: () => api.createCandidate(positionId, displayName.trim()),
    onSuccess: (candidate) => {
      onCreated(candidate.id);
      onClose();
    },
    onError: (err) => {
      if (err instanceof ApiError) {
        setError(err.message);
        return;
      }
      setError(t("errors.generic"));
    }
  });

  useEffect(() => {
    if (!open) {
      return;
    }
    setDisplayName("");
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

  const canSubmit = displayName.trim().length > 0 && !create.isPending;

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
          "relative z-10 flex w-full max-w-md flex-col overflow-hidden rounded-xl border border-border bg-surface shadow-[0_24px_64px_-24px_rgba(15,23,42,0.45)]"
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
              onKeyDown={(event) => {
                if (event.key === "Enter" && canSubmit) {
                  event.preventDefault();
                  create.mutate();
                }
              }}
            />
          </Field>
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
            {create.isPending ? t("candidates.adding") : t("candidates.addManualSubmit")}
          </Button>
        </footer>
      </div>
    </div>
  );
}
