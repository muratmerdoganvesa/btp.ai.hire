import type { Position } from "@hirelens/api-client";
import { Button, cn } from "@hirelens/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "../api";
import { isDevAuth } from "../auth-mode";
import { useAuthStore } from "../auth-store";
import { AppShell } from "../components/app-shell";
import { Pagination } from "../components/pagination";
import { useTourStore } from "../tour/tour-store";

const PAGE_SIZE = 8;
type SortKey = "review" | "candidates" | "newest" | "title";

export function DashboardPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const session = useAuthStore((s) => s.session);
  const startTour = useTourStore((s) => s.start);
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [query, setQuery] = useState("");
  const [sort, setSort] = useState<SortKey>("review");

  const seed = useMutation({
    mutationFn: () => api.seedDemo(),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["positions"] });
      await queryClient.invalidateQueries({ queryKey: ["positions", "stats"] });
    }
  });

  const tenant = useQuery({
    queryKey: ["tenant", session?.tenantId],
    queryFn: () => api.getCurrentTenant(),
    enabled: Boolean(session)
  });

  const positions = useQuery({
    queryKey: ["positions", "stats"],
    queryFn: () => api.listPositions(true),
    enabled: Boolean(session)
  });

  const list = positions.data ?? [];

  const needsReview = useMemo(
    () =>
      [...list]
        .filter((p) => (p.stats?.reviewPendingCount ?? 0) > 0)
        .sort((a, b) => (b.stats?.reviewPendingCount ?? 0) - (a.stats?.reviewPendingCount ?? 0))
        .slice(0, 4),
    [list]
  );

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    let rows = [...list];
    if (q) {
      rows = rows.filter((p) => p.title.toLowerCase().includes(q));
    }

    rows.sort((a, b) => {
      switch (sort) {
        case "title":
          return a.title.localeCompare(b.title, "tr");
        case "candidates":
          return (b.stats?.totalCandidates ?? 0) - (a.stats?.totalCandidates ?? 0);
        case "newest":
          return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
        default:
          return (b.stats?.reviewPendingCount ?? 0) - (a.stats?.reviewPendingCount ?? 0);
      }
    });

    return rows;
  }, [list, query, sort]);

  const pageCount = Math.max(1, Math.ceil(filtered.length / PAGE_SIZE));

  useEffect(() => {
    if (page > pageCount) {
      setPage(pageCount);
    }
  }, [page, pageCount]);

  const pageRows = useMemo(() => {
    const start = (page - 1) * PAGE_SIZE;
    return filtered.slice(start, start + PAGE_SIZE);
  }, [filtered, page]);

  if (!session) {
    if (isDevAuth) {
      void navigate({ to: "/login" });
    }
    return null;
  }

  const rawWorkspace = tenant.data?.name?.trim() || session.subject;
  const looksLikeId = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(rawWorkspace);
  const workspaceName = looksLikeId ? t("dashboard.workspaceFallback") : rawWorkspace;
  const totals = {
    candidates: list.reduce((sum, p) => sum + (p.stats?.totalCandidates ?? 0), 0),
    evaluated: list.reduce((sum, p) => sum + (p.stats?.evaluatedCount ?? 0), 0),
    review: list.reduce((sum, p) => sum + (p.stats?.reviewPendingCount ?? 0), 0)
  };

  return (
    <AppShell>
      <header className="flex shrink-0 flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div className="min-w-0">
          <p className="text-xs font-semibold uppercase tracking-[0.12em] text-muted">{workspaceName}</p>
          <h1 className="mt-1 text-2xl font-extrabold tracking-tight sm:text-3xl">{t("dashboard.title")}</h1>
          <p className="mt-1 max-w-xl text-sm text-muted">{t("dashboard.subtitle")}</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Button type="button" variant="outline" size="sm" onClick={() => startTour(true)}>
            {t("tour.start")}
          </Button>
          <Button asChild size="sm">
            <Link to="/positions/new">{t("dashboard.createPosition")}</Link>
          </Button>
        </div>
      </header>

      <section
        data-tour="tour-funnel"
        className="grid shrink-0 grid-cols-2 gap-3 sm:grid-cols-4"
      >
        <Kpi label={t("dashboard.openReqs")} value={list.length} />
        <Kpi label={t("dashboard.funnelCandidates")} value={totals.candidates} />
        <Kpi label={t("dashboard.funnelEvaluations")} value={totals.evaluated} />
        <Kpi label={t("dashboard.reviewPending")} value={totals.review} emphasize={totals.review > 0} />
      </section>

      {needsReview.length > 0 ? (
        <section className="shrink-0 rounded-2xl border border-amber-200/80 bg-amber-50/60 p-4 sm:p-5">
          <div className="mb-3 flex items-end justify-between gap-2">
            <div>
              <h2 className="text-base font-extrabold tracking-tight">{t("dashboard.attentionTitle")}</h2>
              <p className="text-sm text-muted">{t("dashboard.attentionHint")}</p>
            </div>
          </div>
          <ul className="grid gap-2 sm:grid-cols-2">
            {needsReview.map((position) => (
              <li key={position.id}>
                <Link
                  to="/positions/$positionId"
                  params={{ positionId: position.id }}
                  className="flex items-center justify-between gap-3 rounded-xl border border-border/80 bg-white px-4 py-3 transition-colors hover:border-brand-4"
                >
                  <span className="min-w-0 truncate font-semibold">{position.title}</span>
                  <span className="shrink-0 rounded-lg bg-amber-100 px-2 py-1 text-xs font-bold text-amber-800">
                    {t("dashboard.reviewBadge", { count: position.stats?.reviewPendingCount ?? 0 })}
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      <section data-tour="tour-recent" className="flex min-h-0 flex-1 flex-col gap-3">
        <div className="flex shrink-0 flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h2 className="text-base font-extrabold tracking-tight">{t("dashboard.jobList")}</h2>
            <p className="text-xs text-muted">{t("dashboard.jobListHint")}</p>
          </div>
          <div className="flex flex-wrap items-center gap-2">
            <input
              value={query}
              placeholder={t("dashboard.searchPlaceholder")}
              onChange={(event) => {
                setQuery(event.target.value);
                setPage(1);
              }}
              className="h-9 w-full min-w-[11rem] rounded-xl border border-border bg-surface px-3 text-sm outline-none placeholder:text-muted focus-visible:border-brand-5 focus-visible:ring-2 focus-visible:ring-brand-6/15 sm:w-52"
              aria-label={t("dashboard.search")}
            />
            <div className="flex rounded-xl border border-border bg-surface p-0.5" role="group" aria-label={t("dashboard.sort")}>
              {(
                [
                  ["review", t("dashboard.sortReview")],
                  ["candidates", t("dashboard.sortCandidates")],
                  ["newest", t("dashboard.sortNewest")]
                ] as const
              ).map(([id, label]) => (
                <button
                  key={id}
                  type="button"
                  onClick={() => {
                    setSort(id);
                    setPage(1);
                  }}
                  className={cn(
                    "h-8 rounded-lg px-2.5 text-xs font-bold transition-colors",
                    sort === id ? "bg-brand-6 text-white" : "text-muted hover:bg-brand-0 hover:text-foreground"
                  )}
                >
                  {label}
                </button>
              ))}
            </div>
          </div>
        </div>

        <div className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-2xl border border-border bg-surface">
          {positions.isLoading ? (
            <div className="space-y-0 divide-y divide-border">
              {Array.from({ length: 5 }).map((_, index) => (
                <div key={index} className="h-14 animate-pulse bg-brand-0/60" />
              ))}
            </div>
          ) : filtered.length === 0 ? (
            <div className="px-6 py-12 text-center">
              <p className="text-base font-bold text-foreground">
                {list.length === 0 ? t("dashboard.empty") : t("dashboard.noResults")}
              </p>
              {list.length === 0 ? (
                <div className="mt-4 flex flex-wrap items-center justify-center gap-3">
                  <Button asChild size="sm">
                    <Link to="/positions/new">{t("dashboard.createFirst")}</Link>
                  </Button>
                  <button
                    type="button"
                    className="text-sm font-semibold text-muted underline-offset-2 hover:text-foreground hover:underline disabled:opacity-50"
                    disabled={seed.isPending}
                    onClick={() => seed.mutate()}
                  >
                    {seed.isPending ? t("dashboard.seedDemoBusy") : t("dashboard.seedDemo")}
                  </button>
                </div>
              ) : null}
            </div>
          ) : (
            <>
              <ul className="min-h-0 flex-1 divide-y divide-border overflow-auto">
                {pageRows.map((position) => (
                  <JobRow key={position.id} position={position} />
                ))}
              </ul>
              <div className="shrink-0 border-t border-border px-3 py-3 sm:px-4">
                <Pagination
                  page={page}
                  pageCount={pageCount}
                  totalItems={filtered.length}
                  pageSize={PAGE_SIZE}
                  onPageChange={setPage}
                />
              </div>
            </>
          )}
        </div>
      </section>
    </AppShell>
  );
}

function JobRow({ position }: { position: Position }) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);
  const stats = position.stats;
  const review = stats?.reviewPendingCount ?? 0;
  const total = stats?.totalCandidates ?? 0;
  const evaluated = stats?.evaluatedCount ?? 0;

  const copyApplyLink = async () => {
    if (!position.slug) {
      return;
    }
    const url = `${window.location.origin}/apply/${position.slug}`;
    try {
      await navigator.clipboard.writeText(url);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1600);
    } catch {
      window.open(url, "_blank", "noopener,noreferrer");
    }
  };

  return (
    <li className="flex flex-col gap-3 px-4 py-4 transition-colors hover:bg-brand-0/35 sm:flex-row sm:items-center sm:justify-between sm:px-5">
      <div className="min-w-0 flex-1">
        <Link
          to="/positions/$positionId"
          params={{ positionId: position.id }}
          className="block truncate text-base font-bold tracking-tight text-foreground hover:text-brand-7"
        >
          {position.title}
        </Link>
        <div className="mt-1.5 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-muted">
          <span>
            <strong className="font-semibold text-foreground">{total}</strong> {t("dashboard.colCandidates").toLowerCase()}
          </span>
          <span>
            <strong className="font-semibold text-foreground">{evaluated}</strong>{" "}
            {t("dashboard.colEvaluated").toLowerCase()}
          </span>
          {review > 0 ? (
            <span className="font-semibold text-amber-800">
              {t("dashboard.reviewBadge", { count: review })}
            </span>
          ) : (
            <span>{t("dashboard.noReviewWaiting")}</span>
          )}
        </div>
      </div>
      <div className="flex shrink-0 flex-wrap items-center gap-2">
        {position.slug ? (
          <Button type="button" variant="ghost" size="sm" onClick={() => void copyApplyLink()}>
            {copied ? t("dashboard.linkCopied") : t("dashboard.copyLink")}
          </Button>
        ) : null}
        <Button asChild size="sm">
          <Link to="/positions/$positionId" params={{ positionId: position.id }}>
            {t("dashboard.openCandidates")}
          </Link>
        </Button>
      </div>
    </li>
  );
}

function Kpi({ label, value, emphasize = false }: { label: string; value: number; emphasize?: boolean }) {
  return (
    <div
      className={cn(
        "rounded-2xl border px-4 py-3",
        emphasize ? "border-amber-200 bg-amber-50/70" : "border-border bg-surface"
      )}
    >
      <p className="text-xs font-semibold text-muted">{label}</p>
      <p
        className={cn(
          "mt-1 text-2xl font-extrabold tabular-nums tracking-tight",
          emphasize ? "text-amber-800" : "text-foreground"
        )}
      >
        {value}
      </p>
    </div>
  );
}
