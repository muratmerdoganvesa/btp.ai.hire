import { Card, CardContent, CardHeader, CardTitle } from "@hirelens/ui";
import { useTranslation } from "react-i18next";

export function InterviewTranscript({ turns }: { turns: { role: string; text: string }[] }) {
  const { t } = useTranslation();
  return (
    <Card>
      <CardHeader>
        <CardTitle>{t("interview.transcript")}</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-2" aria-live="polite">
        {turns.map((turn, index) => (
          <p key={index} className="text-sm">
            <strong>{turn.role}:</strong> {turn.text}
          </p>
        ))}
      </CardContent>
    </Card>
  );
}
