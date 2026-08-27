import { Button } from "@hirelens/ui";
import { useQuery } from "@tanstack/react-query";
import { Link, useParams } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import { api } from "../api";
import { InterviewQuestionsPanel } from "../components/interview-questions-panel";
import { PageBody, PageHero } from "../components/page-hero";

export function PositionQuestionsPage() {
  const { t } = useTranslation();
  const { positionId } = useParams({ from: "/_app/positions/$positionId/questions" });
  const position = useQuery({
    queryKey: ["position", positionId],
    queryFn: () => api.getPosition(positionId)
  });

  return (
    <>
      <PageHero
        kicker={position.data?.title ?? t("nav.positions")}
        title={t("positions.interviewQuestions")}
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <Button
              asChild
              size="sm"
              className="!bg-white !text-[#151f66] hover:!bg-white/90"
            >
              <Link to="/positions/$positionId/edit" params={{ positionId }}>
                {t("positions.editAction")}
              </Link>
            </Button>
            <Button
              asChild
              variant="outline"
              size="sm"
              className="!border-white/40 !bg-white/10 !text-white hover:!bg-white/20 hover:!text-white"
            >
              <Link to="/positions">{t("positions.backToList")}</Link>
            </Button>
          </div>
        }
      />
      <PageBody>
        <p className="text-sm text-muted">{t("positions.interviewQuestionsPageHint")}</p>
        {position.isLoading ? (
          <p className="text-sm text-muted">{t("positions.loading")}</p>
        ) : (
          <InterviewQuestionsPanel questions={position.data?.interviewQuestions} empty />
        )}
      </PageBody>
    </>
  );
}
