import { UserRound } from "lucide-react";

import { Stagger, StaggerItem } from "@/components/motion/Reveal";

/** Vízszintesen görgethető szereplőlista TMDB-profilképekkel. */
export default function CastRow({ cast }) {
    if (!cast?.length) return null;

    return (
        <Stagger className="scrollbar-none mask-fade-x -mx-2 flex snap-x gap-4 overflow-x-auto px-2 pb-2" stagger={0.05}>
            {cast.map((person, i) => (
                <StaggerItem key={`${person.name}-${i}`} className="group w-28 shrink-0 snap-start text-center sm:w-32">
                    <div className="relative mx-auto mb-3 aspect-square w-full overflow-hidden rounded-full bg-muted ring-2 ring-border transition-all duration-300 group-hover:ring-primary">
                        {person.profileUrl ? (
                            <img
                                src={person.profileUrl}
                                alt={person.name}
                                loading="lazy"
                                className="size-full object-cover grayscale-[35%] transition-all duration-500 group-hover:scale-110 group-hover:grayscale-0"
                            />
                        ) : (
                            <div className="flex size-full items-center justify-center text-muted-foreground">
                                <UserRound className="size-10" />
                            </div>
                        )}
                    </div>
                    <p className="line-clamp-1 text-sm font-semibold">{person.name}</p>
                    {person.character && (
                        <p className="line-clamp-2 text-xs text-muted-foreground">{person.character}</p>
                    )}
                </StaggerItem>
            ))}
        </Stagger>
    );
}
