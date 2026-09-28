import { cn } from "@/lib/utils";

/**
 * Egységes oldalkeret: középre igazított, reszponzív tartalomszélesség.
 * size: "sm" (űrlapok), "md" (részletek), "lg" (alapértelmezett), "xl" (rácsok, admin táblák)
 */
function PageContainer({ size = "lg", className, ...props }) {
    return (
        <div
            className={cn(
                "mx-auto w-full px-4 py-8 sm:px-6 lg:px-8",
                size === "sm" && "max-w-md",
                size === "md" && "max-w-3xl",
                size === "lg" && "max-w-6xl",
                size === "xl" && "max-w-7xl",
                className
            )}
            {...props}
        />
    );
}

/** Oldalfejléc: cím, opcionális leírás és jobbra igazított műveletek. */
function PageHeader({ title, description, icon: Icon, children, className }) {
    return (
        <div className={cn("mb-8 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between", className)}>
            <div className="space-y-1">
                <h1 className="flex items-center gap-3 text-3xl font-bold tracking-tight">
                    {Icon && <Icon className="size-7 text-primary" />}
                    {title}
                </h1>
                {description && <p className="text-muted-foreground">{description}</p>}
            </div>
            {children && <div className="flex flex-wrap items-center gap-2">{children}</div>}
        </div>
    );
}

/** Üres állapot: ikon, cím, leírás és opcionális cselekvés. */
function EmptyState({ icon: Icon, title, description, children, className }) {
    return (
        <div
            className={cn(
                "flex flex-col items-center justify-center gap-3 rounded-xl border border-dashed px-6 py-16 text-center",
                className
            )}
        >
            {Icon && (
                <div className="flex size-14 items-center justify-center rounded-full bg-muted">
                    <Icon className="size-7 text-muted-foreground" />
                </div>
            )}
            {title && <h3 className="text-lg font-semibold">{title}</h3>}
            {description && <p className="max-w-sm text-sm text-muted-foreground">{description}</p>}
            {children && <div className="mt-2 flex flex-wrap justify-center gap-2">{children}</div>}
        </div>
    );
}

export { PageContainer, PageHeader, EmptyState };
