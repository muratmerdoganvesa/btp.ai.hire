import { Button } from "@hirelens/ui";
import { useTranslation } from "react-i18next";

export function InterviewChat({
  turns,
  answer,
  onAnswerChange,
  onSend,
  onPause,
  onResume,
  status
}: {
  turns: { role: string; text: string }[];
  answer: string;
  onAnswerChange: (value: string) => void;
  onSend: () => void;
  onPause: () => void;
  onResume: () => void;
  status: string;
}) {
  const { t } = useTranslation();
  return (
    <div className="flex flex-col gap-3">
      <div aria-live="polite" className="flex flex-col gap-2">
        {turns.map((turn, index) => (
          <p key={index} className="text-sm">
            <strong>{turn.role}:</strong> {turn.text}
          </p>
        ))}
      </div>
      {status === "in_progress" ? (
        <>
          <textarea
            className="min-h-24 rounded-md border border-border bg-background px-3 py-2"
            value={answer}
            onChange={(event) => onAnswerChange(event.target.value)}
            aria-label={t("interview.answer")}
          />
          <div className="flex gap-2">
            <Button type="button" onClick={onSend}>
              {t("interview.send")}
            </Button>
            <Button type="button" variant="outline" onClick={onPause}>
              {t("interview.pause")}
            </Button>
          </div>
        </>
      ) : null}
      {status === "paused" ? (
        <Button type="button" onClick={onResume}>
          {t("interview.resume")}
        </Button>
      ) : null}
    </div>
  );
}
