import { Button, Card, CardContent, CardHeader, CardTitle, Chip } from "@hirelens/ui";
import { ApiError, type Position } from "@hirelens/api-client";
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
type Level = (typeof levels)[number];

function splitWeights(count: number): number[] {
  if (count === 0) {
    return [];
  }
  const base = Math.floor(100 / count);
  const remainder = 100 - base * count;
  return Array.from({ length: count }, (_, index) => base + (index < remainder ? 1 : 0));
}

function parseStoredTitle(full: string): { level: Level; title: string } {
  for (const item of levels) {
    if (full === item) {
      return { level: item, title: "" };
    }
    if (full.startsWith(`${item} `)) {
      return { level: item, title: full.slice(item.length).trim() };
    }
  }
  return { level: "Senior", title: full };
}

const emptyForm = {
  title: "",
  jobDescription: "",
  level: "Senior" as Level,
  selected: ["C#", "SQL", "SAP BTP"] as string[]
};

export function PositionsPage() {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const positions = useQuery({ queryKey: ["positions"], queryFn: () => api.listPositions() });
  const [editingId, setEditingId] = useState<string | null>(null);
  const [title, setTitle] = useState(emptyForm.title);
  const [jobDescription, setJobDescription] = useState(emptyForm.jobDescription);
  const [level, setLevel] = useState<Level>(emptyForm.level);
  const [selected, setSelected] = useState<string[]>(emptyForm.selected);
  const [customSkill, setCustomSkill] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [dragIndex, setDragIndex] = useState<number | null>(null);

  const weights = useMemo(() => splitWeights(selected.length), [selected.length]);
  const allSkills = useMemo(() => {
    const extras = selected.filter((skill) => !suggestedSkills.includes(skill));
    return [...suggestedSkills, ...extras];
  }, [selected]);

  const blockers = useMemo(() => {
    const items: string[] = [];
    if (!title.trim()) {
      items.push(t("positions.needTitle"));
    }
    if (!jobDescription.trim()) {
      items.push(t("positions.needJd"));
    }
    if (selected.length < 3) {
      items.push(t("positions.needSkills"));
    }
    return items;
  }, [title, jobDescription, selected.length, t]);

  const ready = blockers.length === 0;
  const isEditing = editingId !== null;

  const resetForm = () => {
    setEditingId(null);
    setTitle(emptyForm.title);
    setJobDescription(emptyForm.jobDescription);
    setLevel(emptyForm.level);
    setSelected([...emptyForm.selected]);
    setCustomSkill("");
    setError(null);
  };

  const loadForEdit = (position: Position) => {
    const parsed = parseStoredTitle(position.title);
    setEditingId(position.id);
    setTitle(parsed.title);
    setLevel(parsed.level);
    setJobDescription(position.jobDescription);
    setSelected(position.criteria.map((criterion) => criterion.name));
    setCustomSkill("");
    setError(null);
    window.scrollTo({ top: 0, behavior: "smooth" });
  };

  const toggleSkill = (skill: string) => {
    setError(null);
    setSelected((current) =>
      current.includes(skill) ? current.filter((item) => item !== skill) : [...current, skill]
    );
  };

  const addCustom = () => {
    const name = customSkill.trim();
    if (!name) {
      return;
    }
    setError(null);
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

  const payload = () => ({
    title: `${level} ${title}`.trim(),
    jobDescription,
    criteria: selected.map((name, index) => ({
      name,
      description: name,
      weight: weights[index] ?? 0
    }))
  });

  const save = useMutation({
    mutationFn: () =>
      isEditing && editingId
        ? api.updatePosition(editingId, payload())
        : api.createPosition(payload()),
    onSuccess: async () => {
      resetForm();
      await queryClient.invalidateQueries({ queryKey: ["positions"] });
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

  return (
    <AppShell>
      <div>
        <h1 className="text-3xl font-extrabold tracking-tight">{t("positions.title")}</h1>
        <p className="mt-1 text-sm text-muted">{t("positions.composerHint")}</p>
      </div>

      <div className="hl-rise-delay grid gap-5 lg:grid-cols-[minmax(0,1fr)_20rem] xl:grid-cols-[minmax(0,1fr)_22rem]">
        <Card data-tour="tour-composer">
          <CardHeader>
            <CardTitle>{isEditing ? t("positions.edit") : t("positions.create")}</CardTitle>
            <p className="text-sm text-muted">{t("positions.multiSelect")}</p>
          </CardHeader>
          <CardContent className="flex flex-col gap-6">
            <Field label={t("positions.name")}>
              <TextInput
                value={title}
                onChange={(event) => {
                  setError(null);
                  setTitle(event.target.value);
                }}
              />
            </Field>
            <Field label={t("positions.jd")}>
              <TextArea
                value={jobDescription}
                onChange={(event) => {
                  setError(null);
                  setJobDescription(event.target.value);
                }}
              />
            </Field>

            <section className="flex flex-col gap-3">
              <h3 className="text-sm font-bold">{t("positions.level")}</h3>
              <div className="flex flex-wrap gap-2">
                {levels.map((item) => (
                  <Chip key={item} selected={level === item} onClick={() => setLevel(item)}>
                    {item}
                  </Chip>
                ))}
              </div>
            </section>

            <section className="flex flex-col gap-3">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <h3 className="text-sm font-bold">{t("positions.skills")}</h3>
                <p className={`text-sm font-semibold ${selected.length >= 3 ? "text-success-fg" : "text-danger"}`}>
                  {selected.length >= 3 ? `✓ ${t("positions.skillsOk")}` : t("positions.skillsMin")}
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
              <h3 className="text-sm font-bold">{t("positions.selected")}</h3>
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
                  className="flex items-center gap-3 rounded-xl px-1 py-2 hover:bg-brand-0"
                >
                  <span aria-hidden="true" className="cursor-grab text-muted">
                    ⋮⋮
                  </span>
                  <span className="inline-flex size-5 items-center justify-center rounded-md bg-brand-6 text-[0.65rem] font-bold text-white">
                    ✓
                  </span>
                  <span className="flex-1 text-sm font-semibold">{skill}</span>
                  <span className="text-xs font-bold text-muted">{weights[index]}%</span>
                  <button
                    type="button"
                    className="text-xs font-semibold text-muted hover:text-danger"
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

            {error ? <p className="text-sm font-medium text-danger">{error}</p> : null}
            <Button type="button" size="lg" className="w-full" onClick={trySave} disabled={save.isPending}>
              {save.isPending
                ? t("positions.saving")
                : isEditing
                  ? t("positions.update")
                  : t("positions.submit")}
            </Button>
            {isEditing ? (
              <Button type="button" variant="outline" onClick={resetForm} disabled={save.isPending}>
                {t("positions.cancelEdit")}
              </Button>
            ) : null}
          </CardContent>
        </Card>

        <section data-tour="tour-position-list" className="flex flex-col gap-3">
          <h2 className="text-xs font-bold uppercase tracking-[0.12em] text-muted">{t("positions.list")}</h2>
          {(positions.data ?? []).length === 0 ? (
            <div className="rounded-2xl border border-dashed border-border bg-surface px-4 py-10 text-center text-sm text-muted">
              {t("positions.empty")}
            </div>
          ) : (
            (positions.data ?? []).map((position) => (
              <Card key={position.id} className={editingId === position.id ? "ring-2 ring-brand-6/30" : ""}>
                <CardContent className="flex flex-col gap-3 !py-4">
                  <p className="font-bold tracking-tight">{position.title}</p>
                  <p className="line-clamp-2 text-sm text-muted">{position.jobDescription}</p>
                  <div className="flex flex-wrap gap-1.5">
                    {position.criteria.map((criterion) => (
                      <span
                        key={criterion.id}
                        className="rounded-full bg-brand-1 px-2.5 py-1 text-[0.7rem] font-bold text-brand-7"
                      >
                        {criterion.name}
                      </span>
                    ))}
                  </div>
                  <div className="flex gap-2 pt-1">
                    <Button type="button" variant="outline" size="sm" onClick={() => loadForEdit(position)}>
                      {t("positions.editAction")}
                    </Button>
                    <Button asChild size="sm">
                      <Link to="/positions/$positionId" params={{ positionId: position.id }}>
                        {t("positions.open")}
                      </Link>
                    </Button>
                  </div>
                </CardContent>
              </Card>
            ))
          )}
        </section>
      </div>
    </AppShell>
  );
}
