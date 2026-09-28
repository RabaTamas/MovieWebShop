import { LayoutGroup, motion } from "motion/react";
import { ChevronLeft, ChevronRight, MoreHorizontal } from "lucide-react";

import { cn } from "@/lib/utils";

/** Oldalszámok kihagyással: 1 … 4 5 6 … 12 (legfeljebb 7 elem). */
function getPageItems(current, total) {
    if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1);

    const items = [1];
    const start = Math.max(2, current - 1);
    const end = Math.min(total - 1, current + 1);

    if (start > 2) items.push("start-ellipsis");
    for (let page = start; page <= end; page++) items.push(page);
    if (end < total - 1) items.push("end-ellipsis");
    items.push(total);

    return items;
}

/**
 * Lapozó: az aktív oldal kijelölése rugós animációval csúszik a gombok között.
 * totalItems + pageSize megadásakor „Showing 9–16 of 24" feliratot is mutat.
 */
const Pagination = ({ currentPage, totalPages, onPageChange, totalItems, pageSize }) => {
    if (totalPages === 0) return null;

    const items = getPageItems(currentPage, totalPages);
    const from = totalItems ? (currentPage - 1) * pageSize + 1 : null;
    const to = totalItems ? Math.min(currentPage * pageSize, totalItems) : null;

    const arrowClass =
        "flex size-9 cursor-pointer items-center justify-center rounded-full text-muted-foreground transition-colors hover:bg-accent hover:text-foreground disabled:pointer-events-none disabled:opacity-30";

    return (
        <nav className="mt-12 flex flex-col items-center gap-3" aria-label="Pagination">
            <div className="flex items-center gap-1 rounded-full border bg-card/60 p-1 shadow-lg backdrop-blur">
                <button
                    type="button"
                    className={arrowClass}
                    disabled={currentPage === 1}
                    onClick={() => onPageChange(currentPage - 1)}
                    aria-label="Previous page"
                >
                    <ChevronLeft className="size-4" />
                </button>

                <LayoutGroup id="pagination">
                    {items.map((item) =>
                        typeof item === "number" ? (
                            <button
                                key={item}
                                type="button"
                                onClick={() => onPageChange(item)}
                                aria-current={item === currentPage ? "page" : undefined}
                                className={cn(
                                    "relative flex size-9 cursor-pointer items-center justify-center rounded-full text-sm font-medium tabular-nums transition-colors",
                                    item === currentPage ? "text-primary-foreground" : "text-muted-foreground hover:text-foreground"
                                )}
                            >
                                {item === currentPage && (
                                    <motion.span
                                        layoutId="pagination-active"
                                        className="absolute inset-0 rounded-full bg-primary shadow-md shadow-primary/40"
                                        transition={{ type: "spring", stiffness: 420, damping: 32 }}
                                    />
                                )}
                                <span className="relative">{item}</span>
                            </button>
                        ) : (
                            <span key={item} className="flex size-9 items-center justify-center text-muted-foreground" aria-hidden="true">
                                <MoreHorizontal className="size-4" />
                            </span>
                        )
                    )}
                </LayoutGroup>

                <button
                    type="button"
                    className={arrowClass}
                    disabled={currentPage === totalPages}
                    onClick={() => onPageChange(currentPage + 1)}
                    aria-label="Next page"
                >
                    <ChevronRight className="size-4" />
                </button>
            </div>

            {totalItems ? (
                <p className="text-xs text-muted-foreground tabular-nums">
                    Showing <span className="font-medium text-foreground">{from}–{to}</span> of {totalItems}
                    {" · "}Page {currentPage} of {totalPages}
                </p>
            ) : null}
        </nav>
    );
};

export default Pagination;
