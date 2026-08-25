import { cva, type VariantProps } from "class-variance-authority";
import type { HTMLAttributes } from "react";
import { cn } from "./cn";

const badgeVariants = cva(
  "inline-flex items-center rounded-full px-2.5 py-0.5 text-[0.7rem] font-bold tracking-wide",
  {
    variants: {
      tone: {
        default: "bg-brand-1 text-brand-7",
        muted: "bg-brand-1/80 text-muted",
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
