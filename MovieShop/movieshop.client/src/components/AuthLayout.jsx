import { motion } from "motion/react";
import { Clapperboard } from "lucide-react";

import { cn } from "@/lib/utils";

/**
 * Bejelentkezés / regisztráció / 2FA oldalak közös kerete:
 * lassan hullámzó „aurora" háttér és rugalmasan beúszó üveghatású kártya.
 */
const AuthLayout = ({ icon: Icon = Clapperboard, title, description, children, className }) => (
    <div className="relative isolate flex flex-1 items-center justify-center overflow-hidden px-4 py-12">
        <div className="pointer-events-none absolute inset-0 -z-10" aria-hidden="true">
            <div className="absolute top-1/4 left-1/4 size-[32rem] animate-aurora rounded-full bg-primary/20 blur-3xl" />
            <div className="absolute right-1/4 bottom-1/4 size-[28rem] animate-aurora rounded-full bg-rose-500/10 blur-3xl [animation-delay:-7s]" />
            <div className="absolute top-1/2 left-1/2 size-[20rem] animate-aurora rounded-full bg-sky-500/10 blur-3xl [animation-delay:-13s]" />
        </div>
        <motion.div
            initial={{ opacity: 0, y: 30, scale: 0.97 }}
            animate={{ opacity: 1, y: 0, scale: 1 }}
            transition={{ type: "spring", stiffness: 120, damping: 18 }}
            className={cn("relative w-full max-w-md", className)}
        >
            <div className="mb-8 flex flex-col items-center text-center">
                <motion.div
                    initial={{ rotate: -25, scale: 0.5 }}
                    animate={{ rotate: 0, scale: 1 }}
                    transition={{ type: "spring", stiffness: 200, damping: 12, delay: 0.1 }}
                    className="mb-4 flex size-12 items-center justify-center rounded-xl bg-primary text-primary-foreground shadow-lg shadow-primary/30"
                >
                    <Icon className="size-6" />
                </motion.div>
                <h1 className="text-3xl font-bold tracking-tight">{title}</h1>
                {description && <p className="mt-2 text-muted-foreground">{description}</p>}
            </div>
            <div className="rounded-2xl border bg-card/70 p-6 shadow-2xl backdrop-blur-xl sm:p-8">{children}</div>
        </motion.div>
    </div>
);

export default AuthLayout;
