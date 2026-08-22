import { Card, CardContent, CardHeader, CardTitle } from "@hirelens/ui";
import { useTranslation } from "react-i18next";

export function GapCard({ gaps }: { gaps: string[] }) {
  const { t } = useTranslation();
  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("interview.gaps")}</CardTitle>
      </CardHeader>
      <CardContent>
        <ul className="list-disc pl-5 text-sm">
          {gaps.map((gap) => (
            <li key={gap}>{gap}</li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}
