import type { RecordDecision } from "@hirelens/api-client";
import { Button, Card, CardContent, CardHeader, CardTitle, cn } from "@hirelens/ui";
import { useState } from "react";
import { useTranslation } from "react-i18next";

const outcomes: RecordDecision["outcome"][] = ["advance", "hold", "reject"];

export function DecisionPanel({
  onSubmit,
  busy,
  onInviteInterview,
  inviteBusy
}: {
  onSubmit: (input: RecordDecision) => Promise<void>;
  busy: boolean;
  onInviteInterview?: () => void;
  inviteBusy?: boolean;
}) {
  const { t } = useTranslation();
  const [outcome, setOutcome] = useState<RecordDecision["outcome"]>("hold");
  const [rationale, setRationale] = useState("");
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    if (!rationale.trim()) {
      setError(t("decision.rationaleHint"));
      return;
    }
    setError(null);
    await onSubmit({ outcome, rationale: rationale.trim() });
    setRationale("");
  };

  return (
    <Card className="border-brand-4/40 bg-gradient-to-b from-brand-0/90 to-surface shadow-sm">
      <CardHeader className="pb-2">
        <CardTitle className="text-lg font-extrabold tracking-tight">{t("decision.title")}</CardTitle>
        <p className="text-sm leading-relaxed text-muted">{t("decision.rationaleHint")}</p>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <div className="grid grid-cols-3 gap-2">
          {outcomes.map((value) => (
            <button
              key={value}
              type="button"
              onClick={() => setOutcome(value)}
              className={cn(
                "rounded-xl border px-2 py-2.5 text-center text-xs font-bold transition-colors sm:text-sm",
                outcome === value
                  ? value === "advance"
                    ? "border-brand-6 bg-brand-6 text-white"
                    : value === "reject"
                      ? "border-danger bg-danger text-danger-fg"
                      : "border-brand-5 bg-brand-1 text-brand-7"
                  : "border-border bg-white text-foreground hover:bg-brand-0"
              )}
            >
              {value === "advance"
                ? t("decision.advance")
                : value === "hold"
                  ? t("decision.hold")
                  : t("decision.reject")}
            </button>
          ))}
        </div>

        <label className="flex flex-col gap-2 text-sm font-semibold text-foreground">
          {t("decision.rationale")}
          <textarea
            className="min-h-28 rounded-xl border border-border bg-white px-3 py-3 text-sm font-medium text-foreground outline-none placeholder:text-muted focus-visible:ring-2 focus-visible:ring-focus"
            value={rationale}
            onChange={(event) => setRationale(event.target.value)}
            placeholder={t("decision.rationalePlaceholder")}
          />
        </label>

        {error ? (
          <p className="text-sm text-danger" role="alert">
            {error}
          </p>
        ) : null}

        <Button type="button" className="w-full" disabled={busy} onClick={() => void submit()}>
          {busy ? t("decision.saving") : t("decision.submit")}
        </Button>

        {onInviteInterview ? (
          <Button
            type="button"
            variant="outline"
            className="w-full"
            disabled={inviteBusy}
            onClick={onInviteInterview}
          >
            {inviteBusy ? t("interview.inviting") : t("decision.moreInterview")}
          </Button>
        ) : null}
      </CardContent>
    </Card>
  );
}
