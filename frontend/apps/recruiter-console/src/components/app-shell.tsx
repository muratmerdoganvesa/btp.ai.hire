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
    <div className="flex min-h-screen text-foreground">
      <ProductTour />
      <aside className="hl-fade sticky top-0 m-4 hidden h-[calc(100vh-2rem)] w-60 shrink-0 flex-col rounded-2xl border border-white/50 bg-surface px-4 py-6 shadow-card lg:flex">
        <Link to="/" className="group px-2">
          <span className="text-[1.45rem] font-extrabold tracking-tight text-brand-7 transition-colors group-hover:text-brand-6">
            HireLens
          </span>
          <span className="mt-1 block text-[0.65rem] font-semibold uppercase tracking-[0.18em] text-muted">
            {t("nav.workspace")}
          </span>
        </Link>
        <nav className="mt-8 flex flex-col gap-1">
          {items.map((item) => {
            const active = item.exact ? pathname === item.to : pathname.startsWith(item.to);
            return (
              <Link
                key={item.to}
                to={item.to}
                data-tour={item.tour}
                className={cn(
                  "rounded-full px-4 py-2.5 text-sm font-semibold transition-colors",
                  active
                    ? "hl-nav-active bg-brand-1 text-brand-7"
                    : "text-muted hover:bg-brand-1/70 hover:text-foreground"
                )}
              >
                {item.label}
              </Link>
            );
          })}
        </nav>
        <div className="mt-auto border-t border-border pt-4">
          <div className="flex items-center gap-3 px-1">
            <InitialsAvatar name={session?.roles[0] ?? "HL"} className="size-9 rounded-full" />
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

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="px-4 pt-4 sm:px-8">
          <div className="flex items-center justify-between gap-4 rounded-2xl border border-white/10 bg-white/5 px-4 py-3 backdrop-blur-md">
            <nav className="flex items-center gap-3 text-sm lg:hidden">
              <Link to="/" className="text-lg font-extrabold tracking-tight text-white">
                HireLens
              </Link>
              <Link
                to="/positions"
                data-tour="tour-nav-positions"
                className={cn(
                  "rounded-full px-3 py-1.5 font-semibold",
                  pathname.startsWith("/positions") ? "bg-brand-6 text-white" : "text-white/70"
                )}
              >
                {t("nav.positions")}
              </Link>
            </nav>
            <p className="hidden max-w-xl text-sm text-white/65 lg:block">{t("dashboard.subtitle")}</p>
            <Button variant="outline" size="sm" className="border-white/20 bg-white/90 lg:hidden" onClick={logout}>
              {t("dashboard.logout")}
            </Button>
          </div>
        </header>
        <main className="hl-rise mx-auto flex w-full max-w-[90rem] flex-1 flex-col gap-7 p-4 sm:px-8 sm:py-6 lg:px-10">
          {children}
        </main>
      </div>
    </div>
  );
}
