import { AnimatePresence, motion } from "motion/react";
import { WifiOff } from "lucide-react";

import useOnlineStatus from "@/hooks/useOnlineStatus";

/** A navigációs sáv alatt megjelenő figyelmeztetés, amíg nincs hálózati kapcsolat. */
export default function OfflineBanner() {
    const online = useOnlineStatus();

    return (
        <AnimatePresence>
            {!online && (
                <motion.div
                    role="status"
                    initial={{ height: 0, opacity: 0 }}
                    animate={{ height: "auto", opacity: 1 }}
                    exit={{ height: 0, opacity: 0 }}
                    className="sticky top-16 z-30 overflow-hidden border-b border-primary/30 bg-primary/15 backdrop-blur"
                >
                    <div className="mx-auto flex max-w-7xl items-center justify-center gap-2 px-4 py-2 text-center text-sm">
                        <WifiOff className="size-4 shrink-0 text-primary" />
                        <span>
                            <strong>You are offline.</strong>{" "}
                            <span className="text-muted-foreground">Cached pages and images still work; purchases, streaming and live features need a connection.</span>
                        </span>
                    </div>
                </motion.div>
            )}
        </AnimatePresence>
    );
}
