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
        "inline-flex items-center rounded-md border px-3 py-1.5 text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-focus",
        selected
          ? "border-brand-8 bg-brand-8 text-brand-0"
          : "border-border bg-surface text-foreground hover:border-brand-5 hover:bg-brand-1",
        className
      )}
      aria-pressed={selected}
      {...props}
    >
      {children}
    </button>
  );
}
