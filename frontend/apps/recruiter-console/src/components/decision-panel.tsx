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
    await onSubmit({ outcome, rationale: rationale.trim() });
    setOpen(false);
    setRationale("");
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("decision.title")}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <p className="text-sm text-muted">{t("decision.rationaleHint")}</p>
        <label className="flex flex-col gap-2 text-sm font-semibold text-foreground">
          {t("decision.outcome")}
          <select
            className="h-11 rounded-xl border border-border bg-white px-3 text-sm font-medium text-foreground outline-none focus-visible:ring-2 focus-visible:ring-focus"
            value={outcome}
            onChange={(event) => setOutcome(event.target.value as RecordDecision["outcome"])}
          >
            <option value="advance">{t("decision.advance")}</option>
            <option value="hold">{t("decision.hold")}</option>
            <option value="reject">{t("decision.reject")}</option>
          </select>
        </label>
        <label className="flex flex-col gap-2 text-sm font-semibold text-foreground">
          {t("decision.rationale")}
          <textarea
            className="min-h-28 rounded-xl border border-border bg-white px-3 py-3 text-sm font-medium text-foreground outline-none focus-visible:ring-2 focus-visible:ring-focus"
            value={rationale}
            onChange={(event) => setRationale(event.target.value)}
          />
        </label>
        {error ? (
          <p className="text-sm text-danger" role="alert">
            {error}
          </p>
        ) : null}
        <div className="mt-2 flex flex-col gap-3 sm:flex-row">
          <Button type="button" className="sm:min-w-[9.5rem]" disabled={busy} onClick={() => void submit()}>
            {t("decision.submit")}
          </Button>
          <Button type="button" variant="outline" className="sm:min-w-[9.5rem]" onClick={() => setOpen(true)}>
            {t("decision.override")}
          </Button>
        </div>
        <OverrideDialog
          open={open}
          onClose={() => setOpen(false)}
          busy={busy}
          onConfirm={() => void submit()}
        >
          <label className="flex flex-col gap-2 text-sm font-semibold text-foreground">
            {t("decision.outcome")}
            <select
              className="h-11 rounded-xl border border-border bg-white px-3 text-sm font-medium text-foreground outline-none focus-visible:ring-2 focus-visible:ring-focus"
              value={outcome}
              onChange={(event) => setOutcome(event.target.value as RecordDecision["outcome"])}
            >
              <option value="advance">{t("decision.advance")}</option>
              <option value="hold">{t("decision.hold")}</option>
              <option value="reject">{t("decision.reject")}</option>
            </select>
          </label>
          <label className="mt-3 flex flex-col gap-2 text-sm font-semibold text-foreground">
            {t("decision.rationale")}
            <textarea
              className="min-h-28 rounded-xl border border-border bg-white px-3 py-3 text-sm font-medium text-foreground outline-none focus-visible:ring-2 focus-visible:ring-focus"
              value={rationale}
              onChange={(event) => setRationale(event.target.value)}
              placeholder={t("decision.overrideHint")}
            />
          </label>
          {error ? (
            <p className="mt-2 text-sm text-danger" role="alert">
              {error}
            </p>
          ) : null}
        </OverrideDialog>
      </CardContent>
    </Card>
  );
}
