import { Card, CardContent, CardHeader, CardTitle } from "@hirelens/ui";
import { useTranslation } from "react-i18next";

export function TokenUsageChart({
  used,
  limit
}: {
  used: number;
  limit: number;
}) {
  const { t } = useTranslation();
  const ratio = limit === 0 ? 0 : Math.min(100, Math.round((used / limit) * 100));
  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("admin.metering")}</CardTitle>
      </CardHeader>
      <CardContent>
        <div className="h-3 rounded-full bg-brand-2" role="meter" aria-valuenow={ratio} aria-valuemin={0} aria-valuemax={100}>
          <div className="h-3 rounded-full bg-brand" style={{ width: `${ratio}%` }} />
        </div>
        <p className="mt-2 text-sm text-muted">
          {used} / {limit}
        </p>
      </CardContent>
    </Card>
  );
}
