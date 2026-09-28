import { useState } from "react";
import { AnimatePresence, motion } from "motion/react";
import { toast } from "sonner";
import { BellRing, X } from "lucide-react";

import usePushNotifications from "@/hooks/usePushNotifications";
import { useAuth } from "@/contexts/AuthContext";
import { Button } from "@/components/ui/button";
import { Spinner } from "@/components/ui/spinner";

const DISMISS_KEY = "movieshop-push-prompt-dismissed";

/**
 * Főoldali felkérés push értesítésekre. Csak akkor jelenik meg, ha a felhasználó be van jelentkezve,
 * a böngésző támogatja, a Service Worker fut, még nem döntött az engedélyről, és nem zárta be korábban.
 */
export default function PushPrompt({ className }) {
    const { user } = useAuth();
    const push = usePushNotifications();
    const [dismissed, setDismissed] = useState(() => {
        try {
            return localStorage.getItem(DISMISS_KEY) === "1";
        } catch {
            return false;
        }
    });

    const visible =
        Boolean(user) && push.supported && push.swReady && !push.needsInstallFirst &&
        push.permission === "default" && !push.subscribed && !dismissed;

    const dismiss = () => {
        setDismissed(true);
        try {
            localStorage.setItem(DISMISS_KEY, "1");
        } catch {
            /* privát mód */
        }
    };

    const enable = async () => {
        const { ok, error } = await push.subscribe();
        if (ok) toast.success("Notifications enabled", { description: "We'll let you know when a new movie arrives." });
        else toast.error("Could not enable notifications", { description: error });
    };

    return (
        <AnimatePresence>
            {visible && (
                <motion.div
                    initial={{ opacity: 0, y: -12, height: 0 }}
                    animate={{ opacity: 1, y: 0, height: "auto" }}
                    exit={{ opacity: 0, y: -12, height: 0 }}
                    transition={{ duration: 0.35, ease: [0.22, 1, 0.36, 1] }}
                    className={className}
                >
                    <div className="relative flex flex-col gap-3 overflow-hidden rounded-xl border bg-card p-4 sm:flex-row sm:items-center">
                        <div className="pointer-events-none absolute -top-10 -left-10 size-32 rounded-full bg-primary/20 blur-2xl" aria-hidden="true" />
                        <motion.span
                            className="relative flex size-11 shrink-0 items-center justify-center rounded-full bg-primary text-primary-foreground shadow-lg shadow-primary/30"
                            animate={{ rotate: [0, -16, 14, -10, 8, 0] }}
                            transition={{ duration: 1, repeat: Infinity, repeatDelay: 3 }}
                        >
                            <BellRing className="size-5" />
                        </motion.span>
                        <div className="relative flex-1">
                            <p className="font-semibold">Never miss a premiere</p>
                            <p className="text-sm text-muted-foreground">Get a notification on this device as soon as a new movie arrives.</p>
                        </div>
                        <div className="relative flex gap-2">
                            <Button size="sm" onClick={enable} disabled={push.busy}>
                                {push.busy && <Spinner />}
                                Enable
                            </Button>
                            <Button size="sm" variant="ghost" onClick={dismiss}>
                                Not now
                            </Button>
                        </div>
                        <button
                            type="button"
                            onClick={dismiss}
                            className="absolute top-2 right-2 cursor-pointer rounded p-1 text-muted-foreground opacity-70 hover:opacity-100 sm:hidden"
                            aria-label="Dismiss"
                        >
                            <X className="size-4" />
                        </button>
                    </div>
                </motion.div>
            )}
        </AnimatePresence>
    );
}
