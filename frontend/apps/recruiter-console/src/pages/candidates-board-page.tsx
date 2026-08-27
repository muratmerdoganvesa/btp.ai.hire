import type { CandidateBoardItem } from "@hirelens/api-client";
import { Badge, Button, cn } from "@hirelens/ui";
import { useQuery } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "../api";
import { AppShell } from "../components/app-shell";
import { PageBody, PageHero } from "../components/page-hero";

type StageFilter = "all" | string;

const stageOrder = [
  "new",
  "pool",
  "reviewing",
  "pre_interview",
  "interview",
  "hold",
  "offer",
  "rejected"
] as const;

const stageTone: Record<string, string> = {
  new: "bg-sky-100 text-sky-800",
  pool: "bg-slate-100 text-slate-700",
  reviewing: "bg-violet-100 text-violet-800",
  pre_interview: "bg-indigo-100 text-indigo-800",
  interview: "bg-amber-100 text-amber-900",
  hold: "bg-orange-100 text-orange-900",
  offer: "bg-emerald-100 text-emerald-800",
  rejected: "bg-rose-100 text-rose-800"
};

export function CandidatesBoardPage() {
  const { t } = useTranslation();
  const [query, setQuery] = useState("");
  const [stage, setStage] = useState<StageFilter>("all");
  const [groupPeople, setGroupPeople] = useState(true);

  const board = useQuery({
    queryKey: ["candidates-board"],
    queryFn: () => api.listCandidateBoard()
  });

  const rows = board.data ?? [];

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    return rows.filter((row) => {
      if (stage !== "all" && row.pipelineStage !== stage) {
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
  }, [rows, query, stage]);

  const stageCounts = useMemo(() => {
    const map = new Map<string, number>();
    for (const row of rows) {
      map.set(row.pipelineStage, (map.get(row.pipelineStage) ?? 0) + 1);
    }
    return map;
  }, [rows]);

  const displayRows = useMemo(() => {
    if (!groupPeople) {
      return filtered.map((row) => ({ kind: "app" as const, row }));
    }

    const groups = new Map<string, CandidateBoardItem[]>();
    for (const row of filtered) {
      const list = groups.get(row.personKey) ?? [];
      list.push(row);
      groups.set(row.personKey, list);
    }

    const items: Array<{ kind: "person"; rows: CandidateBoardItem[] } | { kind: "app"; row: CandidateBoardItem }> =
      [];
    for (const [, apps] of groups) {
      apps.sort((a, b) => (b.overallScore ?? -1) - (a.overallScore ?? -1));
      if (apps.length === 1) {
        items.push({ kind: "app", row: apps[0]! });
      } else {
        items.push({ kind: "person", rows: apps });
      }
    }
    items.sort((a, b) => {
      const scoreA = a.kind === "app" ? a.row.overallScore ?? -1 : Math.max(...a.rows.map((r) => r.overallScore ?? -1));
      const scoreB = b.kind === "app" ? b.row.overallScore ?? -1 : Math.max(...b.rows.map((r) => r.overallScore ?? -1));
      return scoreB - scoreA;
    });
    return items;
  }, [filtered, groupPeople]);

  return (
    <AppShell>
      <PageHero
        title={t("candidatesBoard.title")}
        description={t("candidatesBoard.subtitle")}
        actions={
          <p className="text-sm tabular-nums text-white/80">
            {filtered.length} / {rows.length} {t("candidatesBoard.count")}
          </p>
        }
      />

      <PageBody className="gap-4 pb-2">
        <section className="rounded-2xl border border-border bg-surface p-4 shadow-card">
          <label className="block text-xs font-bold uppercase tracking-wide text-muted" htmlFor="candidates-search">
            {t("candidatesBoard.search")}
          </label>
          <input
            id="candidates-search"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder={t("candidatesBoard.searchPlaceholder")}
            className="mt-2 h-11 w-full rounded-xl border border-border bg-white px-4 text-sm outline-none placeholder:text-muted focus-visible:border-brand-5 focus-visible:ring-2 focus-visible:ring-brand-6/15"
          />
          <div className="mt-3 flex flex-wrap items-center gap-2">
            <button
              type="button"
              onClick={() => setStage("all")}
              className={cn(
                "rounded-full px-3 py-1.5 text-xs font-bold transition-colors",
                stage === "all" ? "bg-brand-6 text-white" : "bg-brand-0 text-muted hover:text-foreground"
              )}
            >
              {t("candidatesBoard.allStages")} ({rows.length})
            </button>
            {stageOrder.map((key) => {
              const count = stageCounts.get(key) ?? 0;
              if (count === 0) {
                return null;
              }
              return (
                <button
                  key={key}
                  type="button"
                  onClick={() => setStage(key)}
                  className={cn(
                    "rounded-full px-3 py-1.5 text-xs font-bold transition-colors",
                    stage === key ? "bg-brand-6 text-white" : "bg-brand-0 text-muted hover:text-foreground"
                  )}
                >
                  {t(`candidatesBoard.stages.${key}`)} ({count})
                </button>
              );
            })}
            <label className="ml-auto flex items-center gap-2 text-xs font-semibold text-muted">
              <input
                type="checkbox"
                checked={groupPeople}
                onChange={(event) => setGroupPeople(event.target.checked)}
                className="size-4 rounded border-border"
              />
              {t("candidatesBoard.groupPeople")}
            </label>
          </div>
        </section>

        <section className="overflow-hidden rounded-2xl border border-border bg-surface shadow-card">
          {board.isLoading ? (
            <div className="space-y-0 divide-y divide-border">
              {Array.from({ length: 6 }).map((_, i) => (
                <div key={i} className="h-14 animate-pulse bg-brand-0/50" />
              ))}
            </div>
          ) : filtered.length === 0 ? (
            <p className="px-6 py-12 text-center text-sm text-muted">
              {rows.length === 0 ? t("candidatesBoard.empty") : t("candidatesBoard.noResults")}
            </p>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full min-w-[56rem] text-left text-sm">
                <thead className="border-b border-border bg-brand-0/40 text-[0.7rem] uppercase tracking-wide text-muted">
                  <tr>
                    <th className="px-4 py-3 font-bold">{t("candidatesBoard.colCandidate")}</th>
                    <th className="px-4 py-3 font-bold">{t("candidatesBoard.colJob")}</th>
                    <th className="px-4 py-3 font-bold">{t("candidatesBoard.colScore")}</th>
                    <th className="px-4 py-3 font-bold">{t("candidatesBoard.colStage")}</th>
                    <th className="px-4 py-3 font-bold">{t("candidatesBoard.colApps")}</th>
                    <th className="px-4 py-3 text-right font-bold">{t("candidatesBoard.colActions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {displayRows.map((item) =>
                    item.kind === "app" ? (
                      <ApplicationRow key={item.row.id} row={item.row} />
                    ) : (
                      <PersonGroupRows key={item.rows[0]!.personKey} apps={item.rows} />
                    )
                  )}
                </tbody>
              </table>
            </div>
          )}
        </section>
      </PageBody>
    </AppShell>
  );
}

function PersonGroupRows({ apps }: { apps: CandidateBoardItem[] }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(true);
  const primary = apps[0]!;

  return (
    <>
      <tr className="border-b border-border bg-brand-0/30">
        <td className="px-4 py-3" colSpan={6}>
          <button
            type="button"
            onClick={() => setOpen((v) => !v)}
            className="flex w-full items-center justify-between gap-3 text-left"
          >
            <span>
              <span className="font-bold text-foreground">{primary.displayName}</span>
              <span className="ml-2 text-xs font-semibold text-brand-7">
                {t("candidatesBoard.multiApply", { count: apps.length })}
              </span>
            </span>
            <span className="text-xs font-bold text-muted">{open ? "−" : "+"}</span>
          </button>
        </td>
      </tr>
      {open
        ? apps.map((row) => <ApplicationRow key={row.id} row={row} nested />)
        : null}
    </>
  );
}

function ApplicationRow({ row, nested = false }: { row: CandidateBoardItem; nested?: boolean }) {
  const { t } = useTranslation();
  return (
    <tr className={cn("border-b border-border/80 last:border-0 hover:bg-brand-0/35", nested && "bg-white")}>
      <td className={cn("px-4 py-3", nested && "pl-8")}>
        <Link
          to="/candidates/$candidateId"
          params={{ candidateId: row.id }}
          className="font-semibold text-brand-6 hover:underline"
        >
          {row.displayName}
        </Link>
      </td>
      <td className="px-4 py-3">
        <Link
          to="/positions/$positionId"
          params={{ positionId: row.positionId }}
          className="text-sm text-foreground hover:text-brand-7"
        >
          {row.positionTitle}
        </Link>
      </td>
      <td className="px-4 py-3">
        {row.overallScore == null ? (
          <span className="text-muted">—</span>
        ) : (
          <span className="inline-flex items-center gap-1.5">
            <span className="font-extrabold tabular-nums text-foreground">{Math.round(row.overallScore)}</span>
            <Badge tone="default" className="!rounded-md !px-1.5 !text-[0.65rem]">
              AI
            </Badge>
          </span>
        )}
      </td>
      <td className="px-4 py-3">
        <span
          className={cn(
            "inline-flex rounded-full px-2.5 py-1 text-xs font-bold",
            stageTone[row.pipelineStage] ?? stageTone.pool
          )}
        >
          {t(`candidatesBoard.stages.${row.pipelineStage}`, {
            defaultValue: row.pipelineStage
          })}
        </span>
      </td>
      <td className="px-4 py-3 tabular-nums text-muted">
        {row.siblingApplicationCount > 1 ? row.siblingApplicationCount : "1"}
      </td>
      <td className="px-4 py-3 text-right">
        <Button asChild size="sm" variant="outline">
          <Link to="/candidates/$candidateId" params={{ candidateId: row.id }}>
            {t("candidatesBoard.open")}
          </Link>
        </Button>
      </td>
    </tr>
  );
}
