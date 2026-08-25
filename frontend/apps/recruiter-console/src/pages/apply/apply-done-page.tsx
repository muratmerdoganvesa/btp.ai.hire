import { PublicApi } from "@hirelens/api-client";
import { Button, Card, CardContent, CardHeader, CardTitle } from "@hirelens/ui";
import { Link, useNavigate, useParams, useSearch } from "@tanstack/react-router";
import { useMutation, useQuery } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { ApplyShell } from "./apply-shell";

const publicApi = new PublicApi("");

export function ApplyDonePage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { slug } = useParams({ from: "/apply/$slug/done" });
  const { ref } = useSearch({ from: "/apply/$slug/done" });

  const status = useQuery({
    queryKey: ["public-status", ref],
    queryFn: () => publicApi.getPublicApplicationStatus(ref!),
    enabled: Boolean(ref),
    refetchInterval: (query) =>
      query.state.data?.requiresReupload || query.state.data?.stage === "processing" ? 3000 : false
  });

  useEffect(() => {
    if (status.data?.requiresReupload) {
      void navigate({
        to: "/apply/$slug/unreadable",
        params: { slug },
        search: { ref: ref! }
      });
    }
  }, [status.data?.requiresReupload, navigate, ref, slug]);

  return (
    <ApplyShell>
      <Card>
        <CardHeader>
          <CardTitle>{t("apply.doneTitle")}</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-4 text-sm leading-6">
          <p>{t("apply.doneBody")}</p>
          {ref ? (
            <p className="rounded-xl bg-brand-0 px-4 py-3 font-mono text-base font-semibold">{ref}</p>
          ) : null}
          <p className="text-muted">{t("apply.doneNext")}</p>
          <p className="text-muted">{t("apply.doneContact")}</p>
          <Button asChild variant="outline" className="w-fit">
            <Link to="/apply/$slug" params={{ slug }}>
              {t("apply.backHome")}
            </Link>
          </Button>
        </CardContent>
      </Card>
    </ApplyShell>
  );
}

export function ApplyUnreadablePage() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { slug } = useParams({ from: "/apply/$slug/unreadable" });
  const { ref } = useSearch({ from: "/apply/$slug/unreadable" });
  const [file, setFile] = useState<File | null>(null);
  const [error, setError] = useState<string | null>(null);

  const reupload = useMutation({
    mutationFn: async () => {
      if (!file || !ref) {
        throw new Error("missing");
      }
      return publicApi.reuploadPublicCv(ref, file);
    },
    onSuccess: async () => {
      await navigate({ to: "/apply/$slug/done", params: { slug }, search: { ref: ref! } });
    },
    onError: () => setError(t("apply.submitFailed"))
  });

  return (
    <ApplyShell>
      <Card>
        <CardHeader>
          <CardTitle>{t("apply.unreadableTitle")}</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-4 text-sm leading-6">
          <p>{t("apply.unreadableBody")}</p>
          <p className="text-muted">{t("apply.unreadableHint")}</p>
          <input
            type="file"
            accept=".pdf,.docx,.txt"
            onChange={(e) => setFile(e.target.files?.[0] ?? null)}
          />
          {error ? <p className="text-danger">{error}</p> : null}
          <Button type="button" disabled={!file || reupload.isPending} onClick={() => reupload.mutate()}>
            {t("apply.reupload")}
          </Button>
        </CardContent>
      </Card>
    </ApplyShell>
  );
}
