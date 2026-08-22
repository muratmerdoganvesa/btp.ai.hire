import { Badge, Button, Card, CardContent, CardHeader, CardTitle, InitialsAvatar } from "@hirelens/ui";
import { useQuery } from "@tanstack/react-query";
import { Link, useNavigate } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import { api } from "../api";
import { AppShell } from "../components/app-shell";
import { useAuthStore } from "../auth-store";

export function DashboardPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const session = useAuthStore((s) => s.session);

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

  if (!session) {
    void navigate({ to: "/login" });
    return null;
  }

  const list = positions.data ?? [];
  const criteriaCount = list.reduce((sum, position) => sum + position.criteria.length, 0);

  return (
    <AppShell>
      <header className="flex flex-wrap items-end justify-between gap-4">
        <div className="flex items-center gap-4">
          <InitialsAvatar name={session.subject} className="size-12 text-sm" />
          <div>
            <p className="text-sm text-brand">{t("dashboard.welcome")}</p>
            <h1 className="text-2xl font-semibold tracking-tight">{t("dashboard.title")}</h1>
            <p className="mt-1 text-sm text-muted">{t("dashboard.subtitle")}</p>
          </div>
        </div>
        <Button asChild>
          <Link to="/positions">{list.length === 0 ? t("dashboard.createFirst") : t("dashboard.openPositions")}</Link>
        </Button>
      </header>

      <section className="grid gap-4 sm:grid-cols-3">
        <Metric label={t("dashboard.openReqs")} value={String(list.length)} />
        <Metric label={t("dashboard.criteria")} value={String(criteriaCount)} />
        <Card className="overflow-hidden">
          <CardContent className="pt-4">
            <p className="text-xs font-medium uppercase tracking-wide text-muted">{t("dashboard.session")}</p>
            <p className="mt-2 truncate text-lg font-semibold">{tenant.data?.name ?? t("dashboard.tenant")}</p>
            <p className="mt-1 truncate text-sm text-muted">{me.data?.subject ?? session.subject}</p>
            <div className="mt-3 flex flex-wrap gap-2" aria-label={t("dashboard.roles")}>
              {(me.data?.roles ?? session.roles).map((role) => (
                <Badge key={role}>{role}</Badge>
              ))}
            </div>
          </CardContent>
        </Card>
      </section>

      <Card>
        <CardHeader className="flex-row items-center justify-between">
          <CardTitle>{t("dashboard.recent")}</CardTitle>
          <Link to="/positions" className="text-sm font-medium text-brand">
            {t("dashboard.openPositions")}
          </Link>
        </CardHeader>
        <CardContent>
          {list.length === 0 ? (
            <div className="rounded-lg border border-dashed border-border bg-brand-1/50 px-6 py-10 text-center">
              <p className="text-sm text-muted">{t("dashboard.empty")}</p>
              <Button asChild className="mt-4">
                <Link to="/positions">{t("dashboard.createFirst")}</Link>
              </Button>
            </div>
          ) : (
            <ul className="divide-y divide-border">
              {list.slice(0, 5).map((position) => (
                <li key={position.id} className="flex items-center justify-between gap-4 py-3 first:pt-0 last:pb-0">
                  <div>
                    <p className="font-medium">{position.title}</p>
                    <p className="text-sm text-muted">
                      {position.criteria.map((criterion) => `${criterion.name} ${criterion.weight}`).join(" · ")}
                    </p>
                  </div>
                  <Link
                    to="/positions/$positionId"
                    params={{ positionId: position.id }}
                    className="text-sm font-medium text-brand"
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
    <Card>
      <CardContent className="pt-4">
        <p className="text-xs font-medium uppercase tracking-wide text-muted">{label}</p>
        <p className="mt-3 text-3xl font-semibold tracking-tight">{value}</p>
      </CardContent>
    </Card>
  );
}
