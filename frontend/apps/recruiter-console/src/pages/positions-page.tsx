import { Button, Card, CardContent, CardHeader, CardTitle, Chip } from "@hirelens/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "../api";
import { AppShell } from "../components/app-shell";
import { Field, TextArea, TextInput } from "../components/field";

const suggestedSkills = [
  "C#",
  "SQL",
  "SAP BTP",
  "React",
  "Figma",
  "User Research",
  "HANA",
  "Python",
  "KVKK",
  "Typography"
];

const levels = ["Stajyer", "Junior", "Mid", "Senior", "Lead"] as const;

function splitWeights(count: number): number[] {
  if (count === 0) {
    return [];
  }

  const base = Math.floor(100 / count);
  const remainder = 100 - base * count;
  return Array.from({ length: count }, (_, index) => base + (index < remainder ? 1 : 0));
}

export function PositionsPage() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const positions = useQuery({ queryKey: ["positions"], queryFn: () => api.listPositions() });
  const [title, setTitle] = useState("");
  const [jobDescription, setJobDescription] = useState("");
  const [level, setLevel] = useState<(typeof levels)[number]>("Senior");
  const [selected, setSelected] = useState<string[]>(["C#", "SQL", "SAP BTP"]);
  const [customSkill, setCustomSkill] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [dragIndex, setDragIndex] = useState<number | null>(null);

  const weights = useMemo(() => splitWeights(selected.length), [selected.length]);
  const allSkills = useMemo(() => {
    const extras = selected.filter((skill) => !suggestedSkills.includes(skill));
    return [...suggestedSkills, ...extras];
  }, [selected]);

  const toggleSkill = (skill: string) => {
    setSelected((current) =>
      current.includes(skill) ? current.filter((item) => item !== skill) : [...current, skill]
    );
  };

  const addCustom = () => {
    const name = customSkill.trim();
    if (!name) {
      return;
    }

    setSelected((current) => (current.includes(name) ? current : [...current, name]));
    setCustomSkill("");
  };

  const move = (from: number, to: number) => {
    if (to < 0 || to >= selected.length) {
      return;
    }

    setSelected((current) => {
      const next = [...current];
      const [item] = next.splice(from, 1);
      next.splice(to, 0, item);
      return next;
    });
  };

  const create = useMutation({
    mutationFn: () =>
      api.createPosition({
        title: `${level} ${title}`.trim(),
        jobDescription,
        criteria: selected.map((name, index) => ({
          name,
          description: name,
          weight: weights[index] ?? 0
        }))
      }),
    onSuccess: async () => {
      setTitle("");
      setJobDescription("");
      setError(null);
      await queryClient.invalidateQueries({ queryKey: ["positions"] });
    },
    onError: () => setError(t("errors.weights"))
  });

  const ready = title.trim().length > 0 && jobDescription.trim().length > 0 && selected.length >= 3;

  return (
    <AppShell>
      <div>
        <h1 className="text-3xl font-semibold tracking-tight">{t("positions.title")}</h1>
        <p className="mt-2 text-sm text-muted">{t("positions.composerHint")}</p>
      </div>
      <div className="grid gap-6 xl:grid-cols-[minmax(0,16rem)_minmax(0,1fr)_minmax(0,20rem)]">
        <aside className="hidden rounded-2xl bg-brand-1 px-5 py-8 text-sm leading-6 text-foreground xl:block">
          {t("positions.composerPrompt", { title: title.trim() || t("positions.untitled") })}
        </aside>

        <Card>
          <CardHeader>
            <CardTitle>{t("positions.create")}</CardTitle>
            <p className="text-sm text-muted">{t("positions.multiSelect")}</p>
          </CardHeader>
          <CardContent className="flex flex-col gap-6">
            <Field label={t("positions.name")}>
              <TextInput value={title} onChange={(event) => setTitle(event.target.value)} />
            </Field>
            <Field label={t("positions.jd")}>
              <TextArea value={jobDescription} onChange={(event) => setJobDescription(event.target.value)} />
            </Field>

            <section className="flex flex-col gap-3">
              <h3 className="text-sm font-medium">{t("positions.level")}</h3>
              <div className="flex flex-wrap gap-2">
                {levels.map((item) => (
                  <Chip key={item} selected={level === item} onClick={() => setLevel(item)}>
                    {item}
                  </Chip>
                ))}
              </div>
            </section>

            <section className="flex flex-col gap-3">
              <div className="flex items-center justify-between gap-3">
                <h3 className="text-sm font-medium">{t("positions.skills")}</h3>
                <p className="text-xs text-muted">
                  {selected.length >= 3 ? t("positions.skillsOk") : t("positions.skillsMin")}
                </p>
              </div>
              <div className="flex flex-wrap gap-2">
                {allSkills.map((skill) => (
                  <Chip key={skill} selected={selected.includes(skill)} onClick={() => toggleSkill(skill)}>
                    {skill}
                  </Chip>
                ))}
              </div>
            </section>

            <section className="flex flex-col gap-2">
              <h3 className="text-sm font-medium">{t("positions.selected")}</h3>
              {selected.map((skill, index) => (
                <div
                  key={skill}
                  draggable
                  onDragStart={() => setDragIndex(index)}
                  onDragOver={(event) => event.preventDefault()}
                  onDrop={() => {
                    if (dragIndex !== null) {
                      move(dragIndex, index);
                      setDragIndex(null);
                    }
                  }}
                  className="flex items-center gap-3 rounded-xl px-2 py-2 hover:bg-brand-1"
                >
                  <span aria-hidden="true" className="cursor-grab text-muted">
                    ∷
                  </span>
                  <span
                    className="inline-flex size-5 items-center justify-center rounded-sm bg-brand text-[10px] text-brand-fg"
                    aria-hidden="true"
                  >
                    ✓
                  </span>
                  <span className="flex-1 text-sm">{skill}</span>
                  <span className="text-xs text-muted">{weights[index]}</span>
                  <button
                    type="button"
                    className="text-xs text-muted hover:text-foreground"
                    onClick={() => toggleSkill(skill)}
                  >
                    {t("positions.remove")}
                  </button>
                </div>
              ))}
              <TextInput
                placeholder={t("positions.addPlaceholder")}
                value={customSkill}
                onChange={(event) => setCustomSkill(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === "Enter") {
                    event.preventDefault();
                    addCustom();
                  }
                }}
              />
            </section>

            {error ? (
              <p className="text-sm text-danger" role="alert">
                {error}
              </p>
            ) : null}
            <Button type="button" size="lg" className="w-full" onClick={() => create.mutate()} disabled={create.isPending || !ready}>
              {t("positions.submit")}
            </Button>
          </CardContent>
        </Card>

        <section className="flex flex-col gap-3">
          <h2 className="text-sm font-medium text-muted">{t("positions.list")}</h2>
          {(positions.data ?? []).length === 0 ? (
            <div className="rounded-2xl border border-dashed border-border bg-surface px-6 py-12 text-center text-sm text-muted shadow-card">
              {t("positions.empty")}
            </div>
          ) : (
            (positions.data ?? []).map((position) => (
              <Card key={position.id} className="transition-colors hover:border-brand-4">
                <CardContent className="flex flex-col gap-3 pt-6">
                  <p className="font-medium">{position.title}</p>
                  <p className="line-clamp-2 text-sm text-muted">{position.jobDescription}</p>
                  <div className="flex flex-wrap gap-2">
                    {position.criteria.map((criterion) => (
                      <span
                        key={criterion.id}
                        className="rounded-pill bg-brand-1 px-3 py-1 text-xs text-foreground"
                      >
                        {criterion.name}
                      </span>
                    ))}
                  </div>
                  <Button asChild variant="outline" size="sm" className="mt-1 self-start">
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
