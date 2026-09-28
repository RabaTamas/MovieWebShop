import { Loader2Icon } from "lucide-react";

import { cn } from "@/lib/utils";

function Spinner({ className, ...props }) {
    return (
        <Loader2Icon
            role="status"
            aria-label="Loading"
            className={cn("size-4 animate-spin", className)}
            {...props}
        />
    );
}

/** Teljes blokkot kitöltő betöltésjelző szöveggel (a régi spinner-border helyett). */
function LoadingState({ label = "Loading...", className }) {
    return (
        <div className={cn("flex min-h-[40vh] flex-col items-center justify-center gap-3 text-muted-foreground", className)}>
            <Spinner className="size-8 text-primary" />
            <span className="text-sm">{label}</span>
        </div>
    );
}

export { Spinner, LoadingState };
