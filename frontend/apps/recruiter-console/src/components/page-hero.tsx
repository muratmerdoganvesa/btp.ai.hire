import { cn } from "@hirelens/ui";
import type { CSSProperties, ReactNode } from "react";

/** Shared height so sidebar brand + page hero read as one continuous top bar. */
export const BRAND_BAR_CLASS = "h-[5.75rem]";

/** Same paint as HireLens sidebar brand (inline so it never drops out of the cascade). */
export const brandSurfaceStyle: CSSProperties = {
  background:
    "radial-gradient(circle at 92% -10%, rgb(255 255 255 / 0.14), transparent 42%), radial-gradient(circle at 12% 120%, rgb(61 82 224 / 0.32), transparent 48%), linear-gradient(145deg, #151f66 0%, #1c2a8a 48%, #2436b0 100%)",
  color: "#ffffff"
};

/** Compact page header — same height/paint as HireLens brand (no multi-line body text). */
export function PageHero({
  kicker,
  title,
  actions,
  className
}: {
  kicker?: string;
  title: string;
  /** @deprecated Kept for call-site compatibility; rendered in PageBody instead for bar height. */
  description?: string;
  actions?: ReactNode;
  className?: string;
}) {
  return (
    <header
      style={brandSurfaceStyle}
      className={cn(
        "relative z-20 flex shrink-0 items-center overflow-hidden px-5 sm:px-6 lg:px-7",
        BRAND_BAR_CLASS,
        className
      )}
    >
      <div className="pointer-events-none absolute -right-8 -top-10 size-32 rounded-full bg-white/10" />
      <div className="pointer-events-none absolute -bottom-12 left-8 size-28 rounded-full bg-[#3d52e0]/28" />

      <div className="relative flex w-full items-center justify-between gap-4">
        <div className="min-w-0">
          {kicker ? (
            <p className="truncate text-[0.65rem] font-semibold uppercase tracking-[0.16em] text-white/70">
              {kicker}
            </p>
          ) : null}
          <h1 className="truncate text-xl font-extrabold tracking-tight text-white sm:text-2xl">{title}</h1>
        </div>
        {actions ? <div className="flex shrink-0 flex-wrap items-center gap-2">{actions}</div> : null}
      </div>
    </header>
  );
}

/** Scrollable page body under PageHero. */
export function PageBody({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <div
      className={cn(
        "flex min-h-0 flex-1 flex-col gap-5 overflow-y-auto px-4 py-4 sm:px-5 sm:py-5 lg:px-7",
        className
      )}
    >
      {children}
    </div>
  );
}
