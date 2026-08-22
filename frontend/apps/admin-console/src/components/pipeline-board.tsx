import { Card, CardContent, CardHeader, CardTitle } from "@hirelens/ui";
import { useTranslation } from "react-i18next";

export function PipelineBoard({
  funnel
}: {
  funnel: { positions: number; candidates: number; evaluations: number; interviews: number; decisions: number } | null;
}) {
  const { t } = useTranslation();
  const stages = funnel
    ? [
        ["positions", funnel.positions],
        ["candidates", funnel.candidates],
        ["evaluations", funnel.evaluations],
        ["interviews", funnel.interviews],
        ["decisions", funnel.decisions]
      ]
    : [];

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("admin.funnel")}</CardTitle>
      </CardHeader>
      <CardContent className="grid grid-cols-5 gap-2">
        {stages.map(([label, count]) => (
          <div key={String(label)} className="rounded-md border border-border p-2 text-center text-sm">
            <p className="text-muted">{label}</p>
            <p className="font-semibold">{count}</p>
          </div>
        ))}
      </CardContent>
    </Card>
  );
}
