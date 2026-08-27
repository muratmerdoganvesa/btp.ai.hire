import { cn } from "@hirelens/ui";
import type { ReactNode } from "react";

/** Corporate page header — same dark brand surface as the HireLens sidebar logo. */
export function PageHero({
  kicker,
  title,
  description,
  actions,
  className
}: {
  kicker?: string;
  title: string;
  description?: string;
  actions?: ReactNode;
  className?: string;
}) {
  return (
    <header
      className={cn(
        "hl-brand-surface relative shrink-0 overflow-hidden px-5 py-5 sm:px-6 sm:py-6",
        /* Bleed into AppShell main padding so the band sits flush at the top edge. */
        "-mx-4 -mt-4 rounded-none border-b border-white/10 sm:-mx-5 sm:-mt-5 lg:-mx-7",
        className
      )}
    >
      <div className="pointer-events-none absolute -right-10 -top-12 size-40 rounded-full bg-white/10" />
      <div className="pointer-events-none absolute -bottom-14 left-10 size-36 rounded-full bg-[#3d52e0]/30" />
      <div className="pointer-events-none absolute right-24 top-1/2 size-20 -translate-y-1/2 rounded-full bg-white/5" />

      <div className="relative flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
        <div className="min-w-0">
          {kicker ? (
            <p className="text-[0.65rem] font-semibold uppercase tracking-[0.16em] text-white/70">{kicker}</p>
          ) : null}
          <h1 className="mt-1 text-2xl font-extrabold tracking-tight text-white sm:text-[1.75rem]">{title}</h1>
          {description ? (
            <p className="mt-1.5 max-w-2xl text-sm leading-relaxed text-white/80">{description}</p>
          ) : null}
        </div>
        {actions ? <div className="flex flex-wrap items-center gap-2">{actions}</div> : null}
      </div>
    </header>
  );
}
