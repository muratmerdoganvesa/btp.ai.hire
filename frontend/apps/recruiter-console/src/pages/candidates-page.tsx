import { Button, cn } from "@hirelens/ui";
import { ApiError } from "@hirelens/api-client";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useParams } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "../api";
import { AppShell } from "../components/app-shell";
import { CandidatesTable } from "../components/candidates-table";
import { CvUploadZone, uploadCandidateCv } from "../components/cv-upload-zone";

type SourceMode = "choose" | "sf" | "manual";

export function CandidatesPage() {
  const { t } = useTranslation();
  const { positionId } = useParams({ from: "/positions/$positionId" });
  const queryClient = useQueryClient();
  const [displayName, setDisplayName] = useState("");
  const [cvFile, setCvFile] = useState<File | null>(null);
  const [selectedCandidateId, setSelectedCandidateId] = useState<string | null>(null);
  const [filter, setFilter] = useState("");
  const [sort, setSort] = useState<"score" | "date" | "coverage">("score");
  const [mode, setMode] = useState<SourceMode>("choose");
  const [linkCopied, setLinkCopied] = useState(false);
  const [sfMessage, setSfMessage] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);
  const [formPhase, setFormPhase] = useState<string | null>(null);

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
    mutationFn: async () => {
      setFormError(null);
      setFormPhase(null);
      const candidate = await api.createCandidate(positionId, displayName.trim());
      if (cvFile) {
        setFormPhase(t("upload.phaseUpload"));
        try {
          await uploadCandidateCv(positionId, candidate.id, cvFile, (p) => {
            if (p === "upload") setFormPhase(t("upload.phaseUpload"));
            else if (p === "parse") setFormPhase(t("upload.phaseParse"));
            else if (p.startsWith("match")) setFormPhase(`${t("upload.phaseMatch")} (${p.split(":")[1] ?? ""})`);
          });
        } catch (err) {
          const message = err instanceof Error ? err.message : "";
          if (/scanned|could not be extracted|text could not/i.test(message)) {
            throw new Error(t("upload.scanned"));
          }
          if (err instanceof ApiError && message) {
            throw new Error(`${t("errors.generic")} (${message.replace(/^http_\d+:/, "")})`);
          }
          throw err instanceof Error ? err : new Error(t("errors.generic"));
        }
      }
      return candidate;
    },
    onSuccess: async (candidate) => {
      setDisplayName("");
      setCvFile(null);
      setFormPhase(null);
      setFormError(null);
      setSelectedCandidateId(candidate.id);
      setMode("manual");
      await queryClient.invalidateQueries({ queryKey: ["candidates", positionId] });
    },
    onError: (err) => {
      setFormPhase(null);
      setFormError(err instanceof Error ? err.message : t("errors.generic"));
    }
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
  const applyHref = applySlug ? `${window.location.origin}/apply/${applySlug}` : null;
  const selected = list.find((row) => row.id === selectedCandidateId) ?? null;
  const isEmpty = !candidates.isLoading && list.length === 0;
  const showChooser = isEmpty && mode === "choose";

  const copyApplyLink = async () => {
    if (!applyHref) {
      return;
    }
    await navigator.clipboard.writeText(applyHref);
    setLinkCopied(true);
    window.setTimeout(() => setLinkCopied(false), 1600);
  };

  return (
    <AppShell>
      <header className="flex shrink-0 flex-col gap-3 border-b border-border pb-3 sm:flex-row sm:items-center sm:justify-between">
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
          <p className="text-sm tabular-nums text-muted">
            {list.length} {t("candidates.count")}
          </p>
          {!isEmpty ? (
            <>
              <Button type="button" variant="outline" size="sm" onClick={() => setMode("sf")}>
                {t("candidates.sfTitle")}
              </Button>
              <Button type="button" size="sm" onClick={() => setMode("manual")}>
                {t("candidates.manualTitle")}
              </Button>
            </>
          ) : null}
        </div>
      </header>

      <p className="shrink-0 text-xs text-muted">{t("candidates.rankingDisclaimer")}</p>

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
            title={t("candidates.manualTitle")}
            body={t("candidates.manualBody")}
            actionLabel={t("candidates.manualAction")}
            onAction={() => setMode("manual")}
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

      {mode === "manual" ? (
        <section className="grid shrink-0 gap-3 rounded-xl border border-border bg-surface p-4 lg:grid-cols-[minmax(0,1fr)_minmax(16rem,22rem)]">
          <div className="flex flex-col gap-3">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div>
                <p className="text-sm font-extrabold">{t("candidates.manualTitle")}</p>
                <p className="text-sm text-muted">{t("candidates.manualBody")}</p>
              </div>
              {isEmpty ? (
                <Button type="button" variant="outline" size="sm" onClick={() => setMode("choose")}>
                  {t("candidates.backToSources")}
                </Button>
              ) : null}
            </div>
            <div className="flex flex-col gap-3">
              <label className="text-sm">
                <span className="mb-1 block font-semibold text-muted">{t("candidates.displayName")}</span>
                <input
                  value={displayName}
                  onChange={(event) => setDisplayName(event.target.value)}
                  className="h-9 w-full rounded-lg border border-border bg-white px-3 text-sm outline-none focus-visible:border-brand-5 focus-visible:ring-2 focus-visible:ring-brand-6/15"
                />
              </label>
              <label className="flex cursor-pointer flex-col items-center justify-center rounded-xl border border-dashed border-border bg-brand-1/40 px-4 py-5 text-center transition-colors hover:border-brand-4">
                <span className="text-sm font-medium">{cvFile ? cvFile.name : t("candidates.cvRequired")}</span>
                <span className="mt-1 text-xs text-muted">{t("upload.hint")}</span>
                <input
                  type="file"
                  className="sr-only"
                  accept=".pdf,.docx,.txt,application/pdf,application/vnd.openxmlformats-officedocument.wordprocessingml.document,text/plain"
                  onChange={(event) => setCvFile(event.target.files?.[0] ?? null)}
                />
              </label>
              {formPhase ? (
                <p className="text-sm text-muted" role="status">
                  {formPhase}
                </p>
              ) : null}
              {formError ? (
                <p className="text-sm text-danger" role="alert">
                  {formError}
                </p>
              ) : null}
              <Button
                type="button"
                size="sm"
                className="self-start"
                disabled={!displayName.trim() || !cvFile || create.isPending}
                onClick={() => create.mutate()}
              >
                {create.isPending ? t("candidates.creatingWithCv") : t("candidates.createWithCv")}
              </Button>
            </div>
          </div>
          <div>
            {selected ? (
              <div className="flex flex-col gap-2">
                <p className="text-xs font-semibold uppercase tracking-wide text-muted">
                  {t("candidates.uploadFor", { name: selected.displayName })}
                </p>
                <CvUploadZone
                  compact
                  positionId={positionId}
                  candidateId={selected.id}
                  onCompleted={() => void queryClient.invalidateQueries({ queryKey: ["candidates", positionId] })}
                />
              </div>
            ) : (
              <p className="rounded-lg border border-dashed border-border px-4 py-6 text-sm text-muted">
                {t("candidates.selectHintAfter")}
              </p>
            )}
          </div>
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
        <section className="flex min-h-0 flex-1 flex-col gap-2 overflow-hidden">
          <div className="flex shrink-0 flex-wrap items-center gap-2">
            <input
              value={filter}
              placeholder={t("candidates.filterPlaceholder")}
              onChange={(event) => setFilter(event.target.value)}
              className="h-9 min-w-[12rem] flex-1 rounded-lg border border-border bg-surface px-3 text-sm outline-none placeholder:text-muted focus-visible:border-brand-5 focus-visible:ring-2 focus-visible:ring-brand-6/15 sm:max-w-xs"
            />
            <select
              className="h-9 rounded-lg border border-border bg-surface px-3 text-sm outline-none focus-visible:border-brand-5 focus-visible:ring-2 focus-visible:ring-brand-6/15"
              value={sort}
              onChange={(event) => setSort(event.target.value as typeof sort)}
              aria-label={t("candidates.sort")}
            >
              <option value="score">{t("candidates.sortScore")}</option>
              <option value="date">{t("candidates.sortDate")}</option>
              <option value="coverage">{t("candidates.sortCoverage")}</option>
            </select>
          </div>
          <div className="min-h-0 flex-1 overflow-auto">
            <CandidatesTable
              rows={list}
              selectedId={selectedCandidateId}
              onSelect={(id) => {
                setSelectedCandidateId(id);
                setMode("manual");
              }}
            />
          </div>
        </section>
      ) : mode === "choose" ? (
        <p className="rounded-xl border border-dashed border-border px-4 py-10 text-center text-sm text-muted">
          {t("candidates.empty")}
        </p>
      ) : null}
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
        "flex flex-col gap-3 rounded-xl border bg-surface p-4",
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
