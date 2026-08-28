import type { Offer } from "@hirelens/api-client";
import { Button, cn } from "@hirelens/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "../api";
import { PageBody, PageHero } from "../components/page-hero";

const statusTone: Record<string, string> = {
  draft: "bg-slate-100 text-slate-800",
  sent: "bg-sky-100 text-sky-800",
  accepted: "bg-emerald-100 text-emerald-800",
  declined: "bg-rose-100 text-rose-800",
  withdrawn: "bg-orange-100 text-orange-900"
};

export function OffersPage() {
  const { t } = useTranslation();
  const [query, setQuery] = useState("");
  const [status, setStatus] = useState<string>("all");

  const board = useQuery({
    queryKey: ["offers"],
    queryFn: () => api.listOffers()
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
        row.packageText.toLowerCase().includes(q) ||
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
    () => ["draft", "sent", "accepted", "declined", "withdrawn"].filter((key) => statusCounts.has(key)),
    [statusCounts]
  );

  return (
    <>
      <PageHero
        kicker={t("nav.sectionProcess")}
        title={t("offersBoard.title")}
        actions={
          <p className="text-sm tabular-nums text-white/80">
            {filtered.length} / {rows.length} {t("offersBoard.count")}
          </p>
        }
      />
      <PageBody className="gap-4 pb-2">
        <p className="text-sm text-muted">{t("offersBoard.subtitle")}</p>

        <section className="rounded-2xl border border-border bg-surface p-4 shadow-card">
          <label className="block text-xs font-bold uppercase tracking-wide text-muted" htmlFor="offers-search">
            {t("offersBoard.search")}
          </label>
          <input
            id="offers-search"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder={t("offersBoard.searchPlaceholder")}
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
              {t("offersBoard.allStatuses")} ({rows.length})
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
                {t(`offer.status.${key}`)} ({statusCounts.get(key) ?? 0})
              </button>
            ))}
          </div>
        </section>

        <section className="min-h-0 flex-1 overflow-hidden rounded-2xl border border-border bg-surface shadow-card">
          {board.isLoading ? (
            <p className="px-6 py-12 text-center text-sm text-muted">{t("offersBoard.loading")}</p>
          ) : filtered.length === 0 ? (
            <p className="px-6 py-12 text-center text-sm text-muted">{t("offersBoard.empty")}</p>
          ) : (
            <div className="overflow-x-auto">
              <table className="min-w-full text-left text-sm">
                <thead className="border-b border-border bg-slate-50 text-xs font-bold uppercase tracking-wide text-muted">
                  <tr>
                    <th className="px-4 py-3">{t("offersBoard.colCandidate")}</th>
                    <th className="px-4 py-3">{t("offersBoard.colPosition")}</th>
                    <th className="px-4 py-3">{t("offersBoard.colPackage")}</th>
                    <th className="px-4 py-3">{t("offersBoard.colStatus")}</th>
                    <th className="px-4 py-3">{t("offersBoard.colScore")}</th>
                    <th className="px-4 py-3">{t("offersBoard.colUpdated")}</th>
                    <th className="px-4 py-3 text-right">{t("offersBoard.colActions")}</th>
                  </tr>
                </thead>
                <tbody>
                  {filtered.map((row) => (
                    <OfferRow key={row.id} row={row} />
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </section>
      </PageBody>
    </>
  );
}

function OfferRow({ row }: { row: Offer }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const invalidate = async () => {
    await queryClient.invalidateQueries({ queryKey: ["offers"] });
    await queryClient.invalidateQueries({ queryKey: ["offers", row.candidateId] });
    await queryClient.invalidateQueries({ queryKey: ["candidates-board"] });
  };

  const send = useMutation({ mutationFn: () => api.sendOffer(row.id), onSuccess: invalidate });
  const accept = useMutation({ mutationFn: () => api.acceptOffer(row.id), onSuccess: invalidate });
  const decline = useMutation({ mutationFn: () => api.declineOffer(row.id), onSuccess: invalidate });
  const withdraw = useMutation({ mutationFn: () => api.withdrawOffer(row.id), onSuccess: invalidate });
  const busy = send.isPending || accept.isPending || decline.isPending || withdraw.isPending;

  return (
    <tr className="border-b border-border last:border-0 hover:bg-brand-0/40">
      <td className="px-4 py-3 font-semibold text-foreground">{row.candidateName}</td>
      <td className="px-4 py-3 text-muted">{row.positionTitle}</td>
      <td className="max-w-xs truncate px-4 py-3 text-muted" title={row.packageText}>
        {row.packageText}
      </td>
      <td className="px-4 py-3">
        <span className={cn("inline-flex rounded-md px-2 py-1 text-xs font-bold", statusTone[row.status] ?? "bg-slate-100 text-slate-700")}>
          {t(`offer.status.${row.status}`, { defaultValue: row.status })}
        </span>
      </td>
      <td className="px-4 py-3 tabular-nums font-semibold">
        {row.scoreSnapshot != null ? row.scoreSnapshot : "—"}
      </td>
      <td className="px-4 py-3 text-muted">{new Date(row.updatedAt).toLocaleString()}</td>
      <td className="px-4 py-3">
        <div className="flex flex-wrap items-center justify-end gap-2">
          {row.status === "draft" ? (
            <>
              <Button type="button" className="h-8 px-3 text-xs" disabled={busy} onClick={() => send.mutate()}>
                {t("offer.send")}
              </Button>
              <Button type="button" variant="outline" className="h-8 px-3 text-xs" disabled={busy} onClick={() => withdraw.mutate()}>
                {t("offer.withdraw")}
              </Button>
            </>
          ) : null}
          {row.status === "sent" ? (
            <>
              <Button type="button" className="h-8 px-3 text-xs" disabled={busy} onClick={() => accept.mutate()}>
                {t("offer.accept")}
              </Button>
              <Button type="button" variant="outline" className="h-8 px-3 text-xs" disabled={busy} onClick={() => decline.mutate()}>
                {t("offer.decline")}
              </Button>
              <Button type="button" variant="outline" className="h-8 px-3 text-xs" disabled={busy} onClick={() => withdraw.mutate()}>
                {t("offer.withdraw")}
              </Button>
            </>
          ) : null}
          <Link
            to="/candidates/$candidateId"
            params={{ candidateId: row.candidateId }}
            className="inline-flex h-8 items-center rounded-lg border border-border bg-white px-3 text-xs font-bold text-brand-7 hover:bg-brand-0"
          >
            {t("offersBoard.open")}
          </Link>
        </div>
      </td>
    </tr>
  );
}
