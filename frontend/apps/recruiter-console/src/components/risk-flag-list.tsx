import { Badge, Card, CardContent, CardHeader, CardTitle } from "@hirelens/ui";
import { useTranslation } from "react-i18next";

export function RiskFlagList({ flags }: { flags: string[] }) {
  const { t } = useTranslation();
  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("evaluation.risks")}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-wrap gap-2">
        {flags.length === 0 ? (
          <p className="text-sm text-muted">{t("evaluation.noRisks")}</p>
        ) : (
          flags.map((flag) => (
            <Badge key={flag} tone="muted">
              {flag}
            </Badge>
          ))
        )}
      </CardContent>
    </Card>
  );
}
