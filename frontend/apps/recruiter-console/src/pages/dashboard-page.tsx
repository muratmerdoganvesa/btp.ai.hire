import type { Position } from "@hirelens/api-client";
import { Badge, Button, Card, CardContent, CardHeader, CardTitle, InitialsAvatar } from "@hirelens/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "../api";
import { AppShell } from "../components/app-shell";
import { Field, TextInput } from "../components/field";
import { Pagination } from "../components/pagination";
import { isDevAuth } from "../auth-mode";
import { useAuthStore } from "../auth-store";
import { useTourStore } from "../tour/tour-store";

const PAGE_SIZE = 8;
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

  const displayName = tenant.data?.name?.trim() || session.subject;
  const totals = {
    candidates: list.reduce((sum, p) => sum + (p.stats?.totalCandidates ?? 0), 0),
    evaluated: list.reduce((sum, p) => sum + (p.stats?.evaluatedCount ?? 0), 0),
    review: list.reduce((sum, p) => sum + (p.stats?.reviewPendingCount ?? 0), 0),
    failed: list.reduce((sum, p) => sum + (p.stats?.failedCount ?? 0), 0)
  };

  return (
    <AppShell>
      <header className="flex flex-col gap-6 lg:flex-row lg:items-end lg:justify-between">
        <div className="flex min-w-0 items-start gap-4">
          <InitialsAvatar name={displayName} className="size-12 shrink-0 rounded-full text-sm" />
          <div className="min-w-0">
            <p className="text-xs font-bold uppercase tracking-[0.14em] text-brand-6">{t("dashboard.welcome")}</p>
            <h1 className="mt-1 text-3xl font-extrabold tracking-tight text-foreground">{t("dashboard.title")}</h1>
            <p className="mt-2 max-w-xl text-sm text-muted">{t("dashboard.subtitle")}</p>
            <div className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-2 text-sm">
              <button
                type="button"
                className="font-semibold text-brand-6 underline-offset-2 hover:underline"
                onClick={() => startTour(true)}
              >
                {t("tour.start")}
              </button>
              <button
                type="button"
                className="font-semibold text-muted underline-offset-2 hover:text-foreground hover:underline disabled:opacity-50"
                disabled={seed.isPending}
                onClick={() => seed.mutate()}
              >
                {seed.isPending ? t("dashboard.seedDemoBusy") : t("dashboard.seedDemo")}
              </button>
            </div>
            {seed.isSuccess ? (
              <p className="mt-2 text-sm text-muted">
                {seed.data.skipped ? t("dashboard.seedDemoSkip") : t("dashboard.seedDemoDone")}
              </p>
            ) : null}
          </div>
        </div>
        <Button asChild size="lg" className="w-full shrink-0 sm:w-auto sm:min-w-[14rem]">
          <Link to="/positions">{list.length === 0 ? t("dashboard.createFirst") : t("dashboard.openPositions")}</Link>
        </Button>
      </header>

      <section data-tour="tour-funnel" className="hl-rise-delay grid items-start gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Metric label={t("dashboard.openReqs")} value={String(list.length)} hint={t("dashboard.metricOpenHint")} />
        <Metric label={t("dashboard.funnelCandidates")} value={String(totals.candidates)} />
        <Metric label={t("dashboard.funnelEvaluations")} value={String(totals.evaluated)} />
        <Metric
          label={t("dashboard.reviewPending")}
          value={String(totals.review)}
          accent={totals.review > 0 ? "warn" : "default"}
        />
      </section>

      <Card>
        <CardHeader className="flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <CardTitle>{t("dashboard.jobList")}</CardTitle>
            <p className="mt-1 text-sm text-muted">{t("dashboard.jobListHint")}</p>
          </div>
          <Link to="/positions" className="text-sm font-semibold text-brand-6 hover:text-brand-7">
            {t("dashboard.openPositions")}
          </Link>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <div className="grid gap-3 sm:grid-cols-[minmax(0,1fr)_12rem]">
            <Field label={t("dashboard.search")}>
              <TextInput
                value={query}
                placeholder={t("dashboard.searchPlaceholder")}
                onChange={(event) => {
                  setQuery(event.target.value);
                  setPage(1);
                }}
              />
            </Field>
            <Field label={t("dashboard.sort")}>
              <select
                className="w-full rounded-lg border border-border bg-surface px-3 py-2 text-sm"
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
            </Field>
          </div>

          {positions.isLoading ? (
            <div className="space-y-3">
              {Array.from({ length: 4 }).map((_, index) => (
                <div key={index} className="h-16 animate-pulse rounded-xl bg-brand-0/80" />
              ))}
            </div>
          ) : filtered.length === 0 ? (
            <div className="rounded-2xl border border-dashed border-border bg-brand-0 px-5 py-10 text-center">
              <p className="text-lg font-bold text-foreground">
                {list.length === 0 ? t("dashboard.empty") : t("dashboard.noResults")}
              </p>
              {list.length === 0 ? (
                <Button asChild className="mt-4">
                  <Link to="/positions">{t("dashboard.createFirst")}</Link>
                </Button>
              ) : null}
            </div>
          ) : (
            <>
              <ul className="divide-y divide-border rounded-xl border border-border/80">
                {pageRows.map((position) => (
                  <JobRow key={position.id} position={position} />
                ))}
              </ul>
              <Pagination
                page={page}
                pageCount={pageCount}
                totalItems={filtered.length}
                pageSize={PAGE_SIZE}
                onPageChange={setPage}
              />
            </>
          )}
        </CardContent>
      </Card>
    </AppShell>
  );
}

function JobRow({ position }: { position: Position }) {
  const { t } = useTranslation();
  const stats = position.stats;
  const reviewCount = stats?.reviewPendingCount ?? 0;

  return (
    <li className="group flex flex-wrap items-center justify-between gap-4 px-4 py-4 transition-colors hover:bg-brand-0/50">
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-2">
          <p className="truncate font-semibold tracking-tight text-foreground group-hover:text-brand-7">
            {position.title}
          </p>
          {reviewCount > 0 ? (
            <Badge tone="muted">{t("dashboard.reviewBadge", { count: reviewCount })}</Badge>
          ) : null}
        </div>
        <div className="mt-2 flex flex-wrap gap-2">
          <StatChip label={t("dashboard.funnelCandidates")} value={stats?.totalCandidates ?? 0} />
          <StatChip label={t("dashboard.funnelEvaluations")} value={stats?.evaluatedCount ?? 0} />
          <StatChip label={t("dashboard.pending")} value={stats?.pendingCount ?? 0} />
          {(stats?.failedCount ?? 0) > 0 ? (
            <StatChip label={t("dashboard.failed")} value={stats!.failedCount!} tone="danger" />
          ) : null}
        </div>
      </div>
      <div className="flex shrink-0 items-center gap-2">
        {position.slug ? (
          <Button asChild variant="outline" size="sm">
            <a href={`/apply/${position.slug}`} target="_blank" rel="noreferrer">
              {t("dashboard.publicApply")}
            </a>
          </Button>
        ) : null}
        <Button asChild size="sm">
          <Link to="/positions/$positionId" params={{ positionId: position.id }}>
            {t("positions.open")}
          </Link>
        </Button>
      </div>
    </li>
  );
}

function StatChip({
  label,
  value,
  tone = "default"
}: {
  label: string;
  value: number;
  tone?: "default" | "danger";
}) {
  return (
    <span
      className={
        tone === "danger"
          ? "inline-flex items-center gap-1 rounded-full bg-danger/10 px-2.5 py-1 text-xs font-semibold text-danger"
          : "inline-flex items-center gap-1 rounded-full bg-brand-1 px-2.5 py-1 text-xs font-semibold text-brand-7"
      }
    >
      <span className="tabular-nums">{value}</span>
      <span className="font-medium text-muted">{label}</span>
    </span>
  );
}

function Metric({
  label,
  value,
  hint,
  accent = "default"
}: {
  label: string;
  value: string;
  hint?: string;
  accent?: "default" | "warn";
}) {
  return (
    <div className="rounded-2xl border border-border bg-surface px-4 py-4 shadow-card">
      <div className="flex gap-3">
        <span
          aria-hidden="true"
          className={
            accent === "warn"
              ? "mt-0.5 w-1 shrink-0 self-stretch rounded-full bg-amber-500"
              : "mt-0.5 w-1 shrink-0 self-stretch rounded-full bg-brand-6"
          }
        />
        <div className="min-w-0">
          <p className="text-[0.65rem] font-bold uppercase tracking-[0.12em] text-muted">{label}</p>
          <p className="mt-1 text-3xl font-extrabold tabular-nums tracking-tight text-foreground">{value}</p>
          {hint ? <p className="mt-1 text-xs text-muted">{hint}</p> : null}
        </div>
      </div>
    </div>
  );
}
