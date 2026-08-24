import { Button, Card, CardContent, Chip } from "@hirelens/ui";
import { useForm } from "react-hook-form";
import { useTranslation } from "react-i18next";
import { useNavigate } from "@tanstack/react-router";
import { z } from "zod";
import { api } from "../api";
import { isDevAuth } from "../auth-mode";
import { useAuthStore } from "../auth-store";
import { Field, TextInput } from "../components/field";

const schema = z.object({
  tenantId: z.string().uuid(),
  subject: z.string().min(1),
  role: z.enum(["Recruiter", "HiringManager", "TenantAdmin"])
});

type FormValues = z.infer<typeof schema>;

const roles: FormValues["role"][] = ["Recruiter", "HiringManager", "TenantAdmin"];

export function LoginPage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const setSession = useAuthStore((s) => s.setSession);
  const form = useForm<FormValues>({
    defaultValues: {
      tenantId: crypto.randomUUID(),
      subject: "recruiter.local",
      role: "Recruiter"
    }
  });
  const selectedRole = form.watch("role");

  if (!isDevAuth) {
    window.location.replace("/");
    return null;
  }

  const onSubmit = form.handleSubmit(async (values) => {
    const parsed = schema.safeParse(values);
    if (!parsed.success) {
      return;
    }

    try {
      const token = await api.issueDevToken({
        tenantId: parsed.data.tenantId,
        subject: parsed.data.subject,
        roles: [parsed.data.role]
      });
      setSession({
        token,
        tenantId: parsed.data.tenantId,
        subject: parsed.data.subject,
        roles: [parsed.data.role]
      });
      await navigate({ to: "/" });
    } catch {
      form.setError("root", { message: t("errors.loginFailed") });
    }
  });

  return (
    <main className="grid min-h-screen lg:grid-cols-[1.1fr_0.9fr]">
      <section className="relative hidden overflow-hidden bg-brand-9 px-12 py-16 text-brand-0 lg:flex lg:flex-col lg:justify-between">
        <div className="pointer-events-none absolute -left-24 top-20 size-80 rounded-full bg-brand-7/40 blur-3xl" />
        <div className="pointer-events-none absolute -right-16 bottom-10 size-96 rounded-full bg-brand-6/30 blur-3xl" />
        <div className="relative">
          <p className="text-sm font-medium tracking-wide text-brand-3">{t("login.kicker")}</p>
          <h1 className="mt-4 max-w-lg text-4xl font-semibold leading-tight tracking-tight">{t("login.headline")}</h1>
          <p className="mt-4 max-w-md text-sm leading-6 text-brand-3">{t("login.subhead")}</p>
        </div>
        <ul className="relative mt-12 flex max-w-md flex-col gap-4 text-sm leading-6">
          {[t("login.point1"), t("login.point2"), t("login.point3")].map((point) => (
            <li key={point} className="flex gap-3 rounded-2xl border border-brand-7/60 bg-brand-10/30 p-4">
              <span className="mt-0.5 inline-flex size-5 shrink-0 items-center justify-center rounded-full bg-brand-6 text-xs text-brand-0">
                ✓
              </span>
              <span>{point}</span>
            </li>
          ))}
        </ul>
        <p className="relative mt-10 text-sm font-semibold tracking-tight">{t("app.recruiter")}</p>
      </section>

      <section className="flex items-center justify-center bg-background px-6 py-12">
        <div className="w-full max-w-md">
          <p className="text-sm font-medium text-brand">{t("login.workspace")}</p>
          <h2 className="mt-2 text-2xl font-semibold tracking-tight">{t("login.title")}</h2>
          <p className="mt-2 text-sm text-muted">{t("login.hint")}</p>

          <Card className="mt-8">
            <CardContent className="pt-6">
              <form className="flex flex-col gap-5" onSubmit={onSubmit}>
                <Field label={t("login.tenantId")}>
                  <div className="flex gap-2">
                    <TextInput className="font-mono text-xs" {...form.register("tenantId")} />
                    <Button
                      type="button"
                      variant="outline"
                      className="shrink-0"
                      onClick={() => form.setValue("tenantId", crypto.randomUUID())}
                    >
                      {t("login.newTenant")}
                    </Button>
                  </div>
                </Field>
                <Field label={t("login.subject")}>
                  <TextInput {...form.register("subject")} />
                </Field>
                <fieldset className="flex flex-col gap-2">
                  <legend className="text-sm text-muted">{t("login.role")}</legend>
                  <div className="flex flex-wrap gap-2">
                    {roles.map((role) => (
                      <Chip key={role} selected={selectedRole === role} onClick={() => form.setValue("role", role)}>
                        {t(`login.roles.${role}`)}
                      </Chip>
                    ))}
                  </div>
                </fieldset>
                {form.formState.errors.root ? (
                  <p className="text-sm text-danger" role="alert">
                    {form.formState.errors.root.message}
                  </p>
                ) : null}
                <Button type="submit" size="lg" className="w-full">
                  {t("login.submit")}
                </Button>
              </form>
            </CardContent>
          </Card>
        </div>
      </section>
    </main>
  );
}
