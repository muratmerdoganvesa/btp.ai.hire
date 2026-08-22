import { Card, CardContent, CardHeader, CardTitle } from "@hirelens/ui";
import { useTranslation } from "react-i18next";

export function SourceHighlighter({
  text,
  quote
}: {
  text: string;
  quote: string | null;
}) {
  const { t } = useTranslation();
  if (!text) {
    return null;
  }

  const index = quote ? text.toLowerCase().indexOf(quote.toLowerCase()) : -1;
  const before = index >= 0 ? text.slice(0, index) : text;
  const match = index >= 0 && quote ? text.slice(index, index + quote.length) : "";
  const after = index >= 0 && quote ? text.slice(index + quote.length) : "";

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("evaluation.source")}</CardTitle>
      </CardHeader>
      <CardContent>
        <p className="whitespace-pre-wrap text-sm leading-6">
          {before}
          {match ? <mark className="bg-score-partial text-score-partial-fg">{match}</mark> : null}
          {after}
        </p>
      </CardContent>
    </Card>
  );
}
