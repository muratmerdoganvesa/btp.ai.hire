import type { CriterionScore } from "@hirelens/api-client";
import { Card, CardContent, CardHeader, CardTitle } from "@hirelens/ui";
import { useTranslation } from "react-i18next";
import { EvidenceChip } from "./evidence-chip";

export function EvidencePanel({
  scores,
  onSelect
}: {
  scores: CriterionScore[];
  onSelect?: (quote: string, start: number, end: number) => void;
}) {
  const { t } = useTranslation();
  const quotes = scores.flatMap((score) => score.evidence);

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("evaluation.source")}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-wrap gap-2">
        {quotes.length === 0 ? (
          <p className="text-sm text-muted">{t("evaluation.noScore")}</p>
        ) : (
          quotes.map((item, index) => (
            <EvidenceChip
              key={`${item.startOffset}-${index}`}
              evidence={item}
              onSelect={(selected) => onSelect?.(selected.quote, selected.startOffset, selected.endOffset)}
            />
          ))
        )}
      </CardContent>
    </Card>
  );
}
