import { Button, Card, CardContent, CardHeader, CardTitle } from "@hirelens/ui";
import { useState } from "react";
import { useTranslation } from "react-i18next";

type Criterion = { name: string; description: string; weight: number };

export function RubricEditor({
  onSave
}: {
  onSave: (name: string, criteria: Criterion[]) => Promise<void>;
}) {
  const { t } = useTranslation();
  const [name, setName] = useState("Default");
  const [criteria, setCriteria] = useState<Criterion[]>([{ name: "Core", description: "Core", weight: 100 }]);

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("admin.rubrics")}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <input className="rounded-md border border-border bg-background px-3 py-2" value={name} onChange={(event) => setName(event.target.value)} />
        {criteria.map((criterion, index) => (
          <div key={index} className="grid grid-cols-3 gap-2">
            <input
              className="rounded-md border border-border bg-background px-3 py-2"
              value={criterion.name}
              onChange={(event) => {
                const next = [...criteria];
                next[index] = { ...criterion, name: event.target.value };
                setCriteria(next);
              }}
            />
            <input
              className="rounded-md border border-border bg-background px-3 py-2"
              value={criterion.description}
              onChange={(event) => {
                const next = [...criteria];
                next[index] = { ...criterion, description: event.target.value };
                setCriteria(next);
              }}
            />
            <input
              type="number"
              className="rounded-md border border-border bg-background px-3 py-2"
              value={criterion.weight}
              onChange={(event) => {
                const next = [...criteria];
                next[index] = { ...criterion, weight: Number(event.target.value) };
                setCriteria(next);
              }}
            />
          </div>
        ))}
        <Button type="button" onClick={() => void onSave(name, criteria)}>
          {t("positions.submit")}
        </Button>
      </CardContent>
    </Card>
  );
}
