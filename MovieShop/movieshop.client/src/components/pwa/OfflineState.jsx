import { RefreshCw, WifiOff } from "lucide-react";

import { Button } from "@/components/ui/button";
import { EmptyState } from "@/components/ui/page";

/**
 * Offline tartalékállapot azokhoz a tartalmakhoz, amelyek csak a szerverről tölthetők be
 * (a Service Worker az API-választ szándékosan nem cache-eli).
 */
export default function OfflineState({ className, title = "You are offline" }) {
    return (
        <EmptyState
            icon={WifiOff}
            title={title}
            description="This content needs an internet connection. Reconnect and try again — the app shell and cached images keep working meanwhile."
            className={className}
        >
            <Button variant="outline" onClick={() => window.location.reload()}>
                <RefreshCw />
                Try again
            </Button>
        </EmptyState>
    );
}
