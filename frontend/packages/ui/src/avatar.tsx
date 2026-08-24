import { initialsFromName } from "@hirelens/domain";
import { cn } from "./cn";

export function InitialsAvatar({ name, className }: { name: string; className?: string }) {
  return (
    <span
      aria-hidden="true"
      className={cn(
        "inline-flex size-8 items-center justify-center rounded-full bg-brand text-xs font-semibold text-brand-fg",
        className
      )}
    >
      {initialsFromName(name)}
    </span>
  );
}
