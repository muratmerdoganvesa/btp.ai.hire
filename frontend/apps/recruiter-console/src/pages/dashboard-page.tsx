import { Badge, Button, Card, CardContent, CardHeader, CardTitle, InitialsAvatar } from "@hirelens/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import { api } from "../api";
import { AppShell } from "../components/app-shell";
import { isDevAuth } from "../auth-mode";
import { useAuthStore } from "../auth-store";

export function DashboardPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const session = useAuthStore((s) => s.session);
  const queryClient = useQueryClient();
  const seed = useMutation({
    mutationFn: () => api.seedDemo(),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ["positions"] });
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

  return (
    <AppShell>
      <header className="flex flex-wrap items-end justify-between gap-6">
        <div className="flex items-center gap-4">
          <InitialsAvatar name={session.subject} className="size-14 rounded-2xl text-sm shadow-card" />
          <div>
            <p className="text-xs font-semibold uppercase tracking-[0.16em] text-brand">{t("dashboard.welcome")}</p>
            <h1 className="font-display mt-1 text-4xl font-semibold tracking-tight text-foreground">
              {t("dashboard.title")}
            </h1>
            <p className="mt-2 max-w-lg text-sm text-muted">{t("dashboard.subtitle")}</p>
          </div>
        </div>
        <div className="flex flex-wrap gap-2">
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

      <section className="hl-rise-delay grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
        <Metric label={t("dashboard.openReqs")} value={String(funnel.data?.positions ?? list.length)} />
        <Metric label={t("dashboard.funnelCandidates")} value={String(funnel.data?.candidates ?? "—")} />
        <Metric label={t("dashboard.funnelEvaluations")} value={String(funnel.data?.evaluations ?? "—")} />
        <Metric label={t("dashboard.funnelInterviews")} value={String(funnel.data?.interviews ?? "—")} />
        <Metric label={t("dashboard.funnelDecisions")} value={String(funnel.data?.decisions ?? "—")} />
      </section>

      <section className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,18rem)]">
        <Card className="border-border/80 bg-surface/90">
          <CardHeader className="flex-row items-center justify-between">
            <CardTitle className="font-display text-2xl">{t("dashboard.recent")}</CardTitle>
            <Link to="/positions" className="text-sm font-semibold text-brand transition-colors hover:text-brand-7">
              {t("dashboard.openPositions")}
            </Link>
          </CardHeader>
          <CardContent>
            {list.length === 0 ? (
              <div className="rounded-xl border border-dashed border-brand-3/70 bg-gradient-to-br from-brand-1/80 to-transparent px-6 py-12 text-center">
                <p className="font-display text-lg text-foreground">{t("dashboard.empty")}</p>
                {seed.isSuccess ? (
                  <p className="mt-3 text-sm text-muted">
                    {seed.data.skipped ? t("dashboard.seedDemoSkip") : t("dashboard.seedDemoDone")}
                  </p>
                ) : null}
                <Button asChild className="mt-5">
                  <Link to="/positions">{t("dashboard.createFirst")}</Link>
                </Button>
              </div>
            ) : (
              <ul className="divide-y divide-border/80">
                {list.slice(0, 5).map((position) => (
                  <li key={position.id} className="flex items-center justify-between gap-4 py-3.5 first:pt-0 last:pb-0">
                    <div>
                      <p className="font-medium">{position.title}</p>
                      <p className="mt-0.5 text-sm text-muted">
                        {position.criteria.map((criterion) => `${criterion.name} ${criterion.weight}`).join(" · ")}
                      </p>
                    </div>
                    <Link
                      to="/positions/$positionId"
                      params={{ positionId: position.id }}
                      className="text-sm font-semibold text-brand transition-colors hover:text-brand-7"
                    >
                      {t("positions.open")}
                    </Link>
                  </li>
                ))}
              </ul>
            )}
          </CardContent>
        </Card>

        <Card className="overflow-hidden border-border/80 bg-surface/90">
          <CardContent className="pt-5">
            <p className="text-[0.68rem] font-semibold uppercase tracking-[0.14em] text-muted">{t("dashboard.session")}</p>
            <p className="font-display mt-3 truncate text-2xl font-semibold tracking-tight">
              {tenant.data?.name ?? t("dashboard.tenant")}
            </p>
            <p className="mt-1 truncate text-sm text-muted">{me.data?.subject ?? session.subject}</p>
            <div className="mt-4 flex flex-wrap gap-2" aria-label={t("dashboard.roles")}>
              {(me.data?.roles ?? session.roles).map((role) => (
                <Badge key={role}>{role}</Badge>
              ))}
            </div>
            <p className="mt-6 text-xs leading-5 text-muted">{t("dashboard.humanOversight")}</p>
          </CardContent>
        </Card>
      </section>
    </AppShell>
  );
}

function Metric({ label, value }: { label: string; value: string }) {
  return (
    <Card className="border-border/80 bg-surface/90 transition-transform duration-300 hover:-translate-y-0.5">
      <CardContent className="pt-5">
        <p className="text-[0.68rem] font-semibold uppercase tracking-[0.14em] text-muted">{label}</p>
        <p className="font-display mt-3 text-4xl font-semibold tracking-tight tabular-nums">{value}</p>
      </CardContent>
    </Card>
  );
}
