import { Badge, Card, CardContent, CardHeader, CardTitle } from "@hirelens/ui";
import { useTranslation } from "react-i18next";

const GUID_RE =
  /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;

export function RiskFlagList({
  flags,
  labelById
}: {
  flags: string[];
  /** Maps criterion IDs (or other codes) to human-readable labels for HR. */
  labelById?: Record<string, string>;
}) {
  const { t } = useTranslation();
  const labels = flags
    .map((flag) => resolveFlag(flag, labelById, t))
    .filter((label, index, all) => all.indexOf(label) === index);

  return (
    <Card className="border-border/80">
      <CardHeader className="pb-2">
        <CardTitle className="text-base font-extrabold tracking-tight">{t("evaluation.risks")}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-wrap gap-2">
        {labels.length === 0 ? (
          <p className="text-sm text-muted">{t("evaluation.noRisks")}</p>
        ) : (
          labels.map((label) => (
            <Badge key={label} tone="danger">
              {label}
            </Badge>
          ))
        )}
      </CardContent>
    </Card>
  );
}

function resolveFlag(
  flag: string,
  labelById: Record<string, string> | undefined,
  t: (key: string, opts?: Record<string, string>) => string
): string {
  const mapped = labelById?.[flag];
  if (mapped) {
    return t("evaluation.criterionNeedsCheck", { name: mapped });
  }
  if (GUID_RE.test(flag.trim())) {
    return t("evaluation.genericVerification");
  }
  return flag;
}
