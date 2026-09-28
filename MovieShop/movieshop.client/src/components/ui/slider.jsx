import { Slider as SliderPrimitive } from "radix-ui";

import { cn } from "@/lib/utils";

/**
 * shadcn/ui slider (Radix). Több fogantyút is kezel: value={[min, max]} → tartománycsúszka.
 * thumbLabels: az egyes fogantyúk akadálymentes nevei (pl. ["Minimum price", "Maximum price"]).
 */
function Slider({ className, defaultValue, value, min = 0, max = 100, thumbLabels = [], ...props }) {
    const values = Array.isArray(value) ? value : Array.isArray(defaultValue) ? defaultValue : [min, max];

    return (
        <SliderPrimitive.Root
            data-slot="slider"
            defaultValue={defaultValue}
            value={value}
            min={min}
            max={max}
            className={cn(
                "relative flex w-full touch-none items-center py-2 select-none data-[disabled]:opacity-50",
                className
            )}
            {...props}
        >
            <SliderPrimitive.Track
                data-slot="slider-track"
                className="relative h-1.5 w-full grow overflow-hidden rounded-full bg-muted"
            >
                <SliderPrimitive.Range data-slot="slider-range" className="absolute h-full bg-primary" />
            </SliderPrimitive.Track>
            {values.map((_, index) => (
                <SliderPrimitive.Thumb
                    data-slot="slider-thumb"
                    key={index}
                    aria-label={thumbLabels[index]}
                    className="block size-4.5 cursor-grab rounded-full border-2 border-primary bg-background shadow-md shadow-primary/30 transition-[transform,box-shadow] hover:scale-110 focus-visible:ring-4 focus-visible:ring-primary/30 focus-visible:outline-none active:scale-110 active:cursor-grabbing"
                />
            ))}
        </SliderPrimitive.Root>
    );
}

export { Slider };
