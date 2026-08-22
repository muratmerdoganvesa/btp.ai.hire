import type { Decision } from "@hirelens/api-client";
import { Card, CardContent, CardHeader, CardTitle } from "@hirelens/ui";
import { useTranslation } from "react-i18next";

export function AuditTimeline({ decisions }: { decisions: Decision[] }) {
  const { t } = useTranslation();
  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("evaluation.audit")}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {decisions.length === 0 ? (
          <p className="text-sm text-muted">{t("evaluation.noAudit")}</p>
        ) : (
          decisions.map((decision) => (
            <div key={decision.id} className="border-l-2 border-brand pl-3">
              <p className="text-sm font-medium">{decision.outcome}</p>
              <p className="text-sm text-muted">{decision.rationale}</p>
              <p className="text-xs text-muted">{new Date(decision.decidedAt).toLocaleString()}</p>
            </div>
          ))
        )}
      </CardContent>
    </Card>
  );
}
