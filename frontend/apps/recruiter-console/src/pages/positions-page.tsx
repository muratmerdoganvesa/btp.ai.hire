import { Button } from "@hirelens/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "../api";
import { AppShell } from "../components/app-shell";
import { PageBody, PageHero } from "../components/page-hero";

export function PositionsPage() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [query, setQuery] = useState("");
  const [deletingId, setDeletingId] = useState<string | null>(null);
  const positions = useQuery({ queryKey: ["positions"], queryFn: () => api.listPositions(true) });

  const remove = useMutation({
    mutationFn: (id: string) => api.deletePosition(id),
    onMutate: (id) => setDeletingId(id),
    onSettled: () => setDeletingId(null),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["positions"] });
    }
  });

  const rows = useMemo(() => {
    const q = query.trim().toLowerCase();
    let list = [...(positions.data ?? [])];
    if (q) {
      list = list.filter(
        (position) =>
          position.title.toLowerCase().includes(q) ||
          position.jobDescription.toLowerCase().includes(q) ||
          position.criteria.some((criterion) => criterion.name.toLowerCase().includes(q))
      );
    }
    return list.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
  }, [positions.data, query]);

  return (
    <AppShell>
      <PageHero
        kicker={t("nav.sectionRecruiting")}
        title={t("positions.title")}
        actions={
          <Button asChild size="sm" className="!bg-white !text-[#151f66] hover:!bg-white/90" data-tour="tour-position-create">
            <Link to="/positions/new">{t("positions.create")}</Link>
          </Button>
        }
      />
      <PageBody>
      <p className="text-sm text-muted">{t("positions.listHint")}</p>
      <div className="flex min-h-0 flex-1 flex-col gap-2 overflow-hidden rounded-xl border border-border bg-surface">
        <div className="shrink-0 border-b border-border px-3 py-2 sm:px-4">
          <label className="sr-only" htmlFor="positions-search">
            {t("positions.search")}
          </label>
          <input
            id="positions-search"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder={t("positions.searchPlaceholder")}
            className="h-9 w-full rounded-lg border border-border bg-white px-3 text-sm outline-none placeholder:text-muted focus-visible:border-brand-5 focus-visible:ring-2 focus-visible:ring-brand-6/15"
          />
        </div>

        <div className="min-h-0 flex-1 overflow-auto" data-tour="tour-position-list">
          <table className="w-full min-w-[48rem] text-left text-sm">
            <thead className="sticky top-0 z-10 border-b border-border bg-surface text-[0.7rem] uppercase tracking-wide text-muted">
              <tr>
                <th className="px-3 py-2.5 font-bold sm:px-4">{t("positions.colTitle")}</th>
                <th className="px-3 py-2.5 font-bold">{t("positions.colCriteria")}</th>
                <th className="px-3 py-2.5 font-bold">{t("positions.colCandidates")}</th>
                <th className="px-3 py-2.5 font-bold">{t("positions.colCreated")}</th>
                <th className="w-56 px-3 py-2.5 text-right font-bold sm:px-4">{t("positions.colActions")}</th>
              </tr>
            </thead>
            <tbody>
              {positions.isLoading ? (
                <tr>
                  <td colSpan={5} className="px-4 py-10 text-center text-muted">
                    {t("positions.loading")}
                  </td>
                </tr>
              ) : rows.length === 0 ? (
                <tr>
                  <td colSpan={5} className="px-4 py-10 text-center">
                    <p className="text-muted">{t("positions.empty")}</p>
                    <Button asChild className="mt-4" size="sm">
                      <Link to="/positions/new">{t("positions.create")}</Link>
                    </Button>
                  </td>
                </tr>
              ) : (
                rows.map((position) => (
                  <tr key={position.id} className="border-b border-border last:border-0 hover:bg-brand-0/30">
                    <td className="px-3 py-2 sm:px-4">
                      <p className="font-semibold text-foreground">{position.title}</p>
                      <p className="mt-0.5 line-clamp-1 text-xs text-muted">{position.jobDescription}</p>
                    </td>
                    <td className="px-3 py-2 text-muted">{position.criteria.length}</td>
                    <td className="px-3 py-2 text-muted">{position.stats?.totalCandidates ?? 0}</td>
                    <td className="px-3 py-2 text-muted">
                      {new Date(position.createdAt).toLocaleDateString("tr-TR")}
                    </td>
                    <td className="px-3 py-2 sm:px-4">
                      <div className="flex flex-nowrap items-center justify-end gap-2">
                        <Link
                          to="/positions/$positionId/edit"
                          params={{ positionId: position.id }}
                          className="inline-flex h-8 shrink-0 items-center justify-center whitespace-nowrap rounded-lg border border-border bg-white px-3 text-xs font-semibold text-foreground transition-colors hover:bg-brand-0"
                        >
                          {t("positions.editAction")}
                        </Link>
                        <Link
                          to="/positions/$positionId"
                          params={{ positionId: position.id }}
                          className="inline-flex h-8 shrink-0 items-center justify-center whitespace-nowrap rounded-lg bg-brand-6 px-3 text-xs font-semibold text-white transition-colors hover:bg-brand-7"
                        >
                          {t("positions.open")}
                        </Link>
                        <Button
                          type="button"
                          variant="outline"
                          size="sm"
                          className="h-8 px-3 text-xs text-danger hover:bg-danger-bg"
                          disabled={deletingId === position.id}
                          onClick={() => {
                            if (!window.confirm(t("positions.deleteConfirm"))) {
                              return;
                            }
                            remove.mutate(position.id);
                          }}
                        >
                          {deletingId === position.id ? t("positions.deleting") : t("positions.delete")}
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
      </PageBody>
    </AppShell>
  );
}
