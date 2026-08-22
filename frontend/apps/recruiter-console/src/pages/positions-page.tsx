import { Button, Card, CardContent, CardHeader, CardTitle } from "@hirelens/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "../api";
import { AppShell } from "../components/app-shell";
import { Field, TextArea, TextInput } from "../components/field";

const emptyCriterion = { name: "", description: "", weight: 50 };

export function PositionsPage() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const positions = useQuery({ queryKey: ["positions"], queryFn: () => api.listPositions() });
  const [title, setTitle] = useState("");
  const [jobDescription, setJobDescription] = useState("");
  const [criteria, setCriteria] = useState([
    { ...emptyCriterion, name: "C#", weight: 60 },
    { ...emptyCriterion, name: "SQL", weight: 40 }
  ]);
  const [error, setError] = useState<string | null>(null);

  const weightSum = criteria.reduce((sum, criterion) => sum + Number(criterion.weight || 0), 0);

  const create = useMutation({
    mutationFn: () =>
      api.createPosition({
        title,
        jobDescription,
        criteria
      }),
    onSuccess: async () => {
      setTitle("");
      setJobDescription("");
      setError(null);
      await queryClient.invalidateQueries({ queryKey: ["positions"] });
    },
    onError: () => setError(t("errors.weights"))
  });

  return (
    <AppShell>
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">{t("positions.title")}</h1>
        <p className="mt-1 text-sm text-muted">{t("positions.weightsHint")}</p>
      </div>
      <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]">
        <Card>
          <CardHeader>
            <CardTitle>{t("positions.create")}</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            <Field label={t("positions.name")}>
              <TextInput value={title} onChange={(event) => setTitle(event.target.value)} />
            </Field>
            <Field label={t("positions.jd")}>
              <TextArea value={jobDescription} onChange={(event) => setJobDescription(event.target.value)} />
            </Field>
            <div className="flex items-center justify-between text-sm">
              <span className="text-muted">{t("positions.criteria")}</span>
              <span className={weightSum === 100 ? "text-foreground" : "text-danger"}>
                {t("positions.weightSum")}: {weightSum}
              </span>
            </div>
            <div className="flex flex-col gap-3">
              {criteria.map((criterion, index) => (
                <div key={index} className="grid gap-2 rounded-md border border-border bg-background p-3 sm:grid-cols-[1fr_1fr_5rem]">
                  <TextInput
                    placeholder={t("positions.criterionName")}
                    value={criterion.name}
                    onChange={(event) => {
                      const next = [...criteria];
                      next[index] = { ...criterion, name: event.target.value };
                      setCriteria(next);
                    }}
                  />
                  <TextInput
                    placeholder={t("positions.criterionDesc")}
                    value={criterion.description}
                    onChange={(event) => {
                      const next = [...criteria];
                      next[index] = { ...criterion, description: event.target.value };
                      setCriteria(next);
                    }}
                  />
                  <TextInput
                    type="number"
                    aria-label={t("positions.weight")}
                    value={criterion.weight}
                    onChange={(event) => {
                      const next = [...criteria];
                      next[index] = { ...criterion, weight: Number(event.target.value) };
                      setCriteria(next);
                    }}
                  />
                </div>
              ))}
            </div>
            <Button type="button" variant="outline" onClick={() => setCriteria([...criteria, { ...emptyCriterion, weight: 0 }])}>
              {t("positions.addCriterion")}
            </Button>
            {error ? (
              <p className="text-sm text-danger" role="alert">
                {error}
              </p>
            ) : null}
            <Button type="button" onClick={() => create.mutate()} disabled={create.isPending || !title || !jobDescription}>
              {t("positions.submit")}
            </Button>
          </CardContent>
        </Card>
        <section className="flex flex-col gap-3">
          <h2 className="text-sm font-medium text-muted">{t("positions.list")}</h2>
          {(positions.data ?? []).length === 0 ? (
            <div className="rounded-lg border border-dashed border-border bg-brand-1/40 px-6 py-12 text-center text-sm text-muted">
              {t("positions.empty")}
            </div>
          ) : (
            (positions.data ?? []).map((position) => (
              <Card key={position.id} className="transition-colors hover:border-brand-4">
                <CardContent className="flex items-center justify-between gap-4 pt-4">
                  <div>
                    <p className="font-medium">{position.title}</p>
                    <p className="mt-1 line-clamp-2 text-sm text-muted">{position.jobDescription}</p>
                    <p className="mt-2 text-xs text-muted">
                      {position.criteria.map((criterion) => `${criterion.name} ${criterion.weight}`).join(" · ")}
                    </p>
                  </div>
                  <Button asChild variant="outline" size="sm">
                    <Link to="/positions/$positionId" params={{ positionId: position.id }}>
                      {t("positions.open")}
                    </Link>
                  </Button>
                </CardContent>
              </Card>
            ))
          )}
        </section>
      </div>
    </AppShell>
  );
}
