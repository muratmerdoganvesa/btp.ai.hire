import { Button, Card, CardContent } from "@hirelens/ui";
import { useQuery } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "../api";
import { AppShell } from "../components/app-shell";
import { Field, TextInput } from "../components/field";

export function PositionsPage() {
  const { t } = useTranslation();
  const [query, setQuery] = useState("");
  const positions = useQuery({ queryKey: ["positions"], queryFn: () => api.listPositions(true) });

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
      <header className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <h1 className="text-3xl font-extrabold tracking-tight">{t("positions.title")}</h1>
          <p className="mt-1 text-sm text-muted">{t("positions.listHint")}</p>
        </div>
        <Button asChild data-tour="tour-position-create">
          <Link to="/positions/new">{t("positions.create")}</Link>
        </Button>
      </header>

      <Card>
        <CardContent className="flex flex-col gap-4 !py-5">
          <Field label={t("positions.search")}>
            <TextInput
              value={query}
              onChange={(event) => setQuery(event.target.value)}
              placeholder={t("positions.searchPlaceholder")}
            />
          </Field>

          <div className="overflow-x-auto rounded-xl border border-border" data-tour="tour-position-list">
            <table className="w-full min-w-[48rem] text-left text-sm">
              <thead className="border-b border-border bg-brand-0/50 text-xs uppercase tracking-wide text-muted">
                <tr>
                  <th className="px-4 py-3 font-bold">{t("positions.colTitle")}</th>
                  <th className="px-4 py-3 font-bold">{t("positions.colCriteria")}</th>
                  <th className="px-4 py-3 font-bold">{t("positions.colCandidates")}</th>
                  <th className="px-4 py-3 font-bold">{t("positions.colCreated")}</th>
                  <th className="px-4 py-3 text-right font-bold">{t("positions.colActions")}</th>
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
                      <td className="px-4 py-3">
                        <p className="font-semibold text-foreground">{position.title}</p>
                        <p className="mt-0.5 line-clamp-1 text-xs text-muted">{position.jobDescription}</p>
                      </td>
                      <td className="px-4 py-3 text-muted">{position.criteria.length}</td>
                      <td className="px-4 py-3 text-muted">{position.stats?.totalCandidates ?? 0}</td>
                      <td className="px-4 py-3 text-muted">
                        {new Date(position.createdAt).toLocaleDateString("tr-TR")}
                      </td>
                      <td className="px-4 py-3">
                        <div className="flex justify-end gap-2">
                          <Button asChild variant="outline" size="sm">
                            <Link to="/positions/$positionId/edit" params={{ positionId: position.id }}>
                              {t("positions.editAction")}
                            </Link>
                          </Button>
                          <Button asChild size="sm">
                            <Link to="/positions/$positionId" params={{ positionId: position.id }}>
                              {t("positions.open")}
                            </Link>
                          </Button>
                        </div>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </CardContent>
      </Card>
    </AppShell>
  );
}
