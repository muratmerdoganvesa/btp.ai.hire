import { Button } from "@hirelens/ui";
import { ApiError } from "@hirelens/api-client";
import type { ExtractedInterviewQuestion, FlaggedPhrase, UnmeasurablePhrase } from "@hirelens/api-client";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate, useParams } from "@tanstack/react-router";
import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "../api";
import { Field, TextArea, TextInput } from "../components/field";
import { PageBody, PageHero } from "../components/page-hero";

type CriterionRow = { name: string; description: string; weight: number; mandatory?: boolean };

function splitWeights(count: number): number[] {
  if (count === 0) {
    return [];
  }
  const base = Math.floor(100 / count);
  const remainder = 100 - base * count;
  return Array.from({ length: count }, (_, index) => base + (index < remainder ? 1 : 0));
}

function defaultCriteria(): CriterionRow[] {
  const weights = splitWeights(3);
  return weights.map((weight) => ({ name: "", description: "", weight }));
}

export function PositionCreatePage() {
  return <PositionFormPage mode="create" />;
}

export function PositionEditPage() {
  const { positionId } = useParams({ from: "/_app/positions/$positionId/edit" });
  return <PositionFormPage mode="edit" positionId={positionId} />;
}

function PositionFormPage({ mode, positionId }: { mode: "create" | "edit"; positionId?: string }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const isEditing = mode === "edit";

  const existing = useQuery({
    queryKey: ["positions", positionId],
    queryFn: () => api.getPosition(positionId!),
    enabled: isEditing && Boolean(positionId)
  });

  const [title, setTitle] = useState("");
  const [jobDescription, setJobDescription] = useState("");
  const [criteria, setCriteria] = useState<CriterionRow[]>(defaultCriteria);
  const [error, setError] = useState<string | null>(null);
  const [extractError, setExtractError] = useState<string | null>(null);
  const [flaggedPhrases, setFlaggedPhrases] = useState<FlaggedPhrase[]>([]);
  const [unmeasurable, setUnmeasurable] = useState<UnmeasurablePhrase[]>([]);
  const [interviewQuestions, setInterviewQuestions] = useState<ExtractedInterviewQuestion[]>([]);
  const [extractWarnings, setExtractWarnings] = useState<string[]>([]);
  const [flaggedDismissed, setFlaggedDismissed] = useState(false);

  useEffect(() => {
    if (!existing.data) {
      return;
    }
    setTitle(existing.data.title);
    setJobDescription(existing.data.jobDescription);
    setCriteria(
      existing.data.criteria.map((criterion) => ({
        name: criterion.name,
        description: criterion.description,
        weight: criterion.weight
      }))
    );
    setInterviewQuestions(
      (existing.data.interviewQuestions ?? []).map((item) => ({
        questionId: item.questionId ?? "",
        criterionId: item.criterionId ?? "",
        question: item.question,
        whatToListenFor: item.whatToListenFor ?? []
      }))
    );
    setUnmeasurable(existing.data.unmeasurable ?? []);
    setFlaggedPhrases(existing.data.flaggedPhrases ?? []);
  }, [existing.data]);

  const weightSum = useMemo(() => criteria.reduce((sum, row) => sum + (Number(row.weight) || 0), 0), [criteria]);

  const canExtract = title.trim().length > 0 && jobDescription.trim().length >= 100;

  const blockers = useMemo(() => {
    const items: string[] = [];
    if (!title.trim()) {
      items.push(t("positions.needTitle"));
    }
    if (!jobDescription.trim()) {
      items.push(t("positions.needJd"));
    }
    if (criteria.length === 0) {
      items.push(t("positions.needCriteria"));
    }
    if (criteria.some((row) => !row.name.trim())) {
      items.push(t("positions.needCriterionNames"));
    }
    if (weightSum !== 100) {
      items.push(t("positions.weightsHint"));
    }
    return items;
  }, [title, jobDescription, criteria, weightSum, t]);

  const ready = blockers.length === 0;

  const updateRow = (index: number, patch: Partial<CriterionRow>) => {
    setError(null);
    setCriteria((rows) => rows.map((row, i) => (i === index ? { ...row, ...patch } : row)));
  };

  const addRow = () => {
    setError(null);
    setCriteria((rows) => {
      const weights = splitWeights(rows.length + 1);
      return [...rows, { name: "", description: "", weight: weights[weights.length - 1] ?? 0 }].map((row, index) => ({
        ...row,
        weight: weights[index] ?? row.weight
      }));
    });
  };

  const removeRow = (index: number) => {
    setError(null);
    setCriteria((rows) => {
      const next = rows.filter((_, i) => i !== index);
      const weights = splitWeights(next.length);
      return next.map((row, i) => ({ ...row, weight: weights[i] ?? 0 }));
    });
  };

  const balanceWeights = () => {
    setError(null);
    const weights = splitWeights(criteria.length);
    setCriteria((rows) => rows.map((row, index) => ({ ...row, weight: weights[index] ?? 0 })));
  };

  const extract = useMutation({
    mutationFn: () =>
      api.extractCriteria({
        jobTitle: title.trim(),
        jobDescription: jobDescription.trim()
      }),
    onSuccess: (response) => {
      setExtractError(null);
      setCriteria(
        response.criteria.map((item) => ({
          name: item.label,
          description: item.description,
          weight: item.weight,
          mandatory: item.mandatory
        }))
      );
      setFlaggedPhrases(response.flaggedPhrases ?? []);
      setUnmeasurable(response.unmeasurable ?? []);
      setInterviewQuestions(response.interviewQuestions ?? []);
      setExtractWarnings(response.warnings ?? []);
      setFlaggedDismissed(false);
      if (response.criteria.length === 0) {
        setExtractError(t("positions.extractEmpty"));
      }
    },
    onError: (err) => {
      if (err instanceof ApiError) {
        if (err.status === 401 || err.status === 403) {
          setExtractError(t("positions.extractAuthFailed"));
          return;
        }
        if (/AI Core kimliği|Orchestration returned 401|Authentication is required/i.test(err.message)) {
          setExtractError(t("positions.extractAiAuthFailed"));
          return;
        }
        setExtractError(err.message || t("positions.extractFailed"));
        return;
      }
      setExtractError(t("positions.extractFailed"));
    }
  });

  const save = useMutation({
    mutationFn: () => {
      const payload = {
        title: title.trim(),
        jobDescription: jobDescription.trim(),
        criteria: criteria.map((row) => ({
          name: row.name.trim(),
          description: row.description.trim() || row.name.trim(),
          weight: Number(row.weight)
        })),
        interviewQuestions: interviewQuestions
          .filter((item) => item.question.trim().length > 0)
          .map((item) => ({
            questionId: item.questionId ?? "",
            criterionId: item.criterionId ?? "",
            question: item.question.trim(),
            whatToListenFor: item.whatToListenFor ?? []
          })),
        unmeasurable: unmeasurable
          .filter((item) => item.phrase.trim().length > 0)
          .map((item) => ({ phrase: item.phrase.trim(), reason: item.reason ?? "" })),
        flaggedPhrases: flaggedPhrases
          .filter((item) => item.phrase.trim().length > 0)
          .map((item) => ({
            phrase: item.phrase.trim(),
            category: item.category ?? "",
            reason: item.reason ?? ""
          }))
      };
      return isEditing && positionId ? api.updatePosition(positionId, payload) : api.createPosition(payload);
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["positions"] });
      await navigate({ to: "/positions" });
    },
    onError: (err) => {
      if (err instanceof ApiError) {
        if (err.message.includes("weights") || err.message.includes("sum to 100")) {
          setError(t("errors.weights"));
          return;
        }
        setError(`${t("positions.saveFailed")} (${err.message})`);
        return;
      }
      setError(t("positions.saveFailed"));
    }
  });

  const trySave = () => {
    if (!ready) {
      setError(`${t("positions.saveBlocked")} ${blockers.join(" · ")}`);
      return;
    }
    setError(null);
    save.mutate();
  };

  if (isEditing && existing.isLoading) {
    return (
      <>
        <PageHero kicker={t("positions.title")} title={t("positions.edit")} />
        <PageBody>
          <p className="text-sm text-muted">{t("positions.loading")}</p>
        </PageBody>
      </>
    );
  }

  const showFlagged = !flaggedDismissed && flaggedPhrases.length > 0;

  return (
    <>
      <PageHero
        kicker={t("positions.title")}
        title={isEditing ? t("positions.edit") : t("positions.create")}
        actions={
          <Button asChild variant="outline" size="sm" className="!border-white/40 !bg-white/10 !text-white hover:!bg-white/20 hover:!text-white">
            <Link to="/positions">{t("positions.backToList")}</Link>
          </Button>
        }
      />
      <PageBody>
      <p className="text-sm text-muted">{t("positions.formHint")}</p>
      <div
        data-tour="form-composer"
        className="flex min-h-0 flex-1 flex-col overflow-auto rounded-xl border border-border bg-surface"
      >
        <div className="flex flex-col gap-4 p-4 sm:p-5">
          <div className="grid gap-4 lg:grid-cols-2">
            <Field label={t("positions.name")}>
              <TextInput
                value={title}
                onChange={(event) => {
                  setError(null);
                  setExtractError(null);
                  setTitle(event.target.value);
                }}
                placeholder={t("positions.namePlaceholder")}
              />
            </Field>
            <div className="lg:col-span-2">
              <Field label={t("positions.jd")}>
                <TextArea
                  className="min-h-28"
                  value={jobDescription}
                  onChange={(event) => {
                    setError(null);
                    setExtractError(null);
                    setJobDescription(event.target.value);
                  }}
                  placeholder={t("positions.jdPlaceholder")}
                />
              </Field>
            </div>
          </div>

          <section className="flex flex-col gap-3">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <h3 className="text-sm font-bold">{t("positions.criteria")}</h3>
              <div className="flex flex-wrap items-center gap-2">
                <span className={`text-sm font-semibold ${weightSum === 100 ? "text-success-fg" : "text-danger"}`}>
                  {t("positions.weightSum")}: {weightSum}/100
                </span>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  disabled={!canExtract || extract.isPending}
                  onClick={() => extract.mutate()}
                >
                  {extract.isPending ? t("positions.extracting") : t("positions.extractCriteria")}
                </Button>
                <Button type="button" variant="outline" size="sm" onClick={balanceWeights}>
                  {t("positions.balanceWeights")}
                </Button>
                <Button type="button" variant="outline" size="sm" onClick={addRow}>
                  {t("positions.addCriterion")}
                </Button>
              </div>
            </div>

            {showFlagged ? (
              <div className="rounded-xl border border-amber-300 bg-amber-50 px-4 py-3 text-sm text-amber-950">
                <p className="font-semibold">
                  {t("positions.flaggedIntro", { count: flaggedPhrases.length })}
                </p>
                <ul className="mt-2 list-disc space-y-1 pl-5">
                  {flaggedPhrases.map((item, index) => (
                    <li key={`${item.phrase}-${index}`}>
                      <span className="font-medium">{item.phrase}</span>
                      {item.reason ? ` — ${item.reason}` : null}
                    </li>
                  ))}
                </ul>
                <p className="mt-2">{t("positions.flaggedFooter")}</p>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  className="mt-3"
                  onClick={() => setFlaggedDismissed(true)}
                >
                  {t("positions.flaggedDismiss")}
                </Button>
              </div>
            ) : null}

            <div className="overflow-x-auto rounded-xl border border-border">
              <table className="w-full min-w-[36rem] text-left text-sm">
                <thead className="border-b border-border bg-brand-0/50 text-[0.7rem] uppercase tracking-wide text-muted">
                  <tr>
                    <th className="px-3 py-2 font-bold">{t("positions.criterionName")}</th>
                    <th className="px-3 py-2 font-bold">{t("positions.criterionDesc")}</th>
                    <th className="w-24 px-3 py-2 font-bold">{t("positions.weight")}</th>
                    <th className="w-16 px-3 py-2" />
                  </tr>
                </thead>
                <tbody>
                  {criteria.map((row, index) => (
                    <tr key={index} className="border-b border-border last:border-0">
                      <td className="px-3 py-1.5">
                        <TextInput
                          className="min-w-[8rem] rounded-lg px-3 py-2"
                          value={row.name}
                          onChange={(event) => updateRow(index, { name: event.target.value })}
                        />
                      </td>
                      <td className="px-3 py-1.5">
                        <TextInput
                          className="min-w-[10rem] rounded-lg px-3 py-2"
                          value={row.description}
                          onChange={(event) => updateRow(index, { description: event.target.value })}
                        />
                      </td>
                      <td className="px-3 py-1.5">
                        <TextInput
                          className="rounded-lg px-3 py-2"
                          type="number"
                          min={1}
                          max={100}
                          value={row.weight}
                          onChange={(event) => updateRow(index, { weight: Number(event.target.value) || 0 })}
                        />
                      </td>
                      <td className="px-3 py-1.5 text-right">
                        <button
                          type="button"
                          className="text-xs font-semibold text-muted hover:text-danger disabled:opacity-40"
                          disabled={criteria.length <= 1}
                          onClick={() => removeRow(index)}
                        >
                          {t("positions.remove")}
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            {unmeasurable.length > 0 ? (
              <div className="rounded-xl border border-border bg-muted/30 px-4 py-3 text-xs text-muted">
                <p className="font-semibold text-foreground">{t("positions.unmeasurableIntro")}</p>
                <ul className="mt-1 list-disc space-y-0.5 pl-5">
                  {unmeasurable.map((item, index) => (
                    <li key={`${item.phrase}-${index}`}>
                      <span className="font-medium">{item.phrase}</span>
                      {item.reason ? ` — ${item.reason}` : null}
                    </li>
                  ))}
                </ul>
              </div>
            ) : null}

            {extractWarnings.length > 0 ? (
              <div className="rounded-xl border border-border bg-muted/30 px-4 py-3 text-xs text-muted">
                <p className="font-semibold text-foreground">{t("positions.extractWarningsIntro")}</p>
                <ul className="mt-1 list-disc space-y-0.5 pl-5">
                  {extractWarnings.map((warning, index) => (
                    <li key={`${warning}-${index}`}>{warning}</li>
                  ))}
                </ul>
              </div>
            ) : null}

            <p className="text-xs text-muted">{t("positions.weightsHint")}</p>
          </section>

          {interviewQuestions.length > 0 ? (
            <section className="flex flex-col gap-3">
              <div>
                <h3 className="text-sm font-bold">{t("positions.interviewQuestions")}</h3>
                <p className="mt-1 text-xs text-muted">{t("positions.interviewQuestionsHint")}</p>
              </div>
              <ol className="space-y-3">
                {interviewQuestions.map((item, index) => (
                  <li
                    key={item.questionId || `${item.criterionId}-${index}`}
                    className="rounded-xl border border-border bg-brand-0/30 px-4 py-3"
                  >
                    <p className="text-sm font-semibold text-foreground">
                      {index + 1}. {item.question}
                    </p>
                    {item.whatToListenFor.length > 0 ? (
                      <ul className="mt-2 list-disc space-y-0.5 pl-5 text-xs text-muted">
                        {item.whatToListenFor.map((hint, hintIndex) => (
                          <li key={`${item.questionId}-hint-${hintIndex}`}>{hint}</li>
                        ))}
                      </ul>
                    ) : null}
                  </li>
                ))}
              </ol>
            </section>
          ) : null}

          {extractError ? <p className="text-sm font-medium text-danger">{extractError}</p> : null}
          {error ? <p className="text-sm font-medium text-danger">{error}</p> : null}

          <div className="flex flex-wrap gap-2 border-t border-border pt-4">
            <Button type="button" size="sm" onClick={trySave} disabled={save.isPending}>
              {save.isPending ? t("positions.saving") : isEditing ? t("positions.update") : t("positions.submit")}
            </Button>
            <Button asChild variant="outline" size="sm" disabled={save.isPending}>
              <Link to="/positions">{t("positions.cancelEdit")}</Link>
            </Button>
          </div>
        </div>
      </div>
      </PageBody>
    </>
  );
}
