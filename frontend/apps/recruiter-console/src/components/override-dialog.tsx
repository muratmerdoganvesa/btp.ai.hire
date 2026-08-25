import { Button } from "@hirelens/ui";
import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";

export function OverrideDialog({
  open,
  onClose,
  onConfirm,
  busy = false,
  children
}: {
  open: boolean;
  onClose: () => void;
  onConfirm: () => void;
  busy?: boolean;
  children: ReactNode;
}) {
  const { t } = useTranslation();
  if (!open) {
    return null;
  }

  return (
    <div className="fixed inset-0 z-20 flex items-center justify-center bg-foreground/40 p-4">
      <div
        role="dialog"
        aria-modal="true"
        aria-labelledby="override-title"
        className="w-full max-w-lg rounded-2xl border border-border bg-surface p-6 shadow-card"
      >
        <h2 id="override-title" className="mb-1 text-lg font-extrabold tracking-tight">
          {t("decision.override")}
        </h2>
        <p className="mb-4 text-sm text-muted">{t("decision.overrideHint")}</p>
        {children}
        <div className="mt-6 flex flex-col-reverse gap-3 sm:flex-row sm:justify-end">
          <Button variant="outline" type="button" onClick={onClose} disabled={busy}>
            {t("decision.close")}
          </Button>
          <Button type="button" onClick={onConfirm} disabled={busy}>
            {t("decision.submit")}
          </Button>
        </div>
      </div>
    </div>
  );
}
