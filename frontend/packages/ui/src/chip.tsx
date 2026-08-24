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
        "inline-flex items-center gap-2 rounded-pill border px-4 py-2 text-sm transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus",
        selected
          ? "border-brand bg-brand text-brand-fg"
          : "border-foreground/20 bg-surface text-foreground hover:border-brand hover:bg-brand-1",
        className
      )}
      aria-pressed={selected}
      {...props}
    >
      <span aria-hidden="true" className="text-xs font-semibold">
        {selected ? "✓" : "+"}
      </span>
      {children}
    </button>
  );
}
