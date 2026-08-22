import type { Evidence } from "@hirelens/api-client";
import { Badge } from "@hirelens/ui";

export function EvidenceChip({
  evidence,
  onSelect
}: {
  evidence: Evidence;
  onSelect?: (evidence: Evidence) => void;
}) {
  return (
    <button type="button" onClick={() => onSelect?.(evidence)} className="text-left">
      <Badge>
        <span className="font-medium">{evidence.source}</span>
        <span className="ml-2 max-w-xs truncate">{evidence.quote}</span>
      </Badge>
    </button>
  );
}
