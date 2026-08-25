import { Button, Card, CardContent, CardHeader, CardTitle, InitialsAvatar } from "@hirelens/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import { api } from "../api";
import { AppShell } from "../components/app-shell";
import { isDevAuth } from "../auth-mode";
import { useAuthStore } from "../auth-store";
import { useTourStore } from "../tour/tour-store";

export function DashboardPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const session = useAuthStore((s) => s.session);
  const startTour = useTourStore((s) => s.start);
  const queryClient = useQueryClient();
  const seed = useMutation({
    mutationFn: () => api.seedDemo(),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["positions"] });
      await queryClient.invalidateQueries({ queryKey: ["positions", "stats"] });
      await queryClient.invalidateQueries({ queryKey: ["funnel"] });
    }
  });

  const tenant = useQuery({
    queryKey: ["tenant", session?.tenantId],
    queryFn: () => api.getCurrentTenant(),
    enabled: Boolean(session)
  });

  const positions = useQuery({
    queryKey: ["positions", "stats"],
    queryFn: () => api.listPositions(true),
    enabled: Boolean(session)
  });

  if (!session) {
    if (isDevAuth) {
      void navigate({ to: "/login" });
    }
    return null;
  }

  const list = positions.data ?? [];
  const displayName = tenant.data?.name?.trim() || session.subject;

  return (
    <AppShell>
      <header className="flex flex-col gap-6 lg:flex-row lg:items-end lg:justify-between">
        <div className="flex min-w-0 items-start gap-4">
          <InitialsAvatar name={displayName} className="size-12 shrink-0 rounded-full text-sm" />
          <div className="min-w-0">
            <p className="text-xs font-bold uppercase tracking-[0.14em] text-brand-6">{t("dashboard.welcome")}</p>
            <h1 className="mt-1 text-3xl font-extrabold tracking-tight text-foreground">{t("dashboard.title")}</h1>
            <p className="mt-2 max-w-xl text-sm text-muted">{t("dashboard.subtitle")}</p>
            <div className="mt-3 flex flex-wrap items-center gap-x-4 gap-y-2 text-sm">
              <button
                type="button"
                className="font-semibold text-brand-6 underline-offset-2 hover:underline"
                onClick={() => startTour(true)}
              >
                {t("tour.start")}
              </button>
              <button
                type="button"
                className="font-semibold text-muted underline-offset-2 hover:text-foreground hover:underline disabled:opacity-50"
                disabled={seed.isPending}
                onClick={() => seed.mutate()}
              >
                {seed.isPending ? t("dashboard.seedDemoBusy") : t("dashboard.seedDemo")}
              </button>
            </div>
            {seed.isSuccess ? (
              <p className="mt-2 text-sm text-muted">
                {seed.data.skipped ? t("dashboard.seedDemoSkip") : t("dashboard.seedDemoDone")}
              </p>
            ) : null}
          </div>
        </div>
        <Button asChild size="lg" className="w-full shrink-0 sm:w-auto sm:min-w-[14rem]">
          <Link to="/positions">{list.length === 0 ? t("dashboard.createFirst") : t("dashboard.openPositions")}</Link>
        </Button>
      </header>

      <section className="hl-rise-delay grid items-start gap-3 sm:grid-cols-2 xl:grid-cols-4">
        <Metric label={t("dashboard.openReqs")} value={String(list.length)} />
        <Metric
          label={t("dashboard.funnelCandidates")}
          value={String(list.reduce((sum, p) => sum + (p.stats?.totalCandidates ?? 0), 0))}
        />
        <Metric
          label={t("dashboard.funnelEvaluations")}
          value={String(list.reduce((sum, p) => sum + (p.stats?.evaluatedCount ?? 0), 0))}
        />
        <Metric
          label={t("dashboard.reviewPending")}
          value={String(list.reduce((sum, p) => sum + (p.stats?.reviewPendingCount ?? 0), 0))}
        />
      </section>

      <Card>
        <CardHeader className="flex-row items-center justify-between gap-3">
          <CardTitle>{t("dashboard.jobList")}</CardTitle>
          <Link to="/positions" className="text-sm font-semibold text-brand-6 hover:text-brand-7">
            {t("dashboard.openPositions")}
          </Link>
        </CardHeader>
        <CardContent>
          {list.length === 0 ? (
            <div className="rounded-2xl border border-dashed border-border bg-brand-0 px-5 py-10 text-center">
              <p className="text-lg font-bold text-foreground">{t("dashboard.empty")}</p>
              <Button asChild className="mt-4">
                <Link to="/positions">{t("dashboard.createFirst")}</Link>
              </Button>
            </div>
          ) : (
            <ul className="divide-y divide-border">
              {list.map((position) => (
                <li key={position.id} className="flex flex-wrap items-center justify-between gap-4 py-3.5">
                  <div className="min-w-0">
                    <p className="truncate font-semibold tracking-tight">{position.title}</p>
                    <p className="mt-0.5 text-sm text-muted">
                      {position.stats?.totalCandidates ?? 0} {t("dashboard.funnelCandidates")} ·{" "}
                      {position.stats?.evaluatedCount ?? 0} {t("dashboard.funnelEvaluations")} ·{" "}
                      {position.stats?.pendingCount ?? 0} {t("dashboard.pending")}
                    </p>
                  </div>
                  <Link
                    to="/positions/$positionId"
                    params={{ positionId: position.id }}
                    className="shrink-0 text-sm font-semibold text-brand-6 hover:text-brand-7"
                  >
                    {t("positions.open")}
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>
    </AppShell>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-2xl border border-border bg-surface px-4 py-4 shadow-card">
      <div className="flex gap-3">
        <span aria-hidden="true" className="mt-0.5 w-1 shrink-0 self-stretch rounded-full bg-brand-6" />
        <div className="min-w-0">
          <p className="text-[0.65rem] font-bold uppercase tracking-[0.12em] text-muted">{label}</p>
          <p className="mt-1 text-3xl font-extrabold tabular-nums tracking-tight text-foreground">{value}</p>
        </div>
      </div>
    </div>
  );
}
