export type ScoreBand = "strong" | "solid" | "partial" | "limited";

export function scoreBand(score: number | null): ScoreBand | "unknown" {
  if (score === null) {
    return "unknown";
  }

  if (score >= 85) {
    return "strong";
  }

  if (score >= 70) {
    return "solid";
  }

  if (score >= 55) {
    return "partial";
  }

  return "limited";
}

export function initialsFromName(displayName: string): string {
  const parts = displayName.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) {
    return "?";
  }

  const first = parts[0]?.[0] ?? "";
  const last = parts.length > 1 ? (parts[parts.length - 1]?.[0] ?? "") : "";
  return (first + last).toUpperCase();
}
