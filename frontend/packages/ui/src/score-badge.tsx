import { scoreBand } from "@hirelens/domain";
import { cn } from "./cn";

const bandClass: Record<string, string> = {
  strong: "bg-score-strong text-score-strong-fg",
  solid: "bg-score-solid text-score-solid-fg",
  partial: "bg-score-partial text-score-partial-fg",
  limited: "bg-score-limited text-score-limited-fg",
  unknown: "bg-score-limited text-score-limited-fg"
};

export function ScoreBadge({
  score,
  label
}: {
  score: number | null;
  label: string;
}) {
  const band = scoreBand(score);
  return (
    <span
      className={cn("inline-flex items-center gap-2 rounded-sm px-2 py-0.5 text-xs font-medium", bandClass[band])}
    >
      <span aria-hidden="true">{score === null ? "—" : String(score)}</span>
      <span>{label}</span>
    </span>
  );
}
