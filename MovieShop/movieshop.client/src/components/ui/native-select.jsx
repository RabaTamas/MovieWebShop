import { ChevronDownIcon } from "lucide-react";

import { cn } from "@/lib/utils";

/**
 * shadcn/ui native-select: a böngésző saját <select> eleme shadcn-stílusban.
 * Ugyanúgy `value` + `onChange` alapú, mint egy sima select, ezért a meglévő
 * űrlaplogika változtatás nélkül használhatja.
 */
function NativeSelect({ className, containerClassName, ...props }) {
    return (
        <div
            className={cn("group/native-select relative w-full has-[select:disabled]:opacity-50", containerClassName)}
            data-slot="native-select-wrapper"
        >
            <select
                data-slot="native-select"
                className={cn(
                    "border-input dark:bg-input/30 dark:hover:bg-input/50 h-9 w-full min-w-0 appearance-none rounded-md border bg-transparent pl-3 pr-9 py-1 text-sm shadow-xs transition-[color,box-shadow] outline-none disabled:pointer-events-none disabled:cursor-not-allowed",
                    "focus-visible:border-ring focus-visible:ring-ring/50 focus-visible:ring-[3px]",
                    "aria-invalid:ring-destructive/20 dark:aria-invalid:ring-destructive/40 aria-invalid:border-destructive",
                    "[&>option]:bg-popover [&>option]:text-popover-foreground",
                    className
                )}
                {...props}
            />
            <ChevronDownIcon
                className="text-muted-foreground pointer-events-none absolute top-1/2 right-3 size-4 -translate-y-1/2 opacity-60 select-none"
                aria-hidden="true"
            />
        </div>
    );
}

export { NativeSelect };
