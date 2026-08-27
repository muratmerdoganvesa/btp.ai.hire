import { cn } from "@hirelens/ui";
import { useEffect, useId, useRef, useState } from "react";
import { useTranslation } from "react-i18next";

export function ScoringGlossary({ className }: { className?: string }) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const panelId = useId();
  const rootRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) {
      return;
    }

    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setOpen(false);
      }
    };
    const onPointer = (event: PointerEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener("keydown", onKey);
    document.addEventListener("pointerdown", onPointer);
    return () => {
      document.removeEventListener("keydown", onKey);
      document.removeEventListener("pointerdown", onPointer);
    };
  }, [open]);

  return (
    <div ref={rootRef} className={cn("mt-1", className)}>
      <div className="flex items-start gap-1.5">
        <p className="text-xs font-semibold text-brand-7">{t("candidates.rankingRule")}</p>
        <button
          type="button"
          className="mt-px inline-flex size-5 shrink-0 items-center justify-center rounded-full text-brand-7 hover:bg-brand-0 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus"
          aria-expanded={open}
          aria-controls={panelId}
          aria-label={t("score.glossaryAria")}
          onClick={(event) => {
            event.stopPropagation();
            setOpen((value) => !value);
          }}
        >
          <InfoIcon />
        </button>
      </div>
      {open ? (
        <div
          id={panelId}
          role="region"
          aria-label={t("score.glossaryTitle")}
          className="mt-2 rounded-xl border border-brand-2 bg-brand-0/60 px-3.5 py-3 text-left"
        >
          <p className="text-xs font-extrabold tracking-tight text-foreground">{t("score.glossaryTitle")}</p>
          <dl className="mt-2 flex flex-col gap-2.5">
            <GlossaryItem title={t("score.glossaryScoreTitle")} body={t("score.glossaryScoreBody")} />
            <GlossaryItem title={t("score.glossaryCoverageTitle")} body={t("score.glossaryCoverageBody")} />
            <GlossaryItem title={t("score.glossaryHowTitle")} body={t("score.glossaryHowBody")} />
          </dl>
        </div>
      ) : null}
    </div>
  );
}

function GlossaryItem({ title, body }: { title: string; body: string }) {
  return (
    <div>
      <dt className="text-[0.7rem] font-extrabold uppercase tracking-wide text-brand-7">{title}</dt>
      <dd className="mt-0.5 text-xs leading-5 text-foreground/90">{body}</dd>
    </div>
  );
}

function InfoIcon() {
  return (
    <svg viewBox="0 0 20 20" className="size-4" aria-hidden="true">
      <circle cx="10" cy="10" r="8.25" fill="none" stroke="currentColor" strokeWidth="1.6" />
      <circle cx="10" cy="6.4" r="1.05" fill="currentColor" />
      <path
        d="M10 9.1v5.2"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.7"
        strokeLinecap="round"
      />
    </svg>
  );
}
