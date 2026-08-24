import { cva, type VariantProps } from "class-variance-authority";
import type { HTMLAttributes } from "react";
import { cn } from "./cn";

const badgeVariants = cva("inline-flex items-center rounded-pill px-3 py-1 text-xs font-medium", {
  variants: {
    tone: {
      default: "bg-brand-2 text-foreground",
      muted: "bg-score-limited text-score-limited-fg",
      danger: "bg-danger-bg text-danger"
    }
  },
  defaultVariants: { tone: "default" }
});

export type BadgeProps = HTMLAttributes<HTMLSpanElement> & VariantProps<typeof badgeVariants>;

export function Badge({ className, tone, ...props }: BadgeProps) {
  return <span className={cn(badgeVariants({ tone }), className)} {...props} />;
}
