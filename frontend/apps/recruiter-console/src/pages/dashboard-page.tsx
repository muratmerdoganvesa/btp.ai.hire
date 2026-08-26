import type { Position } from "@hirelens/api-client";
import { Button, cn } from "@hirelens/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "../api";
import { AppShell } from "../components/app-shell";
import { Pagination } from "../components/pagination";
import { isDevAuth } from "../auth-mode";
import { useAuthStore } from "../auth-store";
import { useTourStore } from "../tour/tour-store";

const PAGE_SIZE = 10;
type SortKey = "newest" | "title" | "candidates" | "pending" | "review";

export function DashboardPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const session = useAuthStore((s) => s.session);
  const startTour = useTourStore((s) => s.start);
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [query, setQuery] = useState("");
  const [sort, setSort] = useState<SortKey>("newest");

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

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    let rows = [...list];
    if (q) {
      rows = rows.filter((p) => p.title.toLowerCase().includes(q) || p.jobDescription.toLowerCase().includes(q));
    }

    rows.sort((a, b) => {
      switch (sort) {
        case "title":
          return a.title.localeCompare(b.title, "tr");
        case "candidates":
          return (b.stats?.totalCandidates ?? 0) - (a.stats?.totalCandidates ?? 0);
        case "pending":
          return (b.stats?.pendingCount ?? 0) - (a.stats?.pendingCount ?? 0);
        case "review":
          return (b.stats?.reviewPendingCount ?? 0) - (a.stats?.reviewPendingCount ?? 0);
        default:
          return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
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
      <header className="flex shrink-0 flex-col gap-3 border-b border-border pb-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="min-w-0">
          <p className="text-xs font-medium uppercase tracking-wide text-muted">{workspaceName}</p>
          <h1 className="text-xl font-extrabold leading-tight tracking-tight text-foreground sm:text-2xl">
            {t("dashboard.title")}
          </h1>
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
        className="grid shrink-0 grid-cols-2 divide-border overflow-hidden rounded-xl border border-border bg-surface sm:grid-cols-4 sm:divide-x"
      >
        <Kpi label={t("dashboard.openReqs")} value={list.length} />
        <Kpi label={t("dashboard.funnelCandidates")} value={totals.candidates} />
        <Kpi label={t("dashboard.funnelEvaluations")} value={totals.evaluated} />
        <Kpi label={t("dashboard.reviewPending")} value={totals.review} emphasize={totals.review > 0} />
      </section>

      <section data-tour="tour-recent" className="flex min-h-0 flex-1 flex-col gap-2">
        <div className="flex shrink-0 flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
          <h2 className="text-base font-extrabold tracking-tight">{t("dashboard.jobList")}</h2>
          <div className="flex flex-wrap items-center gap-2">
            <label className="sr-only" htmlFor="dashboard-search">
              {t("dashboard.search")}
            </label>
            <input
              id="dashboard-search"
              value={query}
              placeholder={t("dashboard.searchPlaceholder")}
              onChange={(event) => {
                setQuery(event.target.value);
                setPage(1);
              }}
              className="h-9 w-full min-w-[12rem] rounded-lg border border-border bg-surface px-3 text-sm outline-none placeholder:text-muted focus-visible:border-brand-5 focus-visible:ring-2 focus-visible:ring-brand-6/15 sm:w-56"
            />
            <label className="sr-only" htmlFor="dashboard-sort">
              {t("dashboard.sort")}
            </label>
            <select
              id="dashboard-sort"
              className="h-9 rounded-lg border border-border bg-surface px-3 text-sm outline-none focus-visible:border-brand-5 focus-visible:ring-2 focus-visible:ring-brand-6/15"
              value={sort}
              onChange={(event) => {
                setSort(event.target.value as SortKey);
                setPage(1);
              }}
            >
              <option value="newest">{t("dashboard.sortNewest")}</option>
              <option value="title">{t("dashboard.sortTitle")}</option>
              <option value="candidates">{t("dashboard.sortCandidates")}</option>
              <option value="pending">{t("dashboard.sortPending")}</option>
              <option value="review">{t("dashboard.sortReview")}</option>
            </select>
          </div>
        </div>

        <div className="flex min-h-0 flex-1 flex-col overflow-hidden rounded-xl border border-border bg-surface">
          {positions.isLoading ? (
            <div className="space-y-0 divide-y divide-border">
              {Array.from({ length: 5 }).map((_, index) => (
                <div key={index} className="h-11 animate-pulse bg-brand-0/60" />
              ))}
            </div>
          ) : filtered.length === 0 ? (
            <div className="px-6 py-10 text-center">
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
              <div className="min-h-0 flex-1 overflow-auto">
                <table className="w-full min-w-[44rem] text-left text-sm">
                  <thead className="sticky top-0 z-10 bg-surface">
                    <tr className="border-b border-border text-[0.7rem] font-bold uppercase tracking-[0.08em] text-muted">
                      <th className="px-3 py-2.5 font-bold sm:px-4">{t("dashboard.colTitle")}</th>
                      <th className="px-3 py-2.5 text-right font-bold tabular-nums">{t("dashboard.colCandidates")}</th>
                      <th className="hidden px-3 py-2.5 text-right font-bold tabular-nums md:table-cell">
                        {t("dashboard.colEvaluated")}
                      </th>
                      <th className="hidden px-3 py-2.5 text-right font-bold tabular-nums md:table-cell">
                        {t("dashboard.colPending")}
                      </th>
                      <th className="px-3 py-2.5 text-right font-bold tabular-nums">{t("dashboard.colReview")}</th>
                      <th className="px-3 py-2.5 text-right font-bold sm:px-4">
                        <span className="sr-only">{t("dashboard.colActions")}</span>
                      </th>
                    </tr>
                  </thead>
                  <tbody>
                    {pageRows.map((position) => (
                      <JobRow key={position.id} position={position} />
                    ))}
                  </tbody>
                </table>
              </div>
              <div className="shrink-0 px-3 pb-3 sm:px-4">
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
    <tr className="border-b border-border last:border-0 transition-colors hover:bg-brand-0/40">
      <td className="px-3 py-2 sm:px-4">
        <Link
          to="/positions/$positionId"
          params={{ positionId: position.id }}
          className="block min-w-0 font-semibold tracking-tight text-foreground hover:text-brand-7"
        >
          <span className="line-clamp-1">{position.title}</span>
        </Link>
      </td>
      <td className="px-3 py-2 text-right tabular-nums text-foreground">{stats?.totalCandidates ?? 0}</td>
      <td className="hidden px-3 py-2 text-right tabular-nums text-muted md:table-cell">
        {stats?.evaluatedCount ?? 0}
      </td>
      <td className="hidden px-3 py-2 text-right tabular-nums text-muted md:table-cell">
        {stats?.pendingCount ?? 0}
      </td>
      <td className="px-3 py-2 text-right">
        <span
          className={cn(
            "inline-flex min-w-[1.75rem] justify-end tabular-nums font-semibold",
            review > 0 ? "text-amber-700" : "text-muted"
          )}
        >
          {review}
        </span>
      </td>
      <td className="px-3 py-2 sm:px-4">
        <div className="flex items-center justify-end gap-1.5">
          {position.slug ? (
            <button
              type="button"
              onClick={() => void copyApplyLink()}
              className="rounded-lg px-2.5 py-1 text-xs font-semibold text-muted transition-colors hover:bg-brand-1 hover:text-brand-7"
            >
              {copied ? t("dashboard.linkCopied") : t("dashboard.copyLink")}
            </button>
          ) : null}
          <Link
            to="/positions/$positionId"
            params={{ positionId: position.id }}
            className="inline-flex h-8 shrink-0 items-center justify-center whitespace-nowrap rounded-lg bg-brand-6 px-3 text-xs font-semibold text-white transition-colors hover:bg-brand-7"
          >
            {t("positions.open")}
          </Link>
        </div>
      </td>
    </tr>
  );
}

function Kpi({ label, value, emphasize = false }: { label: string; value: number; emphasize?: boolean }) {
  return (
    <div className="px-3 py-2.5 sm:px-4 sm:py-3">
      <p className="text-[0.7rem] font-semibold text-muted">{label}</p>
      <p
        className={cn(
          "mt-0.5 text-xl font-extrabold tabular-nums tracking-tight sm:text-2xl",
          emphasize ? "text-amber-700" : "text-foreground"
        )}
      >
        {value}
      </p>
    </div>
  );
}
