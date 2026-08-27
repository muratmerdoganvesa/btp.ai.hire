import { Button, cn } from "@hirelens/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useParams } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "../api";
import { AddCandidateDialog } from "../components/add-candidate-dialog";
import { AppShell } from "../components/app-shell";
import { CandidatesTable } from "../components/candidates-table";
import { CvUploadZone } from "../components/cv-upload-zone";

type SourceMode = "choose" | "sf";
type SortMode = "score" | "date" | "coverage";

export function CandidatesPage() {
  const { t } = useTranslation();
  const { positionId } = useParams({ from: "/positions/$positionId" });
  const queryClient = useQueryClient();
  const [selectedCandidateId, setSelectedCandidateId] = useState<string | null>(null);
  const [filter, setFilter] = useState("");
  const [sort, setSort] = useState<SortMode>("score");
  const [mode, setMode] = useState<SourceMode>("choose");
  const [linkCopied, setLinkCopied] = useState(false);
  const [sfMessage, setSfMessage] = useState<string | null>(null);
  const [addOpen, setAddOpen] = useState(false);

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

  const pullSf = useMutation({
    mutationFn: () => api.pullSfCandidates(positionId),
    onSuccess: async (result) => {
      setSfMessage(t("candidates.sfDone", { count: result.imported }));
      setMode("choose");
      await queryClient.invalidateQueries({ queryKey: ["candidates", positionId] });
    },
    onError: () => setSfMessage(t("candidates.sfError"))
  });

  const removeCandidate = useMutation({
    mutationFn: (id: string) => api.deleteCandidate(id),
    onSuccess: async (_data, id) => {
      if (selectedCandidateId === id) {
        setSelectedCandidateId(null);
      }
      await queryClient.invalidateQueries({ queryKey: ["candidates", positionId] });
      await queryClient.invalidateQueries({ queryKey: ["positions"] });
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
      const scoreDiff = (b.overallScore ?? -1) - (a.overallScore ?? -1);
      if (scoreDiff !== 0) {
        return scoreDiff;
      }
      return (b.coverageRatio ?? -1) - (a.coverageRatio ?? -1);
    });
    const q = filter.trim().toLowerCase();
    if (!q) {
      return rows;
    }
    return rows.filter((row) => row.displayName.toLowerCase().includes(q));
  }, [candidates.data, filter, sort]);

  const applySlug = position.data?.slug;
  const applyHref = applySlug ? `${window.location.origin}/apply/${applySlug}` : null;
  const selected = list.find((row) => row.id === selectedCandidateId) ?? null;
  const isEmpty = !candidates.isLoading && list.length === 0;
  const showChooser = isEmpty && mode === "choose";
  const scoredCount = list.filter((row) => row.overallScore != null).length;

  const copyApplyLink = async () => {
    if (!applyHref) {
      return;
    }
    await navigator.clipboard.writeText(applyHref);
    setLinkCopied(true);
    window.setTimeout(() => setLinkCopied(false), 1600);
  };

  const clearSelection = () => setSelectedCandidateId(null);

  const sortOptions: { id: SortMode; label: string }[] = [
    { id: "score", label: t("candidates.sortScore") },
    { id: "coverage", label: t("candidates.sortCoverage") },
    { id: "date", label: t("candidates.sortDate") }
  ];

  return (
    <AppShell>
      <header className="flex shrink-0 flex-col gap-3 border-b border-border pb-4 sm:flex-row sm:items-end sm:justify-between">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-2 text-xs font-medium uppercase tracking-wide text-muted">
            <Link to="/positions" className="text-brand-6 hover:underline">
              {t("nav.positions")}
            </Link>
            <span aria-hidden="true">/</span>
            <span>{t("candidates.title")}</span>
          </div>
          <h1 className="mt-1 truncate text-xl font-extrabold leading-tight tracking-tight sm:text-2xl">
            {position.data?.title ?? t("candidates.title")}
          </h1>
          <p className="mt-1 text-sm text-muted">{t("candidates.pageHint")}</p>
          {applySlug ? (
            <div className="mt-2 flex flex-wrap items-center gap-2 text-sm">
              <span className="text-muted">{t("candidates.publicLink")}</span>
              <a className="font-medium text-brand-6 underline-offset-2 hover:underline" href={`/apply/${applySlug}`}>
                /apply/{applySlug}
              </a>
              <Button type="button" variant="outline" size="sm" onClick={() => void copyApplyLink()}>
                {linkCopied ? t("candidates.copied") : t("candidates.copyLink")}
              </Button>
            </div>
          ) : null}
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Button type="button" size="sm" onClick={() => setAddOpen(true)}>
            {t("candidates.addManual")}
          </Button>
          {!isEmpty ? (
            <Button type="button" variant="outline" size="sm" onClick={() => setMode("sf")}>
              {t("candidates.sfTitle")}
            </Button>
          ) : null}
        </div>
      </header>

      {showChooser ? (
        <section className="grid shrink-0 gap-3 md:grid-cols-2">
          <SourcePanel
            title={t("candidates.sfTitle")}
            body={t("candidates.sfBody")}
            actionLabel={pullSf.isPending ? t("candidates.sfWorking") : t("candidates.sfAction")}
            disabled={pullSf.isPending}
            onAction={() => {
              setSfMessage(null);
              pullSf.mutate();
            }}
            emphasize
          />
          <SourcePanel
            title={t("candidates.addManualTitle")}
            body={t("candidates.addManualChooserBody")}
            actionLabel={t("candidates.addManual")}
            onAction={() => setAddOpen(true)}
          />
        </section>
      ) : null}

      {mode === "sf" && !showChooser ? (
        <section className="flex shrink-0 flex-wrap items-center gap-3 rounded-xl border border-border bg-surface px-4 py-3">
          <div className="min-w-0 flex-1">
            <p className="text-sm font-extrabold">{t("candidates.sfTitle")}</p>
            <p className="text-sm text-muted">{t("candidates.sfBody")}</p>
          </div>
          <Button type="button" variant="outline" size="sm" onClick={() => setMode("choose")}>
            {t("candidates.backToSources")}
          </Button>
          <Button type="button" size="sm" disabled={pullSf.isPending} onClick={() => pullSf.mutate()}>
            {pullSf.isPending ? t("candidates.sfWorking") : t("candidates.sfAction")}
          </Button>
        </section>
      ) : null}

      {selected ? (
        <section className="flex shrink-0 flex-col gap-3 rounded-2xl border border-brand-4/50 bg-brand-0/40 p-4 sm:max-w-xl">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div>
              <p className="text-sm font-extrabold">{t("candidates.uploadFor", { name: selected.displayName })}</p>
              <p className="text-sm text-muted">{t("candidates.uploadEditHint")}</p>
            </div>
            <Button type="button" variant="outline" size="sm" onClick={clearSelection}>
              {t("candidates.closeUpload")}
            </Button>
          </div>
          <CvUploadZone
            positionId={positionId}
            candidateId={selected.id}
            onCompleted={() => void queryClient.invalidateQueries({ queryKey: ["candidates", positionId] })}
          />
        </section>
      ) : null}

      {sfMessage ? (
        <p
          className={cn(
            "shrink-0 rounded-lg px-3 py-2 text-sm",
            pullSf.isError ? "bg-danger-bg text-danger" : "bg-brand-0 text-brand-7"
          )}
          role="status"
        >
          {sfMessage}
        </p>
      ) : null}

      {!isEmpty ? (
        <section className="flex min-h-0 flex-1 flex-col gap-3 overflow-hidden">
          <div className="flex shrink-0 flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex flex-wrap items-center gap-2">
              <p className="text-sm font-semibold text-foreground">
                {t("candidates.scoredSummary", { scored: scoredCount, total: list.length })}
              </p>
              <span className="text-xs text-muted">{t("candidates.rankingDisclaimer")}</span>
            </div>
            <div className="flex flex-wrap items-center gap-2">
              <label className="sr-only" htmlFor="candidate-search">
                {t("candidates.filter")}
              </label>
              <input
                id="candidate-search"
                value={filter}
                placeholder={t("candidates.filterPlaceholder")}
                onChange={(event) => setFilter(event.target.value)}
                className="h-9 min-w-[10rem] flex-1 rounded-xl border border-border bg-surface px-3 text-sm outline-none placeholder:text-muted focus-visible:border-brand-5 focus-visible:ring-2 focus-visible:ring-brand-6/15 sm:max-w-[14rem]"
              />
              <div className="flex rounded-xl border border-border bg-surface p-0.5" role="group" aria-label={t("candidates.sort")}>
                {sortOptions.map((option) => (
                  <button
                    key={option.id}
                    type="button"
                    onClick={() => setSort(option.id)}
                    className={cn(
                      "h-8 rounded-lg px-3 text-xs font-bold transition-colors",
                      sort === option.id
                        ? "bg-brand-6 text-white"
                        : "text-muted hover:bg-brand-0 hover:text-foreground"
                    )}
                  >
                    {option.label}
                  </button>
                ))}
              </div>
            </div>
          </div>

          <div className="min-h-0 flex-1 overflow-auto">
            {candidates.isLoading ? (
              <p className="rounded-2xl border border-dashed border-border px-4 py-12 text-center text-sm text-muted">
                {t("positions.loading")}
              </p>
            ) : list.length === 0 ? (
              <p className="rounded-2xl border border-dashed border-border px-4 py-12 text-center text-sm text-muted">
                {t("candidates.filterEmpty")}
              </p>
            ) : (
              <CandidatesTable
                rows={list}
                selectedId={selectedCandidateId}
                onSelect={(id) => setSelectedCandidateId(id)}
                deletingId={removeCandidate.isPending ? removeCandidate.variables ?? null : null}
                onDelete={(id) => removeCandidate.mutate(id)}
              />
            )}
          </div>
        </section>
      ) : mode === "choose" ? (
        <p className="rounded-2xl border border-dashed border-border px-4 py-10 text-center text-sm text-muted">
          {t("candidates.empty")}
        </p>
      ) : null}

      <AddCandidateDialog
        open={addOpen}
        positionId={positionId}
        onClose={() => setAddOpen(false)}
        onCreated={async (candidateId) => {
          await queryClient.invalidateQueries({ queryKey: ["candidates", positionId] });
          setSelectedCandidateId(candidateId);
          setMode("choose");
        }}
      />
    </AppShell>
  );
}

function SourcePanel({
  title,
  body,
  actionLabel,
  onAction,
  disabled,
  emphasize
}: {
  title: string;
  body: string;
  actionLabel: string;
  onAction: () => void;
  disabled?: boolean;
  emphasize?: boolean;
}) {
  return (
    <div
      className={cn(
        "flex flex-col gap-3 rounded-2xl border bg-surface p-4",
        emphasize ? "border-brand-4 shadow-sm" : "border-border"
      )}
    >
      <div>
        <h2 className="text-base font-extrabold tracking-tight">{title}</h2>
        <p className="mt-1 text-sm leading-relaxed text-muted">{body}</p>
      </div>
      <Button type="button" size="sm" variant={emphasize ? "default" : "outline"} disabled={disabled} onClick={onAction}>
        {actionLabel}
      </Button>
    </div>
  );
}
