import { ApiClient } from "@hirelens/api-client";
import { Button } from "@hirelens/ui";
import { createI18n } from "@hirelens/i18n";
import { StrictMode, useMemo, useState } from "react";
import { createRoot } from "react-dom/client";
import { useTranslation } from "react-i18next";
import { BiasMonitorPanel } from "./components/bias-monitor-panel";
import { CandidateCompare } from "./components/candidate-compare";
import { ModelPolicyForm } from "./components/model-policy-form";
import { PipelineBoard } from "./components/pipeline-board";
import { RubricEditor } from "./components/rubric-editor";
import { TokenUsageChart } from "./components/token-usage-chart";
import "./styles.css";

createI18n();

function AdminApp() {
  const { t } = useTranslation();
  const [token, setToken] = useState<string | null>(null);
  const api = useMemo(() => new ApiClient("", () => token), [token]);
  const [hue, setHue] = useState(250);
  const [funnel, setFunnel] = useState<{
    positions: number;
    candidates: number;
    evaluations: number;
    interviews: number;
    decisions: number;
  } | null>(null);
  const [bias, setBias] = useState<{ band: string; count: number }[]>([]);
  const [quota, setQuota] = useState<{ monthlyTokenLimit: number; usedTokens: number; remainingTokens: number } | null>(
    null
  );

  const login = async () => {
    const access = await api.issueDevToken({
      tenantId: crypto.randomUUID(),
      subject: "admin.local",
      roles: ["TenantAdmin"]
    });
    setToken(access);
  };

  const load = async () => {
    const [theme, funnelDto, biasDto, quotaDto] = await Promise.all([
      api.getTheme(),
      api.getFunnel(),
      api.getBias(),
      api.getQuota()
    ]);
    setHue(theme.brandHue);
    document.documentElement.style.setProperty("--hl-brand-hue", String(theme.brandHue));
    setFunnel(funnelDto);
    setBias(biasDto);
    setQuota(quotaDto);
  };

  return (
    <main className="mx-auto flex min-h-screen max-w-5xl flex-col gap-4 p-6">
      <h1 className="text-lg font-semibold">{t("app.admin")}</h1>
      {!token ? (
        <Button type="button" onClick={() => void login()}>
          {t("login.submit")}
        </Button>
      ) : (
        <div className="grid gap-4 md:grid-cols-2">
          <div className="flex flex-col gap-3 rounded-md border border-border p-4">
            <h2 className="font-medium">{t("admin.theme")}</h2>
            <input type="number" value={hue} onChange={(event) => setHue(Number(event.target.value))} />
            <Button
              type="button"
              onClick={() =>
                void api
                  .updateTheme({ brandHue: hue, logoUrl: null, radiusScale: 1, interviewWeight: 30 })
                  .then(() => document.documentElement.style.setProperty("--hl-brand-hue", String(hue)))
              }
            >
              {t("positions.submit")}
            </Button>
          </div>
          <div className="flex flex-col gap-3 rounded-md border border-border p-4">
            <Button
              type="button"
              onClick={() =>
                void api.provisionTenant({
                  tenantId: crypto.randomUUID(),
                  name: "New tenant",
                  slug: `t-${Date.now()}`,
                  adminSubject: "admin.provisioned"
                })
              }
            >
              {t("admin.provision")}
            </Button>
            <Button type="button" variant="outline" onClick={() => void load()}>
              {t("admin.funnel")}
            </Button>
          </div>
          <RubricEditor onSave={(name, criteria) => api.createRubric(name, criteria).then(() => undefined)} />
          <ModelPolicyForm
            onSave={(taskType, modelId, region) => api.upsertPolicy(taskType, modelId, region).then(() => undefined)}
          />
          <PipelineBoard funnel={funnel} />
          <TokenUsageChart used={quota?.usedTokens ?? 0} limit={quota?.monthlyTokenLimit ?? 0} />
          <BiasMonitorPanel bands={bias} />
          <CandidateCompare left={t("admin.funnel")} right={t("admin.bias")} />
        </div>
      )}
    </main>
  );
}

const root = document.getElementById("root");
if (!root) {
  throw new Error("Root element is missing.");
}

createRoot(root).render(
  <StrictMode>
    <AdminApp />
  </StrictMode>
);
