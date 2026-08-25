import { useTranslation } from "react-i18next";

export function ApplyShell({ children }: { children: React.ReactNode }) {
  const { t } = useTranslation();
  return (
    <main className="mx-auto flex min-h-screen max-w-2xl flex-col gap-6 p-6">
      <header>
        <p className="text-xs font-bold uppercase tracking-[0.14em] text-brand">{t("apply.brand")}</p>
        <h1 className="text-lg font-semibold">{t("apply.title")}</h1>
      </header>
      {children}
    </main>
  );
}
