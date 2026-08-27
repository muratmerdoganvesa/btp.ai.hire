import type { InterviewBoardItem } from "@hirelens/api-client";
import { cn } from "@hirelens/ui";
import { useQuery } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "../api";
import { AppShell } from "../components/app-shell";
import { PageBody, PageHero } from "../components/page-hero";

const statusTone: Record<string, string> = {
  invited: "bg-sky-100 text-sky-800",
  disclosed: "bg-indigo-100 text-indigo-800",
  in_progress: "bg-amber-100 text-amber-900",
  started: "bg-amber-100 text-amber-900",
  paused: "bg-orange-100 text-orange-900",
  completed: "bg-emerald-100 text-emerald-800",
  cancelled: "bg-rose-100 text-rose-800"
};

export function InterviewsPage() {
  const { t } = useTranslation();
  const [query, setQuery] = useState("");
  const [status, setStatus] = useState<string>("all");

  const board = useQuery({
    queryKey: ["interviews-board"],
    queryFn: () => api.listInterviews()
  });

  const rows = board.data ?? [];

  const filtered = useMemo(() => {
    const q = query.trim().toLowerCase();
    return rows.filter((row) => {
      if (status !== "all" && row.status !== status) {
        return false;
      }
      if (!q) {
        return true;
      }
      return (
        row.candidateName.toLowerCase().includes(q) ||
        row.positionTitle.toLowerCase().includes(q) ||
        row.status.toLowerCase().includes(q)
      );
    });
  }, [rows, query, status]);

  const statusCounts = useMemo(() => {
    const map = new Map<string, number>();
    for (const row of rows) {
      map.set(row.status, (map.get(row.status) ?? 0) + 1);
    }
    return map;
  }, [rows]);

  const statuses = useMemo(
    () => [...statusCounts.keys()].sort((a, b) => a.localeCompare(b)),
    [statusCounts]
  );

  return (
    <AppShell>
      <PageHero
        kicker={t("nav.sectionProcess")}
        title={t("interviewsBoard.title")}
        actions={
          <p className="text-sm tabular-nums text-white/80">
            {filtered.length} / {rows.length} {t("interviewsBoard.count")}
          </p>
        }
      />
      <PageBody className="gap-4 pb-2">
        <p className="text-sm text-muted">{t("interviewsBoard.subtitle")}</p>

        <section className="rounded-2xl border border-border bg-surface p-4 shadow-card">
          <label className="block text-xs font-bold uppercase tracking-wide text-muted" htmlFor="interviews-search">
            {t("interviewsBoard.search")}
          </label>
          <input
            id="interviews-search"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder={t("interviewsBoard.searchPlaceholder")}
            className="mt-2 h-11 w-full rounded-xl border border-border bg-white px-4 text-sm outline-none placeholder:text-muted focus-visible:border-brand-5 focus-visible:ring-2 focus-visible:ring-brand-6/15"
          />
          <div className="mt-3 flex flex-wrap items-center gap-2">
            <button
              type="button"
              onClick={() => setStatus("all")}
              className={cn(
                "rounded-lg px-3 py-1.5 text-xs font-bold transition-colors",
                status === "all" ? "bg-brand-6 text-white" : "bg-slate-100 text-slate-600 hover:bg-slate-200"
              )}
            >
              {t("interviewsBoard.allStatuses")} ({rows.length})
            </button>
            {statuses.map((key) => (
              <button
                key={key}
                type="button"
                onClick={() => setStatus(key)}
                className={cn(
                  "rounded-lg px-3 py-1.5 text-xs font-bold transition-colors",
                  status === key ? "bg-brand-6 text-white" : "bg-slate-100 text-slate-600 hover:bg-slate-200"
                )}
              >
                {statusLabel(key, t)} ({statusCounts.get(key) ?? 0})
              </button>
            ))}
          </div>
        </section>

        <section className="min-h-0 flex-1 overflow-hidden rounded-2xl border border-border bg-surface shadow-card">
          {board.isLoading ? (
            <p className="px-6 py-12 text-center text-sm text-muted">{t("interviewsBoard.loading")}</p>
          ) : filtered.length === 0 ? (
            <p className="px-6 py-12 text-center text-sm text-muted">{t("interviewsBoard.empty")}</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="min-w-full text-left text-sm">
                <thead className="border-b border-border bg-slate-50 text-xs font-bold uppercase tracking-wide text-muted">
                  <tr>
                    <th className="px-4 py-3">{t("interviewsBoard.colCandidate")}</th>
                    <th className="px-4 py-3">{t("interviewsBoard.colPosition")}</th>
                    <th className="px-4 py-3">{t("interviewsBoard.colStatus")}</th>
                    <th className="px-4 py-3">{t("interviewsBoard.colProgress")}</th>
                    <th className="px-4 py-3">{t("interviewsBoard.colScore")}</th>
                    <th className="px-4 py-3">{t("interviewsBoard.colSent")}</th>
                    <th className="px-4 py-3 text-right">{t("interviewsBoard.colActions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {filtered.map((row) => (
                    <InterviewRow key={row.id} row={row} />
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      </PageBody>
    </AppShell>
  );
}

function InterviewRow({ row }: { row: InterviewBoardItem }) {
  const { t } = useTranslation();
  return (
    <tr className="border-b border-border last:border-0 hover:bg-brand-0/40">
      <td className="px-4 py-3 font-semibold text-foreground">{row.candidateName}</td>
      <td className="px-4 py-3 text-muted">{row.positionTitle}</td>
      <td className="px-4 py-3">
        <span className={cn("inline-flex rounded-md px-2 py-1 text-xs font-bold", statusTone[row.status] ?? "bg-slate-100 text-slate-700")}>
          {statusLabel(row.status, t)}
        </span>
      </td>
      <td className="px-4 py-3 tabular-nums text-muted">
        {row.answerCount}/{row.questionCount || "—"}
      </td>
      <td className="px-4 py-3 tabular-nums font-semibold">
        {row.interviewScore != null ? Math.round(row.interviewScore) : "—"}
      </td>
      <td className="px-4 py-3 text-muted">{new Date(row.createdAt).toLocaleString()}</td>
      <td className="px-4 py-3 text-right">
        <Link
          to="/interviews/$sessionId"
          params={{ sessionId: row.id }}
          className="inline-flex h-8 items-center rounded-lg border border-border bg-white px-3 text-xs font-bold text-brand-7 hover:bg-brand-0"
        >
          {t("interviewsBoard.open")}
        </Link>
      </td>
    </tr>
  );
}

function statusLabel(status: string, t: (key: string) => string): string {
  const map: Record<string, string> = {
    invited: "interview.statusInvited",
    disclosed: "interview.statusDisclosed",
    in_progress: "interview.statusStarted",
    started: "interview.statusStarted",
    paused: "interview.statusPaused",
    completed: "interview.statusCompleted",
    cancelled: "interview.statusCancelled"
  };
  return map[status] ? t(map[status]) : status;
}
