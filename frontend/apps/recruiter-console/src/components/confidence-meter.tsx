import { useTranslation } from "react-i18next";

export function ConfidenceMeter({ value }: { value: number }) {
  const { t } = useTranslation();
  const percent = Math.round(Math.min(1, Math.max(0, value)) * 100);
  return (
    <div className="flex flex-col gap-1" aria-label={t("evaluation.confidence")}>
      <span className="text-xs text-muted">
        {t("evaluation.confidence")} {percent}%
      </span>
      <div className="h-2 rounded-full bg-brand-2">
        <div className="h-2 rounded-full bg-brand" style={{ width: `${percent}%` }} />
      </div>
    </div>
  );
}
