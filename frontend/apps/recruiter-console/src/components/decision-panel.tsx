import type { RecordDecision } from "@hirelens/api-client";
import { Button, Card, CardContent, CardHeader, CardTitle } from "@hirelens/ui";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { OverrideDialog } from "./override-dialog";

export function DecisionPanel({
  onSubmit,
  busy
}: {
  onSubmit: (input: RecordDecision) => Promise<void>;
  busy: boolean;
}) {
  const { t } = useTranslation();
  const [outcome, setOutcome] = useState<RecordDecision["outcome"]>("hold");
  const [rationale, setRationale] = useState("");
  const [open, setOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    if (!rationale.trim()) {
      setError(t("decision.rationaleHint"));
      return;
    }

    setError(null);
    await onSubmit({ outcome, rationale });
    setOpen(false);
    setRationale("");
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("decision.title")}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <p className="text-sm text-muted">{t("decision.rationaleHint")}</p>
        <label className="flex flex-col gap-1 text-sm text-muted">
          {t("decision.outcome")}
          <select
            className="rounded-md border border-border bg-background px-3 py-2 text-foreground"
            value={outcome}
            onChange={(event) => setOutcome(event.target.value as RecordDecision["outcome"])}
          >
            <option value="advance">{t("decision.advance")}</option>
            <option value="hold">{t("decision.hold")}</option>
            <option value="reject">{t("decision.reject")}</option>
          </select>
        </label>
        <label className="flex flex-col gap-1 text-sm text-muted">
          {t("decision.rationale")}
          <textarea
            className="min-h-24 rounded-md border border-border bg-background px-3 py-2 text-foreground"
            value={rationale}
            onChange={(event) => setRationale(event.target.value)}
          />
        </label>
        {error ? (
          <p className="text-sm text-danger" role="alert">
            {error}
          </p>
        ) : null}
        <div className="flex gap-2">
          <Button type="button" disabled={busy} onClick={() => void submit()}>
            {t("decision.submit")}
          </Button>
          <Button type="button" variant="outline" onClick={() => setOpen(true)}>
            {t("decision.override")}
          </Button>
        </div>
        <OverrideDialog open={open} onClose={() => setOpen(false)}>
          <p className="text-sm text-muted">{t("decision.rationaleHint")}</p>
        </OverrideDialog>
      </CardContent>
    </Card>
  );
}
