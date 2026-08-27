import type { ExtractedInterviewQuestion } from "@hirelens/api-client";
import { useTranslation } from "react-i18next";

export function InterviewQuestionsPanel({
  questions,
  empty
}: {
  questions: ExtractedInterviewQuestion[] | undefined;
  empty?: boolean;
}) {
  const { t } = useTranslation();
  const items = (questions ?? []).filter((item) => item.question?.trim());

  if (items.length === 0) {
    if (!empty) {
      return null;
    }
    return (
      <section className="rounded-xl border border-dashed border-border bg-surface px-4 py-3">
        <h3 className="text-sm font-bold">{t("positions.interviewQuestions")}</h3>
        <p className="mt-1 text-xs text-muted">{t("positions.interviewQuestionsEmpty")}</p>
      </section>
    );
  }

  return (
    <section className="flex flex-col gap-3 rounded-xl border border-border bg-surface px-4 py-3">
      <div>
        <h3 className="text-sm font-bold">{t("positions.interviewQuestions")}</h3>
        <p className="mt-1 text-xs text-muted">{t("positions.interviewQuestionsHint")}</p>
      </div>
      <ol className="space-y-3">
        {items.map((item, index) => (
          <li
            key={item.questionId || `${item.criterionId}-${index}`}
            className="rounded-xl border border-border bg-brand-0/30 px-4 py-3"
          >
            <p className="text-sm font-semibold text-foreground">
              {index + 1}. {item.question}
            </p>
            {(item.whatToListenFor ?? []).length > 0 ? (
              <ul className="mt-2 list-disc space-y-0.5 pl-5 text-xs text-muted">
                {(item.whatToListenFor ?? []).map((hint, hintIndex) => (
                  <li key={`${item.questionId}-hint-${hintIndex}`}>{hint}</li>
                ))}
              </ul>
            ) : null}
          </li>
        ))}
      </ol>
    </section>
  );
}
