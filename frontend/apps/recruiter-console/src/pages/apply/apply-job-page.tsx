import { ApiError, PublicApi } from "@hirelens/api-client";
import { Button, Card, CardContent, CardHeader, CardTitle } from "@hirelens/ui";
import { Link, useParams } from "@tanstack/react-router";
import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { ApplyShell } from "./apply-shell";

export const publicApi = new PublicApi("");

export function ApplyJobPage() {
  const { t } = useTranslation();
  const { slug } = useParams({ from: "/apply/$slug" });
  const job = useQuery({
    queryKey: ["public-job", slug],
    queryFn: () => publicApi.getPublicJob(slug),
    retry: (count, error) => !(error instanceof ApiError && error.status === 404) && count < 2
  });

  if (job.isLoading) {
    return <ApplyShell>{t("apply.loading")}</ApplyShell>;
  }

  if (job.isError && job.error instanceof ApiError && job.error.status === 404) {
    return (
      <ApplyShell>
        <Card>
          <CardContent className="py-10 text-center">
            <h1 className="text-xl font-semibold">{t("apply.notFound")}</h1>
          </CardContent>
        </Card>
      </ApplyShell>
    );
  }

  const data = job.data!;
  if (!data.isOpen) {
    return (
      <ApplyShell>
        <Card>
          <CardContent className="flex flex-col gap-4 py-8">
            <p>{t("apply.closed")}</p>
            <Button asChild variant="outline">
              <Link to="/">{t("apply.backHome")}</Link>
            </Button>
          </CardContent>
        </Card>
      </ApplyShell>
    );
  }

  return (
    <ApplyShell>
      <Card>
        <CardHeader>
          <CardTitle>{data.title}</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <p className="whitespace-pre-wrap text-sm leading-6">{data.jobDescription}</p>
          <section>
            <h2 className="text-sm font-semibold">{t("apply.requirements")}</h2>
            <ul className="mt-2 list-disc space-y-1 pl-5 text-sm text-muted">
              {data.criteria.map((c) => (
                <li key={c.id}>{c.name}</li>
              ))}
            </ul>
          </section>
          <Button asChild size="lg" className="w-full sm:w-auto">
            <Link to="/apply/$slug/consent" params={{ slug }}>
              {t("apply.start")}
            </Link>
          </Button>
        </CardContent>
      </Card>
    </ApplyShell>
  );
}
