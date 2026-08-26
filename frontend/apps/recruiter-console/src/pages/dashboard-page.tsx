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

  const workspaceName = tenant.data?.name?.trim() || session.subject;
  const totals = {
    candidates: list.reduce((sum, p) => sum + (p.stats?.totalCandidates ?? 0), 0),
    evaluated: list.reduce((sum, p) => sum + (p.stats?.evaluatedCount ?? 0), 0),
    review: list.reduce((sum, p) => sum + (p.stats?.reviewPendingCount ?? 0), 0)
  };

  return (
    <AppShell>
      <header className="flex flex-col gap-5 border-b border-border pb-6 sm:flex-row sm:items-end sm:justify-between">
        <div className="min-w-0">
          <p className="text-sm text-muted">{workspaceName}</p>
          <h1 className="mt-1 text-[1.75rem] font-extrabold leading-tight tracking-tight text-foreground sm:text-3xl">
            {t("dashboard.title")}
          </h1>
          <p className="mt-2 max-w-lg text-sm leading-relaxed text-muted">{t("dashboard.subtitle")}</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <button
            type="button"
            className="rounded-full px-3 py-2 text-sm font-semibold text-muted transition-colors hover:bg-brand-0 hover:text-foreground"
            onClick={() => startTour(true)}
          >
            {t("tour.start")}
          </button>
          <Button asChild>
            <Link to="/positions/new">{t("dashboard.createPosition")}</Link>
          </Button>
        </div>
      </header>

      <section
        data-tour="tour-funnel"
        className="grid grid-cols-2 divide-border overflow-hidden rounded-2xl border border-border bg-surface sm:grid-cols-4 sm:divide-x"
      >
        <Kpi label={t("dashboard.openReqs")} value={list.length} />
        <Kpi label={t("dashboard.funnelCandidates")} value={totals.candidates} />
        <Kpi label={t("dashboard.funnelEvaluations")} value={totals.evaluated} />
        <Kpi label={t("dashboard.reviewPending")} value={totals.review} emphasize={totals.review > 0} />
      </section>

      <section data-tour="tour-recent" className="flex flex-col gap-4">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h2 className="text-lg font-extrabold tracking-tight">{t("dashboard.jobList")}</h2>
            <p className="mt-0.5 text-sm text-muted">{t("dashboard.jobListHint")}</p>
          </div>
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
              className="h-10 w-full min-w-[14rem] rounded-full border border-border bg-surface px-4 text-sm outline-none transition-shadow placeholder:text-muted focus-visible:border-brand-5 focus-visible:ring-4 focus-visible:ring-brand-6/15 sm:w-64"
            />
            <label className="sr-only" htmlFor="dashboard-sort">
              {t("dashboard.sort")}
            </label>
            <select
              id="dashboard-sort"
              className="h-10 rounded-full border border-border bg-surface px-3 text-sm outline-none focus-visible:border-brand-5 focus-visible:ring-4 focus-visible:ring-brand-6/15"
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

        <div className="overflow-hidden rounded-2xl border border-border bg-surface">
          {positions.isLoading ? (
            <div className="space-y-0 divide-y divide-border">
              {Array.from({ length: 5 }).map((_, index) => (
                <div key={index} className="h-14 animate-pulse bg-brand-0/60" />
              ))}
            </div>
          ) : filtered.length === 0 ? (
            <div className="px-6 py-14 text-center">
              <p className="text-base font-bold text-foreground">
                {list.length === 0 ? t("dashboard.empty") : t("dashboard.noResults")}
              </p>
              {list.length === 0 ? (
                <div className="mt-5 flex flex-wrap items-center justify-center gap-3">
                  <Button asChild>
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
              <div className="overflow-x-auto">
                <table className="w-full min-w-[44rem] text-left text-sm">
                  <thead>
                    <tr className="border-b border-border text-[0.7rem] font-bold uppercase tracking-[0.08em] text-muted">
                      <th className="px-4 py-3 font-bold sm:px-5">{t("dashboard.colTitle")}</th>
                      <th className="px-3 py-3 text-right font-bold tabular-nums">{t("dashboard.colCandidates")}</th>
                      <th className="hidden px-3 py-3 text-right font-bold tabular-nums md:table-cell">
                        {t("dashboard.colEvaluated")}
                      </th>
                      <th className="hidden px-3 py-3 text-right font-bold tabular-nums md:table-cell">
                        {t("dashboard.colPending")}
                      </th>
                      <th className="px-3 py-3 text-right font-bold tabular-nums">{t("dashboard.colReview")}</th>
                      <th className="px-4 py-3 text-right font-bold sm:px-5">
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
              <div className="px-4 pb-4 sm:px-5">
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

        {list.length > 0 ? (
          <p className="text-xs text-muted">
            <button
              type="button"
              className="font-semibold underline-offset-2 hover:text-foreground hover:underline disabled:opacity-50"
              disabled={seed.isPending}
              onClick={() => seed.mutate()}
            >
              {seed.isPending ? t("dashboard.seedDemoBusy") : t("dashboard.seedDemo")}
            </button>
            {seed.isSuccess ? (
              <span className="ml-2">
                {seed.data.skipped ? t("dashboard.seedDemoSkip") : t("dashboard.seedDemoDone")}
              </span>
            ) : null}
          </p>
        ) : null}
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
      <td className="px-4 py-3.5 sm:px-5">
        <Link
          to="/positions/$positionId"
          params={{ positionId: position.id }}
          className="block min-w-0 font-semibold tracking-tight text-foreground hover:text-brand-7"
        >
          <span className="line-clamp-1">{position.title}</span>
        </Link>
      </td>
      <td className="px-3 py-3.5 text-right tabular-nums text-foreground">{stats?.totalCandidates ?? 0}</td>
      <td className="hidden px-3 py-3.5 text-right tabular-nums text-muted md:table-cell">
        {stats?.evaluatedCount ?? 0}
      </td>
      <td className="hidden px-3 py-3.5 text-right tabular-nums text-muted md:table-cell">
        {stats?.pendingCount ?? 0}
      </td>
      <td className="px-3 py-3.5 text-right">
        <span
          className={cn(
            "inline-flex min-w-[1.75rem] justify-end tabular-nums font-semibold",
            review > 0 ? "text-amber-700" : "text-muted"
          )}
        >
          {review}
        </span>
      </td>
      <td className="px-4 py-3.5 sm:px-5">
        <div className="flex items-center justify-end gap-1.5">
          {position.slug ? (
            <button
              type="button"
              onClick={() => void copyApplyLink()}
              className="rounded-full px-3 py-1.5 text-xs font-semibold text-muted transition-colors hover:bg-brand-1 hover:text-brand-7"
            >
              {copied ? t("dashboard.linkCopied") : t("dashboard.copyLink")}
            </button>
          ) : null}
          <Button asChild size="sm" className="!h-9 !min-h-9 !px-4 !text-xs">
            <Link to="/positions/$positionId" params={{ positionId: position.id }}>
              {t("positions.open")}
            </Link>
          </Button>
        </div>
      </td>
    </tr>
  );
}

function Kpi({ label, value, emphasize = false }: { label: string; value: number; emphasize?: boolean }) {
  return (
    <div className="px-4 py-4 sm:px-5 sm:py-5">
      <p className="text-xs font-semibold text-muted">{label}</p>
      <p
        className={cn(
          "mt-1.5 text-2xl font-extrabold tabular-nums tracking-tight sm:text-3xl",
          emphasize ? "text-amber-700" : "text-foreground"
        )}
      >
        {value}
      </p>
    </div>
  );
}
