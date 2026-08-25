import { Badge, Button, Card, CardContent, CardHeader, CardTitle, InitialsAvatar } from "@hirelens/ui";
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
      await queryClient.invalidateQueries({ queryKey: ["funnel"] });
    }
  });

  const me = useQuery({
    queryKey: ["me"],
    queryFn: () => api.getMe(),
    enabled: Boolean(session)
  });

  const tenant = useQuery({
    queryKey: ["tenant", session?.tenantId],
    queryFn: () => api.getCurrentTenant(),
    enabled: Boolean(session)
  });

  const positions = useQuery({
    queryKey: ["positions"],
    queryFn: () => api.listPositions(),
    enabled: Boolean(session)
  });

  const funnel = useQuery({
    queryKey: ["funnel"],
    queryFn: () => api.getFunnel(),
    enabled: Boolean(session),
    retry: false
  });

  if (!session) {
    if (isDevAuth) {
      void navigate({ to: "/login" });
    }
    return null;
  }

  const list = positions.data ?? [];
  const displayName = tenant.data?.name?.trim() || t("dashboard.tenant");

  return (
    <AppShell>
      <header className="flex flex-wrap items-end justify-between gap-5">
        <div className="flex items-center gap-4">
          <InitialsAvatar name={displayName} className="size-12 rounded-lg text-sm" />
          <div>
            <p className="text-[0.7rem] font-semibold uppercase tracking-[0.18em] text-brand-7">
              {t("dashboard.welcome")}
            </p>
            <h1 className="font-display mt-1 text-[2.35rem] font-semibold leading-none tracking-tight text-foreground">
              {t("dashboard.title")}
            </h1>
            <p className="mt-2 max-w-lg text-sm text-muted">{t("dashboard.subtitle")}</p>
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
          <Button type="button" variant="outline" onClick={() => startTour(true)}>
            {t("tour.start")}
          </Button>
          <Button type="button" variant="outline" disabled={seed.isPending} onClick={() => seed.mutate()}>
            {seed.isPending ? t("dashboard.seedDemoBusy") : t("dashboard.seedDemo")}
          </Button>
          <Button asChild>
            <Link to="/positions">{list.length === 0 ? t("dashboard.createFirst") : t("dashboard.openPositions")}</Link>
          </Button>
        </div>
        {seed.isSuccess ? (
          <p className="basis-full text-sm text-muted">
            {seed.data.skipped ? t("dashboard.seedDemoSkip") : t("dashboard.seedDemoDone")}
          </p>
        ) : null}
      </header>

      <section data-tour="tour-funnel" className="hl-rise-delay grid gap-3 sm:grid-cols-2 xl:grid-cols-5">
        <Metric label={t("dashboard.openReqs")} value={String(funnel.data?.positions ?? list.length)} />
        <Metric label={t("dashboard.funnelCandidates")} value={String(funnel.data?.candidates ?? "—")} />
        <Metric label={t("dashboard.funnelEvaluations")} value={String(funnel.data?.evaluations ?? "—")} />
        <Metric label={t("dashboard.funnelInterviews")} value={String(funnel.data?.interviews ?? "—")} />
        <Metric label={t("dashboard.funnelDecisions")} value={String(funnel.data?.decisions ?? "—")} />
      </section>

      <section className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,17rem)]">
        <Card data-tour="tour-recent">
          <CardHeader className="flex-row items-center justify-between gap-3 !border-b-border/70">
            <CardTitle>{t("dashboard.recent")}</CardTitle>
            <Link to="/positions" className="text-sm font-semibold text-brand-7 transition-colors hover:text-brand-9">
              {t("dashboard.openPositions")}
            </Link>
          </CardHeader>
          <CardContent>
            {list.length === 0 ? (
              <div className="rounded-md border border-dashed border-border bg-brand-0 px-5 py-10 text-center">
                <p className="font-display text-lg text-foreground">{t("dashboard.empty")}</p>
                <Button asChild className="mt-4">
                  <Link to="/positions">{t("dashboard.createFirst")}</Link>
                </Button>
              </div>
            ) : (
              <ul className="divide-y divide-border/80">
                {list.slice(0, 5).map((position) => (
                  <li key={position.id} className="flex items-center justify-between gap-4 py-3.5 first:pt-0 last:pb-0">
                    <div className="min-w-0">
                      <p className="truncate font-semibold tracking-tight">{position.title}</p>
                      <p className="mt-0.5 truncate text-sm text-muted">
                        {position.criteria.map((criterion) => `${criterion.name} ${criterion.weight}`).join(" · ")}
                      </p>
                    </div>
                    <Link
                      to="/positions/$positionId"
                      params={{ positionId: position.id }}
                      className="shrink-0 text-sm font-semibold text-brand-7 transition-colors hover:text-brand-9"
                    >
                      {t("positions.open")}
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>

        <Card data-tour="tour-oversight">
          <CardContent>
            <p className="text-[0.65rem] font-semibold uppercase tracking-[0.16em] text-muted">
              {t("dashboard.session")}
            </p>
            <p className="font-display mt-3 text-2xl font-semibold tracking-tight">{displayName}</p>
            <p className="mt-1 text-sm text-muted">{me.data?.roles?.[0] ?? session.roles[0]}</p>
            <div className="mt-4 flex flex-wrap gap-1.5" aria-label={t("dashboard.roles")}>
              {(me.data?.roles ?? session.roles).map((role) => (
                <Badge key={role} tone="muted">
                  {role}
                </Badge>
              ))}
            </div>
            <p className="mt-6 border-t border-border/80 pt-4 text-xs leading-5 text-muted">
              {t("dashboard.humanOversight")}
            </p>
          </CardContent>
        </Card>
      </section>
    </AppShell>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <Card className="hl-metric">
      <CardContent className="pl-5">
        <p className="text-[0.65rem] font-semibold uppercase tracking-[0.14em] text-muted">{label}</p>
        <p className="font-display mt-2 text-3xl font-semibold tracking-tight tabular-nums">{value}</p>
      </CardContent>
    </Card>
  );
}
