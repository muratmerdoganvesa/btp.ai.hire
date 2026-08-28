import type { RecordDecision } from "@hirelens/api-client";
import { Button, Card, CardContent, CardHeader, CardTitle, cn } from "@hirelens/ui";
import { useState } from "react";
import { useTranslation } from "react-i18next";

const outcomes: RecordDecision["outcome"][] = ["advance", "hold", "reject"];

export function DecisionPanel({
  onSubmit,
  busy,
  onInviteInterview,
  onInviteWhatsApp,
  inviteBusy
}: {
  onSubmit: (input: RecordDecision) => Promise<void>;
  busy: boolean;
  onInviteInterview?: () => void;
  onInviteWhatsApp?: () => void;
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
          <div className="flex flex-col gap-2">
            <Button
              type="button"
              variant="outline"
              className="w-full"
              disabled={inviteBusy}
              onClick={onInviteInterview}
            >
              {inviteBusy ? t("interview.inviting") : t("decision.moreInterview")}
            </Button>
            {onInviteWhatsApp ? (
              <Button
                type="button"
                variant="outline"
                className="w-full !border-[#25D366]/60 !text-[#128C7E] hover:!bg-[#25D366]/10"
                disabled={inviteBusy}
                onClick={onInviteWhatsApp}
              >
                <WhatsAppIcon />
                {inviteBusy ? t("interview.inviting") : t("decision.moreInterviewWhatsApp")}
              </Button>
            ) : null}
          </div>
        ) : null}
      </CardContent>
    </Card>
  );
}

function WhatsAppIcon() {
  return (
    <svg viewBox="0 0 24 24" className="size-4 shrink-0" aria-hidden="true" fill="currentColor">
      <path d="M12.04 2c-5.5 0-9.96 4.46-9.96 9.96 0 1.76.46 3.47 1.34 4.98L2 22l5.2-1.36A9.94 9.94 0 0 0 12.04 22c5.5 0 9.96-4.46 9.96-9.96C22 6.46 17.54 2 12.04 2Zm5.78 14.24c-.24.68-1.4 1.25-1.94 1.33-.5.07-1.13.1-1.83-.11-.42-.13-.97-.32-1.67-.62-2.94-1.27-4.85-4.23-5-4.42-.14-.2-1.18-1.57-1.18-3 0-1.42.74-2.12 1.01-2.41.26-.28.58-.35.77-.35h.56c.18 0 .42-.07.66.5.24.58.82 2 .89 2.15.07.14.12.32.02.51-.1.2-.14.32-.28.5-.14.17-.3.38-.42.51-.14.14-.28.3-.12.58.16.28.7 1.16 1.5 1.88 1.04.92 1.9 1.2 2.2 1.34.28.14.45.12.62-.07.16-.2.7-.81.88-1.09.18-.28.37-.23.62-.14.26.1 1.64.77 1.92.91.28.14.47.21.54.32.07.12.07.68-.17 1.36Z" />
    </svg>
  );
}
