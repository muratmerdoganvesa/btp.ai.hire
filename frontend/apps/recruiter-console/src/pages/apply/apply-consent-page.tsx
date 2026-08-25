import { Button, Card, CardContent, CardHeader, CardTitle } from "@hirelens/ui";
import { Link, useParams } from "@tanstack/react-router";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { ApplyShell } from "./apply-shell";

export function ApplyConsentPage() {
  const { t } = useTranslation();
  const { slug } = useParams({ from: "/apply/$slug/consent" });
  const [accepted, setAccepted] = useState(false);

  return (
    <ApplyShell>
      <Card>
        <CardHeader>
          <CardTitle>{t("apply.consentTitle")}</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-4 text-sm leading-6">
          <p>{t("apply.consentBody1")}</p>
          <p>{t("apply.consentBody2")}</p>
          <ul className="list-disc space-y-1 pl-5 text-muted">
            <li>{t("apply.consentProcessed")}</li>
            <li>{t("apply.consentNotProcessed")}</li>
            <li>{t("apply.consentRetention")}</li>
            <li>{t("apply.consentAppeal")}</li>
          </ul>
          <p>
            <a href="/kvkk" className="font-medium text-brand underline-offset-2 hover:underline">
              {t("apply.kvkkLink")}
            </a>
          </p>
          <label className="flex items-start gap-3 rounded-xl border border-border bg-brand-0/50 p-4">
            <input
              type="checkbox"
              className="mt-1"
              checked={accepted}
              onChange={(event) => setAccepted(event.target.checked)}
            />
            <span>{t("apply.consentCheckbox")}</span>
          </label>
          <div className="flex flex-wrap gap-3 pt-2">
            {accepted ? (
              <Button asChild>
                <Link to="/apply/$slug/form" params={{ slug }}>
                  {t("apply.consentContinue")}
                </Link>
              </Button>
            ) : (
              <Button type="button" disabled>
                {t("apply.consentContinue")}
              </Button>
            )}
            <Button asChild variant="outline">
              <Link to="/apply/$slug" params={{ slug }}>
                {t("apply.cancel")}
              </Link>
            </Button>
          </div>
        </CardContent>
      </Card>
    </ApplyShell>
  );
}
