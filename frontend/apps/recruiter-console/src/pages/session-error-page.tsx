import { Button } from "@hirelens/ui";
import { useTranslation } from "react-i18next";

export function SessionErrorPage() {
  const { t } = useTranslation();
  const detail =
    typeof sessionStorage === "undefined" ? "" : (sessionStorage.getItem("hirelens.apiError") ?? "");

  return (
    <main className="flex min-h-screen items-center justify-center bg-background px-6">
      <div className="max-w-md rounded-2xl border border-border bg-surface p-8 text-center shadow-sm">
        <p className="text-sm font-bold tracking-wide text-brand-6">HireLens</p>
        <h1 className="mt-3 text-2xl font-extrabold tracking-tight">{t("sessionError.title")}</h1>
        <p className="mt-3 text-sm leading-6 text-muted">{t("sessionError.body")}</p>
        {detail ? (
          <p className="mt-3 rounded-lg bg-brand-0 px-3 py-2 text-left text-xs text-muted">{detail}</p>
        ) : null}
        <Button asChild className="mt-8 w-full">
          <a href="/logout">{t("sessionError.retry")}</a>
        </Button>
      </div>
    </main>
  );
}
