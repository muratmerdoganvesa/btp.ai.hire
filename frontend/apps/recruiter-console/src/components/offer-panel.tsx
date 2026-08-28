import type { Offer } from "@hirelens/api-client";
import { Button, Card, CardContent, CardHeader, CardTitle, cn } from "@hirelens/ui";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "../api";

const statusTone: Record<string, string> = {
  draft: "bg-slate-100 text-slate-800",
  sent: "bg-sky-100 text-sky-800",
  accepted: "bg-emerald-100 text-emerald-800",
  declined: "bg-rose-100 text-rose-800",
  withdrawn: "bg-orange-100 text-orange-900"
};

export function OfferPanel({ candidateId }: { candidateId: string }) {
  const { t } = useTranslation();
  const queryClient = useQueryClient();
  const [packageText, setPackageText] = useState("");
  const [note, setNote] = useState("");
  const [error, setError] = useState<string | null>(null);

  const offers = useQuery({
    queryKey: ["offers", candidateId],
    queryFn: () => api.listCandidateOffers(candidateId)
  });

  const invalidate = async () => {
    await queryClient.invalidateQueries({ queryKey: ["offers", candidateId] });
    await queryClient.invalidateQueries({ queryKey: ["offers"] });
    await queryClient.invalidateQueries({ queryKey: ["candidates-board"] });
  };

  const onError = (err: unknown) => {
    const message = err instanceof Error ? err.message : "";
    setError(
      message
        ? `${t("errors.generic")} (${message.replace(/^http_\d+:/, "").replace(/^validation:/, "").replace(/^conflict:/, "")})`
        : t("errors.generic")
    );
  };

  const create = useMutation({
    mutationFn: () => api.createOffer(candidateId, { packageText: packageText.trim(), note: note.trim() || null }),
    onSuccess: async () => {
      setPackageText("");
      setNote("");
      setError(null);
      await invalidate();
    },
    onError
  });

  const send = useMutation({
    mutationFn: (id: string) => api.sendOffer(id),
    onSuccess: async () => {
      setError(null);
      await invalidate();
    },
    onError
  });
  const accept = useMutation({
    mutationFn: (id: string) => api.acceptOffer(id),
    onSuccess: async () => {
      setError(null);
      await invalidate();
    },
    onError
  });
  const decline = useMutation({
    mutationFn: (id: string) => api.declineOffer(id),
    onSuccess: async () => {
      setError(null);
      await invalidate();
    },
    onError
  });
  const withdraw = useMutation({
    mutationFn: (id: string) => api.withdrawOffer(id),
    onSuccess: async () => {
      setError(null);
      await invalidate();
    },
    onError
  });

  const rows = offers.data ?? [];
  const hasOpen = rows.some((row) => row.status === "draft" || row.status === "sent");
  const busy =
    create.isPending || send.isPending || accept.isPending || decline.isPending || withdraw.isPending;

  return (
    <Card className="border-border/80">
      <CardHeader className="pb-2">
        <CardTitle className="text-base font-extrabold tracking-tight">{t("offer.title")}</CardTitle>
        <p className="text-xs leading-relaxed text-muted">{t("offer.hint")}</p>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        {error ? (
          <p className="text-sm text-danger" role="alert">
            {error}
          </p>
        ) : null}

        {rows.length > 0 ? (
          <ul className="flex flex-col gap-3">
            {rows.map((row) => (
              <li key={row.id} className="rounded-xl border border-border bg-white px-3 py-3">
                <div className="flex items-center justify-between gap-2">
                  <span className={cn("inline-flex rounded-md px-2 py-1 text-[0.7rem] font-extrabold uppercase tracking-wide", statusTone[row.status] ?? "bg-slate-100")}>
                    {t(`offer.status.${row.status}`, { defaultValue: row.status })}
                  </span>
                  {row.scoreSnapshot != null ? (
                    <span className="text-xs font-bold tabular-nums text-muted">
                      {t("offer.scoreSnapshot", { score: row.scoreSnapshot })}
                    </span>
                  ) : null}
                </div>
                <p className="mt-2 whitespace-pre-wrap text-sm leading-6 text-foreground">{row.packageText}</p>
                {row.note ? <p className="mt-1 text-xs text-muted">{row.note}</p> : null}
                <OfferActions
                  row={row}
                  busy={busy}
                  onSend={() => send.mutate(row.id)}
                  onAccept={() => accept.mutate(row.id)}
                  onDecline={() => decline.mutate(row.id)}
                  onWithdraw={() => withdraw.mutate(row.id)}
                />
              </li>
            ))}
          </ul>
        ) : (
          <p className="text-sm text-muted">{t("offer.empty")}</p>
        )}

        {!hasOpen ? (
          <div className="flex flex-col gap-2">
            <label className="text-sm font-semibold">
              {t("offer.package")}
              <textarea
                className="mt-1.5 min-h-24 w-full rounded-xl border border-border bg-white px-3 py-3 text-sm font-medium outline-none placeholder:text-muted focus-visible:ring-2 focus-visible:ring-focus"
                value={packageText}
                onChange={(event) => setPackageText(event.target.value)}
                placeholder={t("offer.packagePlaceholder")}
              />
            </label>
            <label className="text-sm font-semibold">
              {t("offer.note")}
              <textarea
                className="mt-1.5 min-h-16 w-full rounded-xl border border-border bg-white px-3 py-3 text-sm font-medium outline-none placeholder:text-muted focus-visible:ring-2 focus-visible:ring-focus"
                value={note}
                onChange={(event) => setNote(event.target.value)}
                placeholder={t("offer.notePlaceholder")}
              />
            </label>
            <Button
              type="button"
              className="w-full"
              disabled={busy || !packageText.trim()}
              onClick={() => create.mutate()}
            >
              {create.isPending ? t("offer.creating") : t("offer.create")}
            </Button>
          </div>
        ) : null}
      </CardContent>
    </Card>
  );
}

function OfferActions({
  row,
  busy,
  onSend,
  onAccept,
  onDecline,
  onWithdraw
}: {
  row: Offer;
  busy: boolean;
  onSend: () => void;
  onAccept: () => void;
  onDecline: () => void;
  onWithdraw: () => void;
}) {
  const { t } = useTranslation();
  if (row.status === "draft") {
    return (
      <div className="mt-3 flex flex-wrap gap-2">
        <Button type="button" className="h-8 px-3 text-xs" disabled={busy} onClick={onSend}>
          {t("offer.send")}
        </Button>
        <Button type="button" variant="outline" className="h-8 px-3 text-xs" disabled={busy} onClick={onWithdraw}>
          {t("offer.withdraw")}
        </Button>
      </div>
    );
  }
  if (row.status === "sent") {
    return (
      <div className="mt-3 flex flex-wrap gap-2">
        <Button type="button" className="h-8 px-3 text-xs" disabled={busy} onClick={onAccept}>
          {t("offer.accept")}
        </Button>
        <Button type="button" variant="outline" className="h-8 px-3 text-xs" disabled={busy} onClick={onDecline}>
          {t("offer.decline")}
        </Button>
        <Button type="button" variant="outline" className="h-8 px-3 text-xs" disabled={busy} onClick={onWithdraw}>
          {t("offer.withdraw")}
        </Button>
      </div>
    );
  }
  return null;
}
