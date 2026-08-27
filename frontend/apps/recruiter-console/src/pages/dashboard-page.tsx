import type { Candidate, Position } from "@hirelens/api-client";
import { Button, InitialsAvatar, cn } from "@hirelens/ui";
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

const PAGE_SIZE = 6;
type SortKey = "review" | "candidates" | "newest";

type TopCandidate = Candidate & { positionTitle: string };

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
      await queryClient.invalidateQueries({ queryKey: ["analytics"] });
      await queryClient.invalidateQueries({ queryKey: ["dashboard-top"] });
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

  const funnel = useQuery({
    queryKey: ["analytics", "funnel"],
    queryFn: () => api.getFunnel(),
    enabled: Boolean(session)
  });

  const list = positions.data ?? [];

  const topCandidates = useQuery({
    queryKey: ["dashboard-top", list.map((p) => p.id).join(",")],
    enabled: Boolean(session) && list.length > 0,
    queryFn: async (): Promise<TopCandidate[]> => {
      const focus = [...list]
        .filter((p) => (p.stats?.evaluatedCount ?? 0) > 0 || (p.stats?.totalCandidates ?? 0) > 0)
        .sort((a, b) => (b.stats?.evaluatedCount ?? 0) - (a.stats?.evaluatedCount ?? 0))
        .slice(0, 4);
      if (focus.length === 0) {
        return [];
      }
      const batches = await Promise.all(
        focus.map(async (position) => {
          const rows = await api.listCandidates(position.id);
          return rows.map((row) => ({ ...row, positionTitle: position.title }));
        })
      );
      return batches
        .flat()
        .filter((row) => row.overallScore != null)
        .sort((a, b) => (b.overallScore ?? 0) - (a.overallScore ?? 0))
        .slice(0, 5);
    }
  });

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    let rows = [...list];
    if (q) {
      rows = rows.filter((p) => p.title.toLowerCase().includes(q));
    }
    rows.sort((a, b) => {
      switch (sort) {
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
    open: list.length,
    candidates: funnel.data?.candidates ?? list.reduce((s, p) => s + (p.stats?.totalCandidates ?? 0), 0),
    evaluated: funnel.data?.evaluations ?? list.reduce((s, p) => s + (p.stats?.evaluatedCount ?? 0), 0),
    review: list.reduce((s, p) => s + (p.stats?.reviewPendingCount ?? 0), 0),
    interviews: funnel.data?.interviews ?? 0,
    decisions: funnel.data?.decisions ?? 0,
    pending: list.reduce((s, p) => s + (p.stats?.pendingCount ?? 0), 0)
  };

  const avgScore =
    (topCandidates.data?.length ?? 0) > 0
      ? Math.round(
          (topCandidates.data!.reduce((sum, row) => sum + (row.overallScore ?? 0), 0) /
            topCandidates.data!.length) *
            10
        ) / 10
      : null;

  const funnelStages = [
    { key: "new", label: t("dashboard.funnelNew"), value: Math.max(0, totals.candidates - totals.evaluated), color: "bg-sky-400" },
    { key: "review", label: t("dashboard.funnelInReview"), value: totals.review + totals.pending, color: "bg-violet-400" },
    { key: "interview", label: t("dashboard.funnelInterview"), value: totals.interviews, color: "bg-indigo-500" },
    { key: "decision", label: t("dashboard.funnelDecision"), value: totals.decisions, color: "bg-emerald-500" }
  ];
  const funnelMax = Math.max(1, ...funnelStages.map((s) => s.value));

  const activities = buildActivities(list, totals, t);

  return (
    <AppShell>
      <div className="flex min-h-0 flex-1 flex-col gap-5 overflow-y-auto pb-2">
        <header className="flex shrink-0 flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
          <div className="min-w-0">
            <p className="text-xs font-semibold uppercase tracking-[0.12em] text-muted">{workspaceName}</p>
            <h1 className="mt-1 text-2xl font-extrabold tracking-tight sm:text-[1.75rem]">{t("dashboard.title")}</h1>
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

        <section className="shrink-0 rounded-2xl border border-brand-2/80 bg-gradient-to-r from-brand-0 via-white to-violet-50 px-4 py-3.5 sm:px-5">
          <p className="text-sm font-extrabold text-brand-8">{t("dashboard.aiBannerTitle")}</p>
          <p className="mt-1 text-sm leading-relaxed text-muted">{t("dashboard.aiBannerBody")}</p>
        </section>

        <section data-tour="tour-funnel" className="grid shrink-0 grid-cols-2 gap-3 sm:grid-cols-3 xl:grid-cols-5">
          <Kpi icon="◎" label={t("dashboard.openReqs")} value={totals.open} tone="blue" />
          <Kpi icon="◇" label={t("dashboard.funnelCandidates")} value={totals.candidates} tone="violet" />
          <Kpi icon="✓" label={t("dashboard.funnelEvaluations")} value={totals.evaluated} tone="indigo" />
          <Kpi
            icon="!"
            label={t("dashboard.reviewPending")}
            value={totals.review}
            tone="amber"
            emphasize={totals.review > 0}
          />
          <Kpi
            icon="★"
            label={t("dashboard.avgScore")}
            value={avgScore == null ? "—" : avgScore}
            tone="green"
            className="col-span-2 sm:col-span-1"
          />
        </section>

        <section className="grid shrink-0 gap-4 xl:grid-cols-[1.15fr_0.85fr]">
          <div className="rounded-2xl border border-border bg-surface p-5 shadow-card">
            <div className="mb-4 flex items-end justify-between gap-2">
              <div>
                <h2 className="text-base font-extrabold tracking-tight">{t("dashboard.funnelTitle")}</h2>
                <p className="text-xs text-muted">{t("dashboard.funnelHint")}</p>
              </div>
            </div>
            <ul className="flex flex-col gap-3">
              {funnelStages.map((stage) => (
                <li key={stage.key} className="grid grid-cols-[7.5rem_1fr_2.5rem] items-center gap-3">
                  <span className="truncate text-sm font-semibold text-foreground">{stage.label}</span>
                  <div className="h-2.5 overflow-hidden rounded-full bg-brand-0">
                    <div
                      className={cn("h-full rounded-full transition-[width]", stage.color)}
                      style={{ width: `${Math.max(stage.value > 0 ? 8 : 0, Math.round((stage.value / funnelMax) * 100))}%` }}
                    />
                  </div>
                  <span className="text-right text-sm font-extrabold tabular-nums">{stage.value}</span>
                </li>
              ))}
            </ul>
          </div>

          <div className="rounded-2xl border border-border bg-surface p-5 shadow-card">
            <h2 className="text-base font-extrabold tracking-tight">{t("dashboard.activityTitle")}</h2>
            <p className="mb-4 text-xs text-muted">{t("dashboard.activityHint")}</p>
            {activities.length === 0 ? (
              <p className="text-sm text-muted">{t("dashboard.activityEmpty")}</p>
            ) : (
              <ul className="flex flex-col gap-3">
                {activities.map((item) => (
                  <li key={item.id} className="flex gap-3">
                    <span className={cn("mt-1.5 size-2 shrink-0 rounded-full", item.dot)} aria-hidden />
                    <div className="min-w-0">
                      <p className="text-sm font-semibold text-foreground">{item.title}</p>
                      <p className="text-xs text-muted">{item.detail}</p>
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </section>

        <section className="grid shrink-0 gap-4 xl:grid-cols-[1fr_1fr]">
          <div className="rounded-2xl border border-border bg-surface p-5 shadow-card">
            <div className="mb-4 flex items-center justify-between gap-2">
              <div>
                <h2 className="text-base font-extrabold tracking-tight">{t("dashboard.topCandidatesTitle")}</h2>
                <p className="text-xs text-muted">{t("dashboard.topCandidatesHint")}</p>
              </div>
              <Link to="/positions" className="text-xs font-bold text-brand-6 hover:underline">
                {t("dashboard.seeAll")}
              </Link>
            </div>
            {topCandidates.isLoading ? (
              <div className="space-y-2">
                {Array.from({ length: 3 }).map((_, i) => (
                  <div key={i} className="h-14 animate-pulse rounded-xl bg-brand-0" />
                ))}
              </div>
            ) : (topCandidates.data?.length ?? 0) === 0 ? (
              <p className="text-sm text-muted">{t("dashboard.topCandidatesEmpty")}</p>
            ) : (
              <ul className="flex flex-col gap-2">
                {topCandidates.data!.map((row, index) => (
                  <li key={row.id}>
                    <Link
                      to="/candidates/$candidateId"
                      params={{ candidateId: row.id }}
                      className="flex items-center gap-3 rounded-xl border border-transparent px-2 py-2 transition-colors hover:border-border hover:bg-brand-0/50"
                    >
                      <span className="w-5 text-center text-xs font-bold text-muted">{index + 1}</span>
                      <InitialsAvatar name={row.displayName} className="size-9 shrink-0 rounded-full" />
                      <div className="min-w-0 flex-1">
                        <p className="truncate text-sm font-bold">{row.displayName}</p>
                        <p className="truncate text-xs text-muted">{row.positionTitle}</p>
                      </div>
                      <span className="rounded-lg bg-emerald-100 px-2.5 py-1 text-sm font-extrabold tabular-nums text-emerald-800">
                        {Math.round(row.overallScore!)}
                      </span>
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </div>

          <div className="rounded-2xl border border-border bg-surface p-5 shadow-card">
            <div className="mb-4">
              <h2 className="text-base font-extrabold tracking-tight">{t("dashboard.attentionTitle")}</h2>
              <p className="text-xs text-muted">{t("dashboard.attentionHint")}</p>
            </div>
            {list.filter((p) => (p.stats?.reviewPendingCount ?? 0) > 0).length === 0 ? (
              <p className="text-sm text-muted">{t("dashboard.noReviewWaiting")}</p>
            ) : (
              <ul className="flex flex-col gap-2">
                {list
                  .filter((p) => (p.stats?.reviewPendingCount ?? 0) > 0)
                  .sort((a, b) => (b.stats?.reviewPendingCount ?? 0) - (a.stats?.reviewPendingCount ?? 0))
                  .slice(0, 5)
                  .map((position) => (
                    <li key={position.id}>
                      <Link
                        to="/positions/$positionId"
                        params={{ positionId: position.id }}
                        className="flex items-center justify-between gap-3 rounded-xl border border-amber-100 bg-amber-50/50 px-3 py-2.5 transition-colors hover:border-amber-200"
                      >
                        <span className="min-w-0 truncate text-sm font-semibold">{position.title}</span>
                        <span className="shrink-0 rounded-lg bg-amber-100 px-2 py-1 text-xs font-bold text-amber-900">
                          {t("dashboard.reviewBadge", { count: position.stats?.reviewPendingCount ?? 0 })}
                        </span>
                      </Link>
                    </li>
                  ))}
              </ul>
            )}
          </div>
        </section>

        <section data-tour="tour-recent" className="flex min-h-0 flex-col gap-3">
          <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
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
                className="h-9 w-full min-w-[11rem] rounded-xl border border-border bg-white px-3 text-sm outline-none placeholder:text-muted focus-visible:border-brand-5 focus-visible:ring-2 focus-visible:ring-brand-6/15 sm:w-52"
                aria-label={t("dashboard.search")}
              />
              <div className="flex rounded-xl border border-border bg-white p-0.5" role="group" aria-label={t("dashboard.sort")}>
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

          <div className="overflow-hidden rounded-2xl border border-border bg-surface shadow-card">
            {positions.isLoading ? (
              <div className="space-y-0 divide-y divide-border">
                {Array.from({ length: 4 }).map((_, index) => (
                  <div key={index} className="h-16 animate-pulse bg-brand-0/60" />
                ))}
              </div>
            ) : filtered.length === 0 ? (
              <div className="px-6 py-12 text-center">
                <p className="text-base font-bold">{list.length === 0 ? t("dashboard.empty") : t("dashboard.noResults")}</p>
                {list.length === 0 ? (
                  <div className="mt-4 flex flex-wrap items-center justify-center gap-3">
                    <Button asChild size="sm">
                      <Link to="/positions/new">{t("dashboard.createFirst")}</Link>
                    </Button>
                    <button
                      type="button"
                      className="text-sm font-semibold text-muted underline-offset-2 hover:underline disabled:opacity-50"
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
                <ul className="divide-y divide-border">
                  {pageRows.map((position) => (
                    <JobRow key={position.id} position={position} />
                  ))}
                </ul>
                <div className="border-t border-border px-4 py-3">
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
      </div>
    </AppShell>
  );
}

function buildActivities(
  list: Position[],
  totals: { review: number; evaluated: number; candidates: number; interviews: number; decisions: number },
  t: (key: string, opts?: Record<string, string | number>) => string
) {
  const items: { id: string; title: string; detail: string; dot: string }[] = [];
  if (totals.review > 0) {
    items.push({
      id: "review",
      title: t("dashboard.activityReview"),
      detail: t("dashboard.activityReviewDetail", { count: totals.review }),
      dot: "bg-amber-500"
    });
  }
  if (totals.evaluated > 0) {
    items.push({
      id: "scored",
      title: t("dashboard.activityScored"),
      detail: t("dashboard.activityScoredDetail", { count: totals.evaluated }),
      dot: "bg-emerald-500"
    });
  }
  if (totals.interviews > 0) {
    items.push({
      id: "interview",
      title: t("dashboard.activityInterview"),
      detail: t("dashboard.activityInterviewDetail", { count: totals.interviews }),
      dot: "bg-indigo-500"
    });
  }
  if (totals.decisions > 0) {
    items.push({
      id: "decision",
      title: t("dashboard.activityDecision"),
      detail: t("dashboard.activityDecisionDetail", { count: totals.decisions }),
      dot: "bg-brand-5"
    });
  }
  const newest = [...list].sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())[0];
  if (newest) {
    items.push({
      id: "position",
      title: t("dashboard.activityPosition"),
      detail: newest.title,
      dot: "bg-sky-500"
    });
  }
  return items.slice(0, 5);
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
    <li className="flex flex-col gap-3 px-4 py-4 transition-colors hover:bg-brand-0/40 sm:flex-row sm:items-center sm:justify-between sm:px-5">
      <div className="min-w-0 flex-1">
        <Link
          to="/positions/$positionId"
          params={{ positionId: position.id }}
          className="block truncate text-base font-bold tracking-tight hover:text-brand-7"
        >
          {position.title}
        </Link>
        <div className="mt-1.5 flex flex-wrap gap-x-3 gap-y-1 text-xs text-muted">
          <span>
            <strong className="text-foreground">{stats?.totalCandidates ?? 0}</strong> {t("dashboard.colCandidates").toLowerCase()}
          </span>
          <span>
            <strong className="text-foreground">{stats?.evaluatedCount ?? 0}</strong> {t("dashboard.colEvaluated").toLowerCase()}
          </span>
          {review > 0 ? (
            <span className="font-semibold text-amber-800">{t("dashboard.reviewBadge", { count: review })}</span>
          ) : null}
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

function Kpi({
  icon,
  label,
  value,
  tone,
  emphasize,
  className
}: {
  icon: string;
  label: string;
  value: number | string;
  tone: "blue" | "violet" | "indigo" | "amber" | "green";
  emphasize?: boolean;
  className?: string;
}) {
  const toneClass = {
    blue: "bg-sky-100 text-sky-700",
    violet: "bg-violet-100 text-violet-700",
    indigo: "bg-indigo-100 text-indigo-700",
    amber: "bg-amber-100 text-amber-800",
    green: "bg-emerald-100 text-emerald-800"
  }[tone];

  return (
    <div
      className={cn(
        "rounded-2xl border border-border bg-surface p-4 shadow-card",
        emphasize ? "border-amber-200 bg-amber-50/40" : null,
        className
      )}
    >
      <div className="flex items-start justify-between gap-2">
        <p className="text-xs font-semibold text-muted">{label}</p>
        <span className={cn("inline-flex size-7 items-center justify-center rounded-full text-xs font-bold", toneClass)}>
          {icon}
        </span>
      </div>
      <p className="mt-2 text-2xl font-extrabold tabular-nums tracking-tight text-foreground">{value}</p>
    </div>
  );
}
