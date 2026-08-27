import { Button, InitialsAvatar, cn } from "@hirelens/ui";
import { Link, useNavigate, useRouterState } from "@tanstack/react-router";
import { useState, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { isDevAuth, logout as endSession } from "../auth-mode";
import { useAuthStore } from "../auth-store";
import { ProductTour } from "../tour/product-tour";
import { brandSurfaceStyle, BRAND_BAR_CLASS } from "./page-hero";

const roleKeys: Record<string, string> = {
  Recruiter: "login.roles.Recruiter",
  HiringManager: "login.roles.HiringManager",
  TenantAdmin: "login.roles.TenantAdmin"
};

type NavItem = {
  id: string;
  to: string;
  labelKey: string;
  tour?: string;
  /** How to decide active state */
  match: "exact" | "positions" | "candidates" | "create" | "pipeline" | "interview";
  icon: NavIconName;
};

type NavIconName = "overview" | "jobs" | "add" | "candidates" | "pipeline" | "interview" | "reports";

const primaryNav: NavItem[] = [
  { id: "overview", to: "/", labelKey: "nav.dashboard", match: "exact", tour: "tour-nav-dashboard", icon: "overview" },
  { id: "jobs", to: "/positions", labelKey: "nav.jobs", match: "positions", tour: "tour-nav-positions", icon: "jobs" },
  { id: "create", to: "/positions/new", labelKey: "nav.newJob", match: "create", icon: "add" },
  { id: "candidates", to: "/candidates", labelKey: "nav.candidates", match: "candidates", icon: "candidates" }
];

const processNav: NavItem[] = [
  { id: "pipeline", to: "/pipeline", labelKey: "nav.pipeline", match: "pipeline", icon: "pipeline" },
  { id: "interview", to: "/interviews", labelKey: "nav.aiInterview", match: "interview", icon: "interview" },
  { id: "reports", to: "/", labelKey: "nav.reports", match: "exact", icon: "reports" }
];

export function AppShell({ children }: { children: ReactNode }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const pathname = useRouterState({ select: (state) => state.location.pathname });
  const session = useAuthStore((s) => s.session);
  const [menuOpen, setMenuOpen] = useState(false);

  const role = session?.roles[0];
  const roleLabel = role && roleKeys[role] ? t(roleKeys[role]) : t("nav.recruiterRole");
  const displayName = friendlySubject(session?.subject) ?? roleLabel;

  const logout = () => {
    endSession();
    if (isDevAuth) {
      void navigate({ to: "/login" });
    }
  };

  const activeId = resolveActiveId(pathname);

  const renderNav = (items: NavItem[], onNavigate?: () => void) =>
    items.map((item) => {
      const active = item.id === activeId;
      return (
        <Link
          key={item.id}
          to={item.to}
          data-tour={item.tour}
          onClick={onNavigate}
          className={cn(
            "group flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-semibold transition-colors",
            active
              ? "bg-brand-1 text-brand-7 shadow-sm"
              : "text-slate-600 hover:bg-slate-100 hover:text-slate-900"
          )}
        >
          <span
            className={cn(
              "inline-flex size-8 shrink-0 items-center justify-center rounded-lg",
              active ? "bg-white text-brand-6" : "bg-slate-100 text-slate-500 group-hover:bg-white"
            )}
          >
            <NavIcon name={item.icon} />
          </span>
          <span className="truncate">{t(item.labelKey)}</span>
        </Link>
      );
    });

  return (
    <div className="flex min-h-screen bg-background text-foreground">
      <ProductTour />
      <aside className="sticky top-0 hidden h-screen w-[16.5rem] shrink-0 flex-col bg-[#f8fafc] lg:flex">
        <BrandBlock />
        <div className="flex min-h-0 flex-1 flex-col gap-5 overflow-y-auto border-r border-slate-200 px-3 py-4">
          <div>
            <p className="mb-2 px-2 text-[0.65rem] font-bold uppercase tracking-[0.14em] text-slate-400">
              {t("nav.sectionRecruiting")}
            </p>
            <nav className="flex flex-col gap-1" aria-label={t("nav.main")}>
              {renderNav(primaryNav)}
            </nav>
          </div>

          <div>
            <p className="mb-2 px-2 text-[0.65rem] font-bold uppercase tracking-[0.14em] text-slate-400">
              {t("nav.sectionProcess")}
            </p>
            <nav className="flex flex-col gap-1" aria-label={t("nav.sectionProcess")}>
              {renderNav(processNav)}
            </nav>
          </div>
        </div>

        <div className="border-r border-t border-slate-200 p-3">
          <div className="rounded-2xl border border-slate-200 bg-white p-3 shadow-sm">
            <div className="flex items-center gap-2.5">
              <InitialsAvatar name={displayName} className="size-9 rounded-full" />
              <div className="min-w-0">
                <p className="truncate text-sm font-bold text-slate-800">{displayName}</p>
                <p className="truncate text-xs text-slate-500">{roleLabel}</p>
              </div>
            </div>
            <Button variant="outline" size="sm" className="mt-3 w-full" onClick={logout}>
              {t("dashboard.logout")}
            </Button>
          </div>
        </div>
      </aside>

      <div className="flex min-h-0 min-w-0 flex-1 flex-col">
        <header className="flex flex-col border-b border-border bg-white lg:hidden">
          <div className="flex items-center justify-between gap-2 px-3 py-2.5">
            <BrandBlock compact />
            <div className="flex items-center gap-1">
              <Button
                type="button"
                variant="ghost"
                size="sm"
                aria-expanded={menuOpen}
                aria-label={t("nav.menu")}
                onClick={() => setMenuOpen((open) => !open)}
              >
                {menuOpen ? t("nav.close") : t("nav.menu")}
              </Button>
              <Button variant="ghost" size="sm" onClick={logout}>
                {t("dashboard.logout")}
              </Button>
            </div>
          </div>
          {menuOpen ? (
            <div className="space-y-4 border-t border-border px-3 py-3">
              <nav className="flex flex-col gap-1">{renderNav(primaryNav, () => setMenuOpen(false))}</nav>
              <nav className="flex flex-col gap-1">{renderNav(processNav, () => setMenuOpen(false))}</nav>
            </div>
          ) : null}
        </header>
        <main className="hl-rise flex min-h-0 w-full min-w-0 flex-1 flex-col overflow-hidden bg-[linear-gradient(180deg,#eef1fb_0%,var(--hl-bg)_28%)]">
          {children}
        </main>
      </div>
    </div>
  );
}

function BrandBlock({ compact = false }: { compact?: boolean }) {
  const { t } = useTranslation();
  if (compact) {
    return (
      <Link
        to="/"
        style={brandSurfaceStyle}
        className="flex items-center gap-2 rounded-xl px-3 py-2"
      >
        <span className="flex size-8 items-center justify-center rounded-lg bg-white/15 text-sm font-extrabold text-white">
          HL
        </span>
        <span className="text-sm font-extrabold tracking-tight text-white">HireLens</span>
      </Link>
    );
  }

  return (
    <Link
      to="/"
      style={brandSurfaceStyle}
      className={cn("relative flex items-center overflow-hidden px-5", BRAND_BAR_CLASS)}
    >
      <div className="pointer-events-none absolute -right-6 -top-8 size-28 rounded-full bg-white/10" />
      <div className="pointer-events-none absolute -bottom-10 -left-4 size-24 rounded-full bg-[#3d52e0]/30" />
      <div className="relative flex items-center gap-3">
        <span className="flex size-11 items-center justify-center rounded-2xl bg-white/15 text-base font-extrabold tracking-tight text-white ring-1 ring-white/25">
          HL
        </span>
        <div>
          <p className="text-lg font-extrabold tracking-tight text-white">HireLens</p>
          <p className="text-[0.65rem] font-semibold uppercase tracking-[0.16em] text-white/70">
            {t("nav.workspace")}
          </p>
        </div>
      </div>
    </Link>
  );
}

function resolveActiveId(pathname: string): string | null {
  if (pathname === "/positions/new") {
    return "create";
  }
  if (pathname === "/pipeline") {
    return "pipeline";
  }
  if (pathname === "/interviews" || pathname.startsWith("/interviews/")) {
    return "interview";
  }
  if (pathname === "/candidates" || pathname.startsWith("/candidates/")) {
    return "candidates";
  }
  if (/^\/positions\/[^/]+\/edit$/.test(pathname)) {
    return "jobs";
  }
  if (/^\/positions\/[^/]+$/.test(pathname)) {
    return "jobs";
  }
  if (pathname === "/positions") {
    return "jobs";
  }
  if (pathname === "/") {
    return "overview";
  }
  return null;
}

function NavIcon({ name }: { name: NavIconName }) {
  const common = "size-4";
  switch (name) {
    case "overview":
      return (
        <svg className={common} viewBox="0 0 24 24" fill="none" aria-hidden>
          <path d="M4 14l4-4 3 3 6-7" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
          <path d="M16 6h4v4" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
        </svg>
      );
    case "jobs":
      return (
        <svg className={common} viewBox="0 0 24 24" fill="none" aria-hidden>
          <path
            d="M8 7V6a2 2 0 012-2h4a2 2 0 012 2v1m-9 0h10a2 2 0 012 2v9a2 2 0 01-2 2H7a2 2 0 01-2-2V9a2 2 0 012-2z"
            stroke="currentColor"
            strokeWidth="1.8"
            strokeLinejoin="round"
          />
        </svg>
      );
    case "add":
      return (
        <svg className={common} viewBox="0 0 24 24" fill="none" aria-hidden>
          <path d="M12 5v14M5 12h14" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
        </svg>
      );
    case "candidates":
      return (
        <svg className={common} viewBox="0 0 24 24" fill="none" aria-hidden>
          <circle cx="12" cy="8" r="3.2" stroke="currentColor" strokeWidth="1.8" />
          <path d="M5.5 19a6.5 6.5 0 0113 0" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
        </svg>
      );
    case "pipeline":
      return (
        <svg className={common} viewBox="0 0 24 24" fill="none" aria-hidden>
          <path d="M4 17h4V10H4v7zm6 0h4V7h-4v10zm6 0h4V4h-4v13z" stroke="currentColor" strokeWidth="1.8" strokeLinejoin="round" />
        </svg>
      );
    case "interview":
      return (
        <svg className={common} viewBox="0 0 24 24" fill="none" aria-hidden>
          <rect x="4" y="5" width="16" height="14" rx="2" stroke="currentColor" strokeWidth="1.8" />
          <path d="M8 3v4M16 3v4M4 10h16" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
        </svg>
      );
    case "reports":
      return (
        <svg className={common} viewBox="0 0 24 24" fill="none" aria-hidden>
          <path d="M5 19V9M10 19V5M15 19v-7M20 19V8" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" />
        </svg>
      );
    default:
      return null;
  }
}

function friendlySubject(value: string | undefined): string | null {
  if (!value) {
    return null;
  }
  if (/^[0-9a-f-]{36}$/i.test(value)) {
    return null;
  }
  if (value.includes("@")) {
    return value.split("@")[0] ?? value;
  }
  if (value.includes(".")) {
    const part = value.split(".")[0];
    return part ? part.charAt(0).toUpperCase() + part.slice(1) : value;
  }
  return value.length > 24 ? `${value.slice(0, 12)}…` : value;
}
