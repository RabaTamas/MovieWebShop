import { useEffect, useState } from "react";

import { cn } from "@/lib/utils";

/**
 * Visszaszámláló egy aukció lejártáig. Az utolsó percben piros, pulzáló.
 * onExpire: opcionális callback, ha lejárt az idő (pl. adatok újratöltése).
 */
export default function AuctionCountdown({ endsAt, onExpire, className, highlightUrgent = true }) {
    const [remaining, setRemaining] = useState('');
    const [urgent, setUrgent] = useState(false);

    useEffect(() => {
        const tick = () => {
            const diff = new Date(endsAt) - Date.now();
            if (diff <= 0) {
                setRemaining('Ended');
                onExpire?.();
                return;
            }
            setUrgent(diff < 60000);
            const h = Math.floor(diff / 3600000);
            const m = Math.floor((diff % 3600000) / 60000);
            const s = Math.floor((diff % 60000) / 1000);
            setRemaining(h > 0 ? `${h}h ${m}m ${s}s` : `${m}m ${s}s`);
        };
        tick();
        const id = setInterval(tick, 1000);
        return () => clearInterval(id);
    }, [endsAt, onExpire]);

    return (
        <span className={cn("font-mono tabular-nums", highlightUrgent && urgent && "animate-pulse text-destructive", className)}>
            {remaining}
        </span>
    );
}
