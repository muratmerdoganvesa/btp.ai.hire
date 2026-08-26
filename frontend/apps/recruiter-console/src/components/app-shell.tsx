import { Button, InitialsAvatar, cn } from "@hirelens/ui";
import { Link, useNavigate, useRouterState } from "@tanstack/react-router";
import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { isDevAuth, logout as endSession } from "../auth-mode";
import { useAuthStore } from "../auth-store";
import { ProductTour } from "../tour/product-tour";

function shortLabel(value: string | undefined): string {
  if (!value) {
    return "—";
  }
  if (value.length <= 18) {
    return value;
  }
  return `${value.slice(0, 8)}…${value.slice(-4)}`;
}

export function AppShell({ children }: { children: ReactNode }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const pathname = useRouterState({ select: (state) => state.location.pathname });
  const session = useAuthStore((s) => s.session);

  const items = [
    { to: "/", label: t("nav.dashboard"), exact: true, tour: "tour-nav-dashboard" },
    { to: "/positions", label: t("nav.positions"), exact: false, tour: "tour-nav-positions" }
  ] as const;

  const logout = () => {
    endSession();
    if (isDevAuth) {
      void navigate({ to: "/login" });
    }
  };

  return (
    <div className="flex min-h-screen bg-background text-foreground">
      <ProductTour />
      <aside className="sticky top-0 hidden h-screen w-60 shrink-0 flex-col border-r border-border bg-surface px-4 py-7 lg:flex">
        <Link to="/" className="px-2">
          <span className="text-xl font-extrabold tracking-tight text-brand-6">HireLens</span>
          <span className="mt-1 block text-[0.65rem] font-semibold uppercase tracking-[0.16em] text-muted">
            {t("nav.workspace")}
          </span>
        </Link>
        <nav className="mt-10 flex flex-col gap-1">
          {items.map((item) => {
            const active = item.exact ? pathname === item.to : pathname.startsWith(item.to);
            return (
              <Link
                key={item.to}
                to={item.to}
                data-tour={item.tour}
                className={cn(
                  "rounded-xl px-3 py-2.5 text-sm font-semibold transition-colors",
                  active ? "hl-nav-active" : "text-muted hover:bg-brand-0 hover:text-foreground"
                )}
              >
                {item.label}
              </Link>
            );
          })}
        </nav>
        <div className="mt-auto border-t border-border px-1 pt-5">
          <div className="flex items-center gap-2.5">
            <InitialsAvatar name={session?.roles[0] ?? "HL"} className="size-8 rounded-full" />
            <div className="min-w-0">
              <p className="truncate text-sm font-bold">{session?.roles[0] ?? "Recruiter"}</p>
              <p className="truncate text-xs text-muted" title={session?.subject}>
                {shortLabel(session?.subject)}
              </p>
            </div>
          </div>
          <Button variant="outline" size="sm" className="mt-3 w-full" onClick={logout}>
            {t("dashboard.logout")}
          </Button>
        </div>
      </aside>

      <div className="flex min-h-0 min-w-0 flex-1 flex-col">
        <header className="flex items-center justify-between border-b border-border bg-surface px-4 py-3 lg:hidden">
          <Link to="/" className="text-lg font-extrabold text-brand-6">
            HireLens
          </Link>
          <Button variant="ghost" size="sm" onClick={logout}>
            {t("dashboard.logout")}
          </Button>
        </header>
        <main className="hl-rise flex min-h-0 w-full min-w-0 flex-1 flex-col gap-3 overflow-hidden px-4 py-3 sm:px-5 sm:py-4 lg:px-6">
          {children}
        </main>
      </div>
    </div>
  );
}
