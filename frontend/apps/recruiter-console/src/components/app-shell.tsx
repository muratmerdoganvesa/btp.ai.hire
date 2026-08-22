import { Button, InitialsAvatar, cn } from "@hirelens/ui";
import { Link, useNavigate, useRouterState } from "@tanstack/react-router";
import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { useAuthStore } from "../auth-store";

export function AppShell({ children }: { children: ReactNode }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const pathname = useRouterState({ select: (state) => state.location.pathname });
  const session = useAuthStore((s) => s.session);
  const clear = useAuthStore((s) => s.clear);

  const items = [
    { to: "/", label: t("nav.dashboard"), exact: true },
    { to: "/positions", label: t("nav.positions"), exact: false }
  ] as const;

  const logout = () => {
    clear();
    void navigate({ to: "/login" });
  };

  return (
    <div className="flex min-h-screen bg-background text-foreground">
      <aside className="sticky top-0 hidden h-screen w-64 shrink-0 flex-col border-r border-border bg-surface px-4 py-6 lg:flex">
        <Link to="/" className="px-2 text-sm font-semibold tracking-tight">
          {t("app.recruiter")}
        </Link>
        <p className="mt-1 px-2 text-xs text-muted">{t("nav.workspace")}</p>
        <nav className="mt-8 flex flex-col gap-1">
          {items.map((item) => {
            const active = item.exact ? pathname === item.to : pathname.startsWith(item.to);
            return (
              <Link
                key={item.to}
                to={item.to}
                className={cn(
                  "rounded-md px-3 py-2 text-sm transition-colors",
                  active ? "bg-brand-1 font-medium text-foreground" : "text-muted hover:bg-brand-1 hover:text-foreground"
                )}
              >
                {item.label}
              </Link>
            );
          })}
        </nav>
        <div className="mt-auto rounded-lg border border-border bg-background p-3">
          <div className="flex items-center gap-3">
            <InitialsAvatar name={session?.subject ?? "HL"} />
            <div className="min-w-0">
              <p className="truncate text-sm font-medium">{session?.subject}</p>
              <p className="truncate text-xs text-muted">{session?.roles[0]}</p>
            </div>
          </div>
          <Button variant="outline" size="sm" className="mt-3 w-full" onClick={logout}>
            {t("dashboard.logout")}
          </Button>
        </div>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="sticky top-0 z-10 border-b border-border bg-surface/90 backdrop-blur">
          <div className="flex items-center justify-between gap-4 px-4 py-3 sm:px-6">
            <nav className="flex items-center gap-3 text-sm lg:hidden">
              <Link to="/" className="font-semibold">
                {t("app.recruiter")}
              </Link>
              <Link to="/positions" className="text-muted">
                {t("nav.positions")}
              </Link>
            </nav>
            <p className="hidden text-sm text-muted lg:block">{t("dashboard.subtitle")}</p>
            <Button variant="ghost" size="sm" className="lg:hidden" onClick={logout}>
              {t("dashboard.logout")}
            </Button>
          </div>
        </header>
        <main className="mx-auto flex w-full max-w-6xl flex-1 flex-col gap-6 p-4 sm:p-6">{children}</main>
      </div>
    </div>
  );
}
