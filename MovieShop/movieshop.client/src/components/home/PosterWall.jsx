import { cn } from "@/lib/utils";

const DESKTOP_COLUMNS = 4;
const MOBILE_COLUMNS = 3;

/**
 * Dekoratív, 3D-ben megdöntött „poszterfal" a főoldali hero mögé: a filmposzterek
 * oszloponként ellentétes irányban, végtelenítve úsznak.
 *
 * Két változatban jelenik meg:
 * - nagy képernyőn a jobb oldali sávban, a címsor mellett;
 * - telefonon (és így a telepített PWA-ban) a teljes hero mögött, halványabban
 *   és erősebb elhalványítással, hogy a szöveg olvasható maradjon.
 *
 * A mozgást a rendszer „csökkentett mozgás" beállítása kikapcsolja (index.css).
 */
export default function PosterWall({ movies }) {
    const posters = (movies ?? []).map((m) => m.imageUrl).filter(Boolean);
    if (posters.length < 3) return null;

    // Kevés film esetén is legyen elég elem a folytonos görgetéshez
    const buildColumns = (count) => {
        let pool = [...posters];
        while (pool.length < count * 4) pool = [...pool, ...posters];
        return Array.from({ length: count }, (_, c) => pool.filter((_, i) => i % count === c));
    };

    const renderColumns = (columns, { columnClass, gapClass, baseDuration }) =>
        columns.map((column, ci) => (
            <div key={ci} className={columnClass}>
                <div
                    className={cn("flex flex-col", gapClass, ci % 2 === 0 ? "animate-marquee-up" : "animate-marquee-down")}
                    style={{ animationDuration: `${baseDuration + ci * 14}s` }}
                >
                    {[...column, ...column].map((src, i) => (
                        <img
                            key={i}
                            src={src}
                            alt=""
                            loading="lazy"
                            className="aspect-[2/3] w-full rounded-xl object-cover shadow-2xl ring-1 ring-white/10"
                        />
                    ))}
                </div>
            </div>
        ));

    return (
        <>
            {/* ── Telefon és tablet: a teljes hero mögött, halványan ────────── */}
            <div aria-hidden="true" className="pointer-events-none absolute inset-0 overflow-hidden lg:hidden">
                <div className="absolute -top-1/3 -right-[6%] flex h-[170%] gap-3 opacity-40 [transform:perspective(1100px)_rotateX(12deg)_rotateZ(7deg)]">
                    {renderColumns(buildColumns(MOBILE_COLUMNS), {
                        columnClass: "w-28 sm:w-36",
                        gapClass: "gap-3",
                        baseDuration: 70,
                    })}
                </div>
                {/* A címsor mögött erősebb fátyol, hogy a szöveg kontrasztos maradjon */}
                <div className="absolute inset-0 bg-gradient-to-b from-background via-background/55 to-background" />
                <div className="absolute inset-x-0 bottom-0 h-1/4 bg-gradient-to-t from-background to-transparent" />
            </div>

            {/* ── Nagy képernyő: a jobb oldali sávban (változatlan) ─────────── */}
            <div aria-hidden="true" className="mask-fade-y pointer-events-none absolute inset-y-0 right-0 hidden w-[58%] overflow-hidden lg:block">
                <div className="absolute -top-1/3 -right-[8%] flex h-[170%] gap-5 opacity-55 [transform:perspective(1400px)_rotateX(16deg)_rotateY(-24deg)_rotateZ(9deg)]">
                    {renderColumns(buildColumns(DESKTOP_COLUMNS), {
                        columnClass: "w-40 xl:w-44",
                        gapClass: "gap-5",
                        baseDuration: 60,
                    })}
                </div>
                {/* A szöveg felőli oldalon elhalványul a háttérbe */}
                <div className="absolute inset-0 bg-gradient-to-r from-background via-background/50 to-transparent" />
                <div className="absolute inset-x-0 bottom-0 h-1/3 bg-gradient-to-t from-background to-transparent" />
            </div>
        </>
    );
}
