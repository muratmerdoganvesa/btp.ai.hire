import { Button } from "@hirelens/ui";
import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";

export function OverrideDialog({
  open,
  onClose,
  children
}: {
  open: boolean;
  onClose: () => void;
  children: ReactNode;
}) {
  const { t } = useTranslation();
  if (!open) {
    return null;
  }

  return (
    <div className="fixed inset-0 z-10 flex items-center justify-center bg-foreground/40 p-4">
      <div className="w-full max-w-lg rounded-lg border border-border bg-surface p-4">
        <h2 className="mb-2 text-base font-semibold">{t("decision.override")}</h2>
        <p className="mb-4 text-sm text-muted">{t("decision.overrideHint")}</p>
        {children}
        <div className="mt-4 flex justify-end">
          <Button variant="outline" type="button" onClick={onClose}>
            {t("decision.close")}
          </Button>
        </div>
      </div>
    </div>
  );
}
