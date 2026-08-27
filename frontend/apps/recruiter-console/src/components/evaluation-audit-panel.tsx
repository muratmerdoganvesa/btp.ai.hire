import type { Decision, EvaluationAudit } from "@hirelens/api-client";
import { Card, CardContent, CardHeader, CardTitle } from "@hirelens/ui";
import { useTranslation } from "react-i18next";

const outcomeKeys: Record<string, string> = {
  advance: "decision.advance",
  hold: "decision.hold",
  reject: "decision.reject"
};

const statusKeys: Record<string, string> = {
  completed: "evaluation.statusCompleted",
  failed: "evaluation.statusFailed",
  pending: "evaluation.statusPending",
  processing: "evaluation.statusProcessing"
};

export function EvaluationAuditPanel({ audit, decisions }: { audit: EvaluationAudit; decisions: Decision[] }) {
  const { t } = useTranslation();
  const statusLabel = statusKeys[audit.status] ? t(statusKeys[audit.status]) : t("evaluation.statusReady");

  return (
    <Card className="border-border/80">
      <CardHeader className="pb-2">
        <CardTitle className="text-base font-extrabold tracking-tight">{t("evaluation.auditTrail")}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-5 text-sm">
        <dl className="grid grid-cols-2 gap-3">
          <Item label={t("evaluation.coverage")} value={`${Math.round(audit.coverageRatio * 100)}%`} />
          <Item label={t("evaluation.auditStatus")} value={statusLabel} />
          <Item
            label={t("evaluation.auditExecutedAt")}
            value={audit.executedAt ? new Date(audit.executedAt).toLocaleString() : "—"}
          />
        </dl>
        <section>
          <h3 className="mb-2 text-sm font-bold text-foreground">{t("evaluation.audit")}</h3>
          {decisions.length === 0 ? (
            <p className="text-muted">{t("evaluation.noAudit")}</p>
          ) : (
            <ul className="space-y-3">
              {decisions.map((decision) => (
                <li key={decision.id} className="rounded-xl border border-border bg-brand-0/40 px-3 py-2.5">
                  <p className="font-semibold text-foreground">
                    {outcomeKeys[decision.outcome] ? t(outcomeKeys[decision.outcome]) : decision.outcome}
                  </p>
                  <p className="mt-0.5 text-muted">{decision.rationale}</p>
                  <p className="mt-1 text-xs text-muted">{new Date(decision.decidedAt).toLocaleString()}</p>
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
      <dt className="text-xs font-medium text-muted">{label}</dt>
      <dd className="mt-0.5 font-semibold text-foreground">{value}</dd>
    </div>
  );
}
