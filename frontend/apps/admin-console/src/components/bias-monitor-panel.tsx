import { Card, CardContent, CardHeader, CardTitle } from "@hirelens/ui";
import { useTranslation } from "react-i18next";

export function BiasMonitorPanel({ bands }: { bands: { band: string; count: number }[] }) {
  const { t } = useTranslation();
  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("admin.bias")}</CardTitle>
      </CardHeader>
      <CardContent>
        <ul className="text-sm">
          {bands.map((item) => (
            <li key={item.band}>
              {item.band}: {item.count}
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}
