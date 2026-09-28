import { useCallback, useEffect, useState } from "react";
import { AnimatePresence, motion } from "motion/react";
import { ChevronLeft, ChevronRight, Expand, X } from "lucide-react";

import { cn } from "@/lib/utils";
import { Dialog, DialogContent, DialogDescription, DialogTitle } from "@/components/ui/dialog";
import { Stagger, StaggerItem } from "@/components/motion/Reveal";

// 8 kép pontosan kitölti a 4 oszlopos „bento" rácsot (1 nagy 2×2, 1 széles 2×1, 6 kicsi)
const GRID_LIMIT = 8;

/**
 * „Bento" elrendezésű képgaléria TMDB-háttérképekből, teljes képernyős nagyítóval.
 * A nagyítóban nyilakkal / billentyűzettel (←, →) lehet lapozni.
 */
export default function ImageGallery({ images, title }) {
    const [openIndex, setOpenIndex] = useState(null);
    const [direction, setDirection] = useState(1);

    const count = images?.length ?? 0;

    const go = useCallback(
        (delta) => {
            setDirection(delta);
            setOpenIndex((i) => (i === null ? i : (i + delta + count) % count));
        },
        [count]
    );

    useEffect(() => {
        if (openIndex === null) return;
        const onKey = (e) => {
            if (e.key === "ArrowRight") go(1);
            if (e.key === "ArrowLeft") go(-1);
        };
        window.addEventListener("keydown", onKey);
        return () => window.removeEventListener("keydown", onKey);
    }, [openIndex, go]);

    if (!count) return null;

    const visible = images.slice(0, GRID_LIMIT);
    const hiddenCount = count - visible.length;
    const current = openIndex !== null ? images[openIndex] : null;

    return (
        <>
            <Stagger className="grid auto-rows-[120px] grid-cols-2 gap-3 sm:auto-rows-[150px] md:grid-cols-4" stagger={0.06}>
                {visible.map((image, i) => {
                    const isLast = i === visible.length - 1 && hiddenCount > 0;
                    return (
                        <StaggerItem
                            key={image.url}
                            className={cn(
                                "relative",
                                i === 0 && "col-span-2 row-span-2",
                                i === 3 && "md:col-span-2",
                                // Mobilon (2 oszlop) az utolsó kép teljes szélességű, így nem marad üres cella
                                i === visible.length - 1 && visible.length % 2 === 0 && "col-span-2 md:col-span-1"
                            )}
                        >
                            <button
                                type="button"
                                onClick={() => { setDirection(1); setOpenIndex(i); }}
                                className="group relative size-full cursor-zoom-in overflow-hidden rounded-xl bg-muted ring-1 ring-border outline-none focus-visible:ring-[3px] focus-visible:ring-ring"
                                aria-label={`Open image ${i + 1} of ${count}`}
                            >
                                <img
                                    src={image.thumbnailUrl}
                                    alt={`${title} still ${i + 1}`}
                                    decoding="async"
                                    className="size-full object-cover transition-transform duration-700 ease-out group-hover:scale-110"
                                />
                                <div className="absolute inset-0 bg-gradient-to-t from-black/60 via-transparent to-transparent opacity-0 transition-opacity duration-300 group-hover:opacity-100" />
                                <Expand className="absolute right-3 bottom-3 size-5 text-white opacity-0 transition-all duration-300 group-hover:opacity-100" />
                                {isLast && (
                                    <div className="absolute inset-0 flex items-center justify-center bg-black/60 text-2xl font-bold text-white backdrop-blur-[2px]">
                                        +{hiddenCount + 1}
                                    </div>
                                )}
                            </button>
                        </StaggerItem>
                    );
                })}
            </Stagger>

            <Dialog open={openIndex !== null} onOpenChange={(open) => { if (!open) setOpenIndex(null); }}>
                <DialogContent
                    showCloseButton={false}
                    className="dark max-h-[95vh] w-[min(96vw,1400px)] max-w-none gap-0 overflow-hidden border-white/10 bg-black/95 p-0 text-white sm:max-w-none"
                >
                    <DialogTitle className="sr-only">{title} gallery</DialogTitle>
                    <DialogDescription className="sr-only">Use the arrow keys to browse images.</DialogDescription>

                    <div className="relative flex aspect-video w-full items-center justify-center overflow-hidden bg-black">
                        <AnimatePresence initial={false} custom={direction} mode="popLayout">
                            {current && (
                                <motion.img
                                    key={current.url}
                                    src={current.url}
                                    alt={`${title} still ${openIndex + 1}`}
                                    custom={direction}
                                    initial={{ opacity: 0, x: direction * 80, scale: 1.04 }}
                                    animate={{ opacity: 1, x: 0, scale: 1 }}
                                    exit={{ opacity: 0, x: direction * -80, scale: 0.98 }}
                                    transition={{ duration: 0.45, ease: [0.22, 1, 0.36, 1] }}
                                    className="absolute inset-0 size-full object-contain"
                                />
                            )}
                        </AnimatePresence>

                        <button
                            type="button"
                            onClick={() => go(-1)}
                            className="absolute left-3 z-10 flex size-11 cursor-pointer items-center justify-center rounded-full bg-black/50 text-white backdrop-blur transition hover:scale-110 hover:bg-primary hover:text-primary-foreground"
                            aria-label="Previous image"
                        >
                            <ChevronLeft className="size-6" />
                        </button>
                        <button
                            type="button"
                            onClick={() => go(1)}
                            className="absolute right-3 z-10 flex size-11 cursor-pointer items-center justify-center rounded-full bg-black/50 text-white backdrop-blur transition hover:scale-110 hover:bg-primary hover:text-primary-foreground"
                            aria-label="Next image"
                        >
                            <ChevronRight className="size-6" />
                        </button>
                        <button
                            type="button"
                            onClick={() => setOpenIndex(null)}
                            className="absolute top-3 right-3 z-10 flex size-10 cursor-pointer items-center justify-center rounded-full bg-black/50 text-white backdrop-blur transition hover:bg-white/20"
                            aria-label="Close gallery"
                        >
                            <X className="size-5" />
                        </button>
                        <span className="absolute top-4 left-4 z-10 rounded-full bg-black/50 px-3 py-1 font-mono text-xs backdrop-blur">
                            {(openIndex ?? 0) + 1} / {count}
                        </span>
                    </div>

                    {/* Bélyegképsáv */}
                    <div className="scrollbar-none flex gap-2 overflow-x-auto p-3">
                        {images.map((image, i) => (
                            <button
                                key={image.url}
                                type="button"
                                onClick={() => { setDirection(i > openIndex ? 1 : -1); setOpenIndex(i); }}
                                className={cn(
                                    "h-14 w-24 shrink-0 cursor-pointer overflow-hidden rounded-md ring-2 transition-all",
                                    i === openIndex ? "opacity-100 ring-primary" : "opacity-50 ring-transparent hover:opacity-90"
                                )}
                                aria-label={`Show image ${i + 1}`}
                            >
                                <img src={image.thumbnailUrl} alt="" loading="lazy" className="size-full object-cover" />
                            </button>
                        ))}
                    </div>
                </DialogContent>
            </Dialog>
        </>
    );
}
