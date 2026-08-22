import { Card, CardContent, CardHeader, CardTitle } from "@hirelens/ui";
import { useTranslation } from "react-i18next";

export function CandidateCompare({
  left,
  right
}: {
  left: string;
  right: string;
}) {
  const { t } = useTranslation();
  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("candidates.title")}</CardTitle>
      </CardHeader>
      <CardContent className="grid grid-cols-2 gap-3 text-sm">
        <p>{left || t("dashboard.empty")}</p>
        <p>{right || t("dashboard.empty")}</p>
      </CardContent>
    </Card>
  );
}
