import { Button, cn } from "@hirelens/ui";
import { useTranslation } from "react-i18next";

type PaginationProps = {
  page: number;
  pageCount: number;
  totalItems: number;
  pageSize: number;
  onPageChange: (page: number) => void;
};

function pageNumbers(current: number, total: number): (number | "ellipsis")[] {
  if (total <= 7) {
    return Array.from({ length: total }, (_, i) => i + 1);
  }

  const pages = new Set<number>([1, total, current, current - 1, current + 1]);
  const sorted = [...pages].filter((p) => p >= 1 && p <= total).sort((a, b) => a - b);
  const result: (number | "ellipsis")[] = [];

  for (let i = 0; i < sorted.length; i++) {
    const value = sorted[i];
    const prev = sorted[i - 1];
    if (i > 0 && prev !== undefined && value - prev > 1) {
      result.push("ellipsis");
    }
    result.push(value);
  }

  return result;
}

export function Pagination({ page, pageCount, totalItems, pageSize, onPageChange }: PaginationProps) {
  const { t } = useTranslation();

  if (pageCount <= 1) {
    return null;
  }

  const from = (page - 1) * pageSize + 1;
  const to = Math.min(page * pageSize, totalItems);
  const numbers = pageNumbers(page, pageCount);

  return (
    <nav
      className="flex flex-col gap-3 border-t border-border pt-4 sm:flex-row sm:items-center sm:justify-between"
      aria-label={t("pagination.label")}
    >
      <p className="text-sm text-muted">
        {t("pagination.range", { from, to, total: totalItems })}
      </p>
      <div className="flex flex-wrap items-center gap-1">
        <Button
          type="button"
          variant="outline"
          size="sm"
          disabled={page <= 1}
          onClick={() => onPageChange(page - 1)}
          aria-label={t("pagination.prev")}
        >
          {t("pagination.prev")}
        </Button>
        {numbers.map((item, index) =>
          item === "ellipsis" ? (
            <span key={`ellipsis-${index}`} className="px-2 text-sm text-muted" aria-hidden="true">
              …
            </span>
          ) : (
            <button
              key={item}
              type="button"
              aria-label={t("pagination.page", { page: item })}
              aria-current={item === page ? "page" : undefined}
              className={cn(
                "inline-flex size-9 items-center justify-center rounded-lg text-sm font-semibold transition-colors",
                item === page
                  ? "bg-brand-6 text-white shadow-sm"
                  : "text-foreground hover:bg-brand-0"
              )}
              onClick={() => onPageChange(item)}
            >
              {item}
            </button>
          )
        )}
        <Button
          type="button"
          variant="outline"
          size="sm"
          disabled={page >= pageCount}
          onClick={() => onPageChange(page + 1)}
          aria-label={t("pagination.next")}
        >
          {t("pagination.next")}
        </Button>
      </div>
    </nav>
  );
}
