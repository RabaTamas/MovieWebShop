import { Link } from "react-router-dom";
import { motion } from "motion/react";
import { Film, Home } from "lucide-react";
import { Button } from "@/components/ui/button";

/** 404: régi mozivetítő hangulat — villódzó szám, fénykúp, filmszemcse, lebegő filmtekercs. */
const NotFound = () => {
    return (
        <div className="film-grain relative isolate flex flex-1 items-center justify-center overflow-hidden px-4 py-20">
            {/* Vetítő fénykúpja */}
            <div
                aria-hidden="true"
                className="absolute top-0 left-1/2 -z-10 h-[120%] w-[70rem] -translate-x-1/2 bg-[conic-gradient(from_180deg_at_50%_0%,transparent_38%,color-mix(in_oklch,var(--primary)_14%,transparent)_50%,transparent_62%)] blur-2xl"
            />
            {/* Porszemcsék a fényben */}
            {Array.from({ length: 14 }).map((_, i) => (
                <motion.span
                    key={i}
                    aria-hidden="true"
                    className="absolute size-1 rounded-full bg-primary/60"
                    style={{ left: `${35 + ((i * 37) % 30)}%`, top: `${10 + ((i * 53) % 70)}%` }}
                    animate={{ y: [0, -30, 0], opacity: [0, 0.9, 0] }}
                    transition={{ duration: 4 + (i % 5), repeat: Infinity, delay: i * 0.4, ease: "easeInOut" }}
                />
            ))}

            <div className="relative max-w-md text-center">
                <motion.div
                    initial={{ rotate: -90, opacity: 0 }}
                    animate={{ rotate: 0, opacity: 1 }}
                    transition={{ duration: 1, ease: [0.22, 1, 0.36, 1] }}
                    className="mx-auto mb-4 w-fit animate-float"
                >
                    <Film className="size-12 text-muted-foreground" />
                </motion.div>
                <motion.h1
                    initial={{ scale: 0.6, opacity: 0, filter: "blur(12px)" }}
                    animate={{ scale: 1, opacity: 1, filter: "blur(0px)" }}
                    transition={{ duration: 0.9, ease: [0.22, 1, 0.36, 1] }}
                    className="animate-flicker font-display text-[10rem] leading-none tracking-wider text-primary drop-shadow-[0_0_40px_color-mix(in_oklch,var(--primary)_60%,transparent)]"
                >
                    404
                </motion.h1>
                <motion.div
                    initial={{ opacity: 0, y: 16 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={{ duration: 0.6, delay: 0.5 }}
                >
                    <h2 className="mb-3 text-2xl font-bold">Page Not Found</h2>
                    <p className="mb-8 text-muted-foreground">
                        The page you're looking for doesn't exist or has been moved.
                    </p>
                    <Button size="lg" asChild>
                        <Link to="/">
                            <Home />
                            Go Home
                        </Link>
                    </Button>
                </motion.div>
            </div>
        </div>
    );
};

export default NotFound;
