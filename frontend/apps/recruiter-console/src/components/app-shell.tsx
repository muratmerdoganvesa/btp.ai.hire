import { Button, InitialsAvatar, cn } from "@hirelens/ui";
import { Link, useNavigate, useRouterState } from "@tanstack/react-router";
import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { isDevAuth, logout as endSession } from "../auth-mode";
import { useAuthStore } from "../auth-store";

export function AppShell({ children }: { children: ReactNode }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const pathname = useRouterState({ select: (state) => state.location.pathname });
  const session = useAuthStore((s) => s.session);

  const items = [
    { to: "/", label: t("nav.dashboard"), exact: true },
    { to: "/positions", label: t("nav.positions"), exact: false }
  ] as const;

  const logout = () => {
    endSession();
    if (isDevAuth) {
      void navigate({ to: "/login" });
    }
  };

  return (
    <div className="flex min-h-screen bg-background text-foreground">
      <aside className="sticky top-0 hidden h-screen w-72 shrink-0 flex-col bg-surface px-5 py-8 shadow-card lg:flex">
        <Link to="/" className="px-3 text-base font-semibold tracking-tight">
          {t("app.recruiter")}
        </Link>
        <p className="mt-1 px-3 text-sm text-muted">{t("nav.workspace")}</p>
        <nav className="mt-10 flex flex-col gap-2">
          {items.map((item) => {
            const active = item.exact ? pathname === item.to : pathname.startsWith(item.to);
            return (
              <Link
                key={item.to}
                to={item.to}
                className={cn(
                  "rounded-pill px-4 py-2.5 text-sm transition-colors",
                  active ? "bg-brand font-medium text-brand-fg" : "text-muted hover:bg-brand-1 hover:text-foreground"
                )}
              >
                {item.label}
              </Link>
            );
          })}
        </nav>
        <div className="mt-auto rounded-2xl bg-brand-1 p-4">
          <div className="flex items-center gap-3">
            <InitialsAvatar name={session?.subject ?? "HL"} />
            <div className="min-w-0">
              <p className="truncate text-sm font-medium">{session?.subject}</p>
              <p className="truncate text-xs text-muted">{session?.roles[0]}</p>
            </div>
          </div>
          <Button variant="outline" size="sm" className="mt-4 w-full" onClick={logout}>
            {t("dashboard.logout")}
          </Button>
        </div>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-10 bg-background/80 backdrop-blur">
          <div className="flex items-center justify-between gap-4 px-4 py-4 sm:px-8">
            <nav className="flex items-center gap-2 text-sm lg:hidden">
              <Link to="/" className="rounded-pill bg-brand px-3 py-1.5 font-semibold text-brand-fg">
                {t("app.recruiter")}
              </Link>
              <Link to="/positions" className="rounded-pill px-3 py-1.5 text-muted">
                {t("nav.positions")}
              </Link>
            </nav>
            <p className="hidden text-sm text-muted lg:block">{t("dashboard.subtitle")}</p>
            <Button variant="ghost" size="sm" className="lg:hidden" onClick={logout}>
              {t("dashboard.logout")}
            </Button>
          </div>
        </header>
        <main className="mx-auto flex w-full max-w-6xl flex-1 flex-col gap-8 p-4 sm:p-8">{children}</main>
      </div>
    </div>
  );
}
