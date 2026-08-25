import type { ButtonHTMLAttributes, ReactNode } from "react";
import { cn } from "./cn";

export function Chip({
  selected = false,
  children,
  className,
  ...props
}: ButtonHTMLAttributes<HTMLButtonElement> & {
  selected?: boolean;
  children: ReactNode;
}) {
  return (
    <button
      type="button"
      className={cn(
        "inline-flex items-center gap-2 rounded-full border px-4 py-2 text-sm font-semibold transition-all duration-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus",
        selected
          ? "border-brand-6 bg-brand-6 text-white shadow-sm"
          : "border-foreground/15 bg-white text-foreground hover:border-brand-5 hover:bg-brand-1",
        className
      )}
      aria-pressed={selected}
      {...props}
    >
      <span
        aria-hidden="true"
        className={cn(
          "inline-flex size-4 items-center justify-center rounded-[0.3rem] text-[0.65rem] font-bold leading-none",
          selected ? "bg-white/20 text-white" : "text-foreground/50"
        )}
      >
        {selected ? "✓" : "+"}
      </span>
      {children}
    </button>
  );
}
