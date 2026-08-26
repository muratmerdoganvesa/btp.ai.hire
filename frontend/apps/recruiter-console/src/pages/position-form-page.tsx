import { Button } from "@hirelens/ui";
import { ApiError } from "@hirelens/api-client";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate, useParams } from "@tanstack/react-router";
import { useEffect, useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "../api";
import { AppShell } from "../components/app-shell";
import { Field, TextArea, TextInput } from "../components/field";

type CriterionRow = { name: string; description: string; weight: number };

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
  const { positionId } = useParams({ from: "/positions/$positionId/edit" });
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
  }, [existing.data]);

  const weightSum = useMemo(() => criteria.reduce((sum, row) => sum + (Number(row.weight) || 0), 0), [criteria]);

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

  const save = useMutation({
    mutationFn: () => {
      const payload = {
        title: title.trim(),
        jobDescription: jobDescription.trim(),
        criteria: criteria.map((row) => ({
          name: row.name.trim(),
          description: row.description.trim() || row.name.trim(),
          weight: Number(row.weight)
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
      <AppShell>
        <p className="text-sm text-muted">{t("positions.loading")}</p>
      </AppShell>
    );
  }

  return (
    <AppShell>
      <header className="flex shrink-0 flex-wrap items-end justify-between gap-3">
        <div>
          <p className="text-xs font-bold uppercase tracking-[0.12em] text-muted">{t("positions.title")}</p>
          <h1 className="text-xl font-extrabold tracking-tight sm:text-2xl">
            {isEditing ? t("positions.edit") : t("positions.create")}
          </h1>
        </div>
        <Button asChild variant="outline" size="sm">
          <Link to="/positions">{t("positions.backToList")}</Link>
        </Button>
      </header>

      <div
        data-tour="tour-composer"
        className="flex min-h-0 flex-1 flex-col overflow-auto rounded-xl border border-border bg-surface"
      >
        <div className="flex flex-col gap-4 p-4 sm:p-5">
          <p className="text-sm text-muted">{t("positions.formHint")}</p>

          <div className="grid gap-4 lg:grid-cols-2">
            <Field label={t("positions.name")}>
              <TextInput
                value={title}
                onChange={(event) => {
                  setError(null);
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
                <Button type="button" variant="outline" size="sm" onClick={balanceWeights}>
                  {t("positions.balanceWeights")}
                </Button>
                <Button type="button" variant="outline" size="sm" onClick={addRow}>
                  {t("positions.addCriterion")}
                </Button>
              </div>
            </div>

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
            <p className="text-xs text-muted">{t("positions.weightsHint")}</p>
          </section>

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
    </AppShell>
  );
}
