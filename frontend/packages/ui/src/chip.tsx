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
        "inline-flex items-center gap-2 rounded-full border px-4 py-2 text-sm font-semibold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus",
        selected
          ? "border-brand-6 bg-brand-6 text-white"
          : "border-border bg-white text-foreground hover:border-brand-4 hover:bg-brand-0",
        className
      )}
      aria-pressed={selected}
      {...props}
    >
      <span aria-hidden="true" className="text-xs font-bold">
        {selected ? "✓" : "+"}
      </span>
      {children}
    </button>
  );
}
