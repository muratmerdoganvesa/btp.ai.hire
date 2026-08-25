import { cva, type VariantProps } from "class-variance-authority";
import type { HTMLAttributes } from "react";
import { cn } from "./cn";

const badgeVariants = cva(
  "inline-flex items-center rounded-md px-2 py-0.5 text-[0.7rem] font-semibold uppercase tracking-[0.06em]",
  {
    variants: {
      tone: {
        default: "bg-brand-2 text-brand-9",
        muted: "bg-brand-1 text-muted",
        danger: "bg-danger-bg text-danger"
      }
    },
    defaultVariants: { tone: "default" }
  }
);

export type BadgeProps = HTMLAttributes<HTMLSpanElement> & VariantProps<typeof badgeVariants>;

export function Badge({ className, tone, ...props }: BadgeProps) {
  return <span className={cn(badgeVariants({ tone }), className)} {...props} />;
}
