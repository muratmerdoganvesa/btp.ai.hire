import type { Decision, EvaluationAudit } from "@hirelens/api-client";
import { Card, CardContent, CardHeader, CardTitle } from "@hirelens/ui";
import { useTranslation } from "react-i18next";

export function EvaluationAuditPanel({ audit, decisions }: { audit: EvaluationAudit; decisions: Decision[] }) {
  const { t } = useTranslation();
  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("evaluation.auditTrail")}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4 text-sm">
        <dl className="grid gap-2 sm:grid-cols-2">
          <Item label={t("evaluation.promptVersion")} value={audit.promptVersion} />
          <Item label={t("evaluation.rubricVersion")} value={audit.rubricVersion} />
          <Item label={t("evaluation.model")} value={`${audit.modelName}@${audit.modelVersion}`} />
          <Item label={t("evaluation.coverage")} value={`${Math.round(audit.coverageRatio * 100)}%`} />
          <Item label={t("evaluation.auditStatus")} value={audit.status} />
          <Item
            label={t("evaluation.auditExecutedAt")}
            value={audit.executedAt ? new Date(audit.executedAt).toLocaleString() : "—"}
          />
        </dl>
        <section>
          <h3 className="mb-2 font-semibold">{t("evaluation.audit")}</h3>
          {decisions.length === 0 ? (
            <p className="text-muted">{t("evaluation.noAudit")}</p>
          ) : (
            <ul className="space-y-3">
              {decisions.map((decision) => (
                <li key={decision.id} className="border-l-2 border-brand pl-3">
                  <p className="font-medium">{decision.outcome}</p>
                  <p className="text-muted">{decision.rationale}</p>
                  <p className="text-xs text-muted">{new Date(decision.decidedAt).toLocaleString()}</p>
                </li>
              ))}
            </ul>
          )}
        </section>
      </CardContent>
    </Card>
  );
}

function Item({ label, value }: { label: string; value: string }) {
  return (
    <div>
      <dt className="text-xs uppercase tracking-wide text-muted">{label}</dt>
      <dd className="font-medium">{value}</dd>
    </div>
  );
}
