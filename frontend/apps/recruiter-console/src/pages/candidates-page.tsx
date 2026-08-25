import { Button, Card, CardContent, CardHeader, CardTitle } from "@hirelens/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useParams } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "../api";
import { AppShell } from "../components/app-shell";
import { CandidatesTable } from "../components/candidates-table";
import { CvUploadZone } from "../components/cv-upload-zone";
import { Field, TextInput } from "../components/field";

export function CandidatesPage() {
  const { t } = useTranslation();
  const { positionId } = useParams({ from: "/positions/$positionId" });
  const queryClient = useQueryClient();
  const [displayName, setDisplayName] = useState("");
  const [selectedCandidateId, setSelectedCandidateId] = useState<string | null>(null);
  const [filter, setFilter] = useState("");
  const [sort, setSort] = useState<"score" | "date" | "coverage">("score");

  const position = useQuery({
    queryKey: ["position", positionId],
    queryFn: () => api.getPosition(positionId)
  });
  const candidates = useQuery({
    queryKey: ["candidates", positionId],
    queryFn: () => api.listCandidates(positionId),
    refetchInterval: (query) =>
      (query.state.data ?? []).some((row) => row.recommendedAction === "processing") ? 3000 : false
  });

  const create = useMutation({
    mutationFn: () => api.createCandidate(positionId, displayName),
    onSuccess: async (candidate) => {
      setDisplayName("");
      setSelectedCandidateId(candidate.id);
      await queryClient.invalidateQueries({ queryKey: ["candidates", positionId] });
    }
  });

  const list = useMemo(() => {
    const rows = [...(candidates.data ?? [])];
    rows.sort((a, b) => {
      if (sort === "date") {
        return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
      }
      if (sort === "coverage") {
        return (b.coverageRatio ?? -1) - (a.coverageRatio ?? -1);
      }
      return (b.overallScore ?? -1) - (a.overallScore ?? -1);
    });
    const q = filter.trim().toLowerCase();
    if (!q) {
      return rows;
    }
    return rows.filter(
      (row) =>
        row.displayName.toLowerCase().includes(q) ||
        row.status.toLowerCase().includes(q) ||
        (row.recommendedAction ?? "").includes(q)
    );
  }, [candidates.data, filter, sort]);

  const applySlug = position.data?.slug;

  return (
    <AppShell>
      <div className="flex flex-wrap items-center gap-2 text-sm text-muted">
        <Link to="/positions" className="font-medium text-brand hover:text-brand-7">
          {t("nav.positions")}
        </Link>
        <span aria-hidden="true">/</span>
        <span>{position.data?.title ?? t("candidates.title")}</span>
      </div>

      <div className="rounded-xl border border-brand-3/40 bg-brand-0/50 px-4 py-3 text-sm">
        {t("candidates.rankingDisclaimer")}
      </div>

      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-xs font-semibold uppercase tracking-[0.16em] text-brand">{t("candidates.title")}</p>
          <h1 className="font-display mt-1 text-3xl font-semibold tracking-tight">
            {position.data?.title ?? t("candidates.title")}
          </h1>
          {applySlug ? (
            <p className="mt-2 text-sm text-muted">
              {t("candidates.publicLink")}:{" "}
              <a className="font-medium text-brand underline-offset-2 hover:underline" href={`/apply/${applySlug}`}>
                /apply/{applySlug}
              </a>
            </p>
          ) : null}
        </div>
        <p className="text-sm text-muted">
          {list.length} {t("candidates.count")}
        </p>
      </div>

      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_22rem]">
        <div className="flex flex-col gap-4">
          <div className="flex flex-wrap items-end gap-3">
            <Field label={t("candidates.filter")}>
              <TextInput
                value={filter}
                placeholder={t("candidates.filterPlaceholder")}
                onChange={(event) => setFilter(event.target.value)}
              />
            </Field>
            <Field label={t("candidates.sort")}>
              <select
                className="rounded-lg border border-border px-3 py-2 text-sm"
                value={sort}
                onChange={(event) => setSort(event.target.value as typeof sort)}
              >
                <option value="score">{t("candidates.sortScore")}</option>
                <option value="date">{t("candidates.sortDate")}</option>
                <option value="coverage">{t("candidates.sortCoverage")}</option>
              </select>
            </Field>
          </div>

          {list.length === 0 ? (
            <div className="rounded-lg border border-dashed border-border px-6 py-12 text-center text-sm text-muted">
              {t("candidates.empty")}
            </div>
          ) : (
            <CandidatesTable rows={list} />
          )}
        </div>

        <div className="flex flex-col gap-4">
          <Card>
            <CardHeader>
              <CardTitle className="text-lg">{t("candidates.create")}</CardTitle>
            </CardHeader>
            <CardContent className="flex flex-col gap-3">
              <Field label={t("candidates.displayName")}>
                <TextInput value={displayName} onChange={(event) => setDisplayName(event.target.value)} />
              </Field>
              <Button type="button" disabled={!displayName.trim() || create.isPending} onClick={() => create.mutate()}>
                {t("candidates.create")}
              </Button>
            </CardContent>
          </Card>
          {selectedCandidateId ? (
            <CvUploadZone
              positionId={positionId}
              candidateId={selectedCandidateId}
              onCompleted={() => void queryClient.invalidateQueries({ queryKey: ["candidates", positionId] })}
            />
          ) : (
            <div className="rounded-2xl border border-dashed border-border bg-brand-1/40 px-6 py-10 text-sm text-muted">
              {t("candidates.selectHint")}
            </div>
          )}
        </div>
      </div>
    </AppShell>
  );
}
