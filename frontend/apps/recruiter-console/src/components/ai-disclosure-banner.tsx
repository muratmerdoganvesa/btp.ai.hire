import { useTranslation } from "react-i18next";

export function AiDisclosureBanner() {
  const { t } = useTranslation();
  return (
    <p className="rounded-md border border-border bg-brand-1 px-3 py-2 text-sm" role="note">
      {t("interview.disclosure")}
    </p>
  );
}
