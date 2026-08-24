import { Button, Card, CardContent, CardHeader, CardTitle } from "@hirelens/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useParams } from "@tanstack/react-router";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "../api";
import { AppShell } from "../components/app-shell";
import { CandidateCard } from "../components/candidate-card";
import { CvUploadZone } from "../components/cv-upload-zone";
import { Field, TextInput } from "../components/field";

export function CandidatesPage() {
  const { t } = useTranslation();
  const { positionId } = useParams({ from: "/positions/$positionId" });
  const queryClient = useQueryClient();
  const [displayName, setDisplayName] = useState("");
  const [selectedCandidateId, setSelectedCandidateId] = useState<string | null>(null);

  const position = useQuery({
    queryKey: ["position", positionId],
    queryFn: () => api.getPosition(positionId)
  });
  const candidates = useQuery({
    queryKey: ["candidates", positionId],
    queryFn: () => api.listCandidates(positionId)
  });

  const create = useMutation({
    mutationFn: () => api.createCandidate(positionId, displayName),
    onSuccess: async (candidate) => {
      setDisplayName("");
      setSelectedCandidateId(candidate.id);
      await queryClient.invalidateQueries({ queryKey: ["candidates", positionId] });
    }
  });

  const list = candidates.data ?? [];

  return (
    <AppShell>
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p className="text-sm text-brand">{t("candidates.title")}</p>
          <h1 className="text-3xl font-semibold tracking-tight">{position.data?.title ?? t("candidates.title")}</h1>
          <p className="mt-2 max-w-2xl text-sm text-muted">{position.data?.jobDescription}</p>
        </div>
        <p className="text-sm text-muted">
          {list.length} {t("candidates.count")}
        </p>
      </div>
      <div className="grid gap-6 lg:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>{t("candidates.create")}</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col gap-4">
            <Field label={t("candidates.displayName")}>
              <TextInput value={displayName} onChange={(event) => setDisplayName(event.target.value)} />
            </Field>
            <Button type="button" disabled={!displayName || create.isPending} onClick={() => create.mutate()}>
              {t("candidates.create")}
            </Button>
          </CardContent>
        </Card>
        {selectedCandidateId ? (
          <CvUploadZone
            positionId={positionId}
            candidateId={selectedCandidateId}
            onCompleted={() => void queryClient.invalidateQueries({ queryKey: ["candidates", positionId] })}
          />
        ) : (
          <div className="flex items-center rounded-2xl border border-dashed border-border bg-brand-1/40 px-6 py-10 text-sm text-muted shadow-card">
            {t("candidates.selectHint")}
          </div>
        )}
      </div>
      <div className="flex flex-col gap-3">
        {list.length === 0 ? (
          <div className="rounded-lg border border-dashed border-border px-6 py-12 text-center text-sm text-muted">
            {t("candidates.empty")}
          </div>
        ) : (
          list.map((candidate) => (
            <div key={candidate.id} onClick={() => setSelectedCandidateId(candidate.id)}>
              <CandidateCard candidate={candidate} selected={selectedCandidateId === candidate.id} />
            </div>
          ))
        )}
      </div>
    </AppShell>
  );
}
