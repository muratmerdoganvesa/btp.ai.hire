import type { CandidateBoardItem } from "@hirelens/api-client";
import { Badge, cn } from "@hirelens/ui";
import { useQuery } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "../api";
import { PageBody, PageHero } from "../components/page-hero";

const stageOrder = [
  "new",
  "reviewing",
  "pre_interview",
  "interview",
  "hold",
  "offer",
  "rejected",
  "pool"
] as const;

const columnAccent: Record<string, string> = {
  new: "border-t-sky-400",
  reviewing: "border-t-violet-400",
  pre_interview: "border-t-indigo-400",
  interview: "border-t-amber-400",
  hold: "border-t-orange-400",
  offer: "border-t-emerald-400",
  rejected: "border-t-rose-400",
  pool: "border-t-slate-300"
};

export function PipelinePage() {
  const { t } = useTranslation();
  const [query, setQuery] = useState("");
  const [positionFilter, setPositionFilter] = useState<string>("all");

  const board = useQuery({
    queryKey: ["candidates-board"],
    queryFn: () => api.listCandidateBoard()
  });

  const rows = board.data ?? [];

  const positions = useMemo(() => {
    const map = new Map<string, string>();
    for (const row of rows) {
      map.set(row.positionId, row.positionTitle);
    }
    return [...map.entries()].sort((a, b) => a[1].localeCompare(b[1], "tr"));
  }, [rows]);

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    return rows.filter((row) => {
      if (positionFilter !== "all" && row.positionId !== positionFilter) {
        return false;
      }
      if (!q) {
        return true;
      }
      return (
        row.displayName.toLowerCase().includes(q) ||
        row.positionTitle.toLowerCase().includes(q) ||
        row.personKey.includes(q)
      );
    });
  }, [rows, query, positionFilter]);

  const columns = useMemo(() => {
    const map = new Map<string, CandidateBoardItem[]>();
    for (const stage of stageOrder) {
      map.set(stage, []);
    }
    for (const row of filtered) {
      const key = stageOrder.includes(row.pipelineStage as (typeof stageOrder)[number])
        ? row.pipelineStage
        : "pool";
      const list = map.get(key) ?? [];
      list.push(row);
      map.set(key, list);
    }
    for (const [, list] of map) {
      list.sort((a, b) => (b.overallScore ?? -1) - (a.overallScore ?? -1));
    }
    return map;
  }, [filtered]);

  return (
    <>
      <PageHero kicker={t("nav.sectionProcess")} title={t("pipeline.title")} />

      <PageBody className="gap-4 overflow-hidden pb-2">
        <p className="text-sm text-muted">{t("pipeline.subtitle")}</p>
        <section className="flex shrink-0 flex-col gap-3 rounded-2xl border border-border bg-surface p-4 shadow-card sm:flex-row sm:items-end">
          <div className="min-w-0 flex-1">
            <label className="text-xs font-bold uppercase tracking-wide text-muted" htmlFor="pipeline-search">
              {t("pipeline.search")}
            </label>
            <input
              id="pipeline-search"
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder={t("pipeline.searchPlaceholder")}
              className="mt-1.5 h-10 w-full rounded-xl border border-border bg-white px-3 text-sm outline-none placeholder:text-muted focus-visible:border-brand-5 focus-visible:ring-2 focus-visible:ring-brand-6/15"
            />
          </div>
          <div className="sm:w-64">
            <label className="text-xs font-bold uppercase tracking-wide text-muted" htmlFor="pipeline-position">
              {t("pipeline.filterJob")}
            </label>
            <select
              id="pipeline-position"
              value={positionFilter}
              onChange={(event) => setPositionFilter(event.target.value)}
              className="mt-1.5 h-10 w-full rounded-xl border border-border bg-white px-3 text-sm outline-none focus-visible:border-brand-5 focus-visible:ring-2 focus-visible:ring-brand-6/15"
            >
              <option value="all">{t("pipeline.allJobs")}</option>
              {positions.map(([id, title]) => (
                <option key={id} value={id}>
                  {title}
                </option>
              ))}
            </select>
          </div>
        </section>

        {board.isLoading ? (
          <div className="grid min-h-0 flex-1 grid-cols-2 gap-3 overflow-hidden lg:grid-cols-4">
            {Array.from({ length: 4 }).map((_, i) => (
              <div key={i} className="animate-pulse rounded-2xl bg-brand-0/60" />
            ))}
          </div>
        ) : rows.length === 0 ? (
          <p className="rounded-2xl border border-dashed border-border bg-surface px-6 py-12 text-center text-sm text-muted">
            {t("pipeline.empty")}
          </p>
        ) : (
          <div className="min-h-0 flex-1 overflow-x-auto pb-2">
            <div className="flex h-full min-h-[28rem] w-max gap-3">
              {stageOrder.map((stage) => {
                const cards = columns.get(stage) ?? [];
                return (
                  <section
                    key={stage}
                    className={cn(
                      "flex w-[17.5rem] shrink-0 flex-col overflow-hidden rounded-2xl border border-border border-t-4 bg-[#f8fafc] shadow-card",
                      columnAccent[stage]
                    )}
                  >
                    <header className="flex items-center justify-between gap-2 border-b border-border bg-white px-3 py-3">
                      <h2 className="text-sm font-extrabold tracking-tight">
                        {t(`candidatesBoard.stages.${stage}`)}
                      </h2>
                      <span className="rounded-full bg-brand-0 px-2 py-0.5 text-xs font-bold tabular-nums text-brand-7">
                        {cards.length}
                      </span>
                    </header>
                    <ul className="flex min-h-0 flex-1 flex-col gap-2 overflow-y-auto p-2">
                      {cards.length === 0 ? (
                        <li className="px-2 py-6 text-center text-xs text-muted">{t("pipeline.columnEmpty")}</li>
                      ) : (
                        cards.map((row) => <PipelineCard key={row.id} row={row} />)
                      )}
                    </ul>
                  </section>
                );
              })}
            </div>
          </div>
        )}
      </PageBody>
    </>
  );
}

function PipelineCard({ row }: { row: CandidateBoardItem }) {
  const { t } = useTranslation();
  return (
    <li>
      <Link
        to="/candidates/$candidateId"
        params={{ candidateId: row.id }}
        className="block rounded-xl border border-border bg-white p-3 shadow-sm transition-colors hover:border-brand-4 hover:bg-brand-0/30"
      >
        <div className="flex items-start justify-between gap-2">
          <p className="truncate text-sm font-bold text-foreground">{row.displayName}</p>
          {row.overallScore != null ? (
            <span className="inline-flex shrink-0 items-center gap-1">
              <span className="text-sm font-extrabold tabular-nums">{Math.round(row.overallScore)}</span>
              <Badge tone="default" className="!rounded-md !px-1.5 !py-0 !text-[0.6rem]">
                AI
              </Badge>
            </span>
          ) : (
            <span className="text-xs text-muted">—</span>
          )}
        </div>
        <p className="mt-1 line-clamp-2 text-xs text-muted">{row.positionTitle}</p>
        {row.siblingApplicationCount > 1 ? (
          <p className="mt-2 text-[0.7rem] font-semibold text-brand-7">
            {t("pipeline.multiApply", { count: row.siblingApplicationCount })}
          </p>
        ) : null}
      </Link>
    </li>
  );
}
