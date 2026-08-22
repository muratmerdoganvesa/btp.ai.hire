import { Button, Card, CardContent, CardHeader, CardTitle } from "@hirelens/ui";
import { useState } from "react";
import { useTranslation } from "react-i18next";

export function ModelPolicyForm({
  onSave
}: {
  onSave: (taskType: string, modelId: string, region: string | null) => Promise<void>;
}) {
  const { t } = useTranslation();
  const [taskType, setTaskType] = useState("CvParse");
  const [modelId, setModelId] = useState("cheap");
  const [region, setRegion] = useState("eu");

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("admin.models")}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-2">
        <input className="rounded-md border border-border bg-background px-3 py-2" value={taskType} onChange={(event) => setTaskType(event.target.value)} />
        <input className="rounded-md border border-border bg-background px-3 py-2" value={modelId} onChange={(event) => setModelId(event.target.value)} />
        <input className="rounded-md border border-border bg-background px-3 py-2" value={region} onChange={(event) => setRegion(event.target.value)} />
        <Button type="button" onClick={() => void onSave(taskType, modelId, region)}>
          {t("positions.submit")}
        </Button>
      </CardContent>
    </Card>
  );
}
