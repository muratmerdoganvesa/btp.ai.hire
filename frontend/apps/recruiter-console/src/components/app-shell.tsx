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
    <div className="flex min-h-screen text-foreground">
      <aside className="hl-fade sticky top-0 hidden h-screen w-[17.5rem] shrink-0 flex-col border-r border-border/70 bg-surface/80 px-5 py-8 backdrop-blur-xl lg:flex">
        <Link to="/" className="group px-3">
          <span className="font-display text-[1.35rem] font-semibold tracking-tight text-foreground transition-colors group-hover:text-brand">
            HireLens
          </span>
          <span className="mt-0.5 block text-[0.7rem] font-semibold uppercase tracking-[0.18em] text-muted">
            {t("nav.workspace")}
          </span>
        </Link>
        <nav className="mt-12 flex flex-col gap-1">
          {items.map((item) => {
            const active = item.exact ? pathname === item.to : pathname.startsWith(item.to);
            return (
              <Link
                key={item.to}
                to={item.to}
                className={cn(
                  "rounded-lg px-4 py-2.5 text-sm transition-all duration-200",
                  active
                    ? "hl-nav-active bg-brand-1 font-semibold text-brand-8"
                    : "text-muted hover:bg-brand-1/70 hover:text-foreground"
                )}
              >
                {item.label}
              </Link>
            );
          })}
        </nav>
        <div className="mt-auto border-t border-border/80 pt-5">
          <div className="flex items-center gap-3 px-1">
            <InitialsAvatar name={session?.subject ?? "HL"} className="size-9 rounded-lg" />
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
        <header className="sticky top-0 z-10 border-b border-border/50 bg-background/70 backdrop-blur-xl">
          <div className="flex items-center justify-between gap-4 px-4 py-3.5 sm:px-8">
            <nav className="flex items-center gap-2 text-sm lg:hidden">
              <Link to="/" className="font-display text-lg font-semibold tracking-tight">
                HireLens
              </Link>
              <Link
                to="/positions"
                className={cn(
                  "rounded-lg px-3 py-1.5",
                  pathname.startsWith("/positions") ? "bg-brand-1 font-medium text-brand-8" : "text-muted"
                )}
              >
                {t("nav.positions")}
              </Link>
            </nav>
            <p className="hidden max-w-xl text-sm text-muted lg:block">{t("dashboard.subtitle")}</p>
            <Button variant="ghost" size="sm" className="lg:hidden" onClick={logout}>
              {t("dashboard.logout")}
            </Button>
          </div>
        </header>
        <main className="hl-rise mx-auto flex w-full max-w-[90rem] flex-1 flex-col gap-8 p-4 sm:px-8 sm:py-8 lg:px-10">{children}</main>
      </div>
    </div>
  );
}
