import { useRef } from "react";
import { Link } from "react-router-dom";
import { ImageOff } from "lucide-react";

import { formatPrice } from "@/lib/utils";

const MAX_TILT = 10; // fok

const MovieCard = ({ movie }) => {
    const posterRef = useRef(null);
    const hasDiscount = movie.discountedPrice != null;
    const discountPercent =
        hasDiscount && movie.price > 0 ? Math.round((1 - movie.discountedPrice / movie.price) * 100) : 0;

    // 3D döntés és fényvisszaverődés az egér pozíciója alapján — CSS-változókkal, újrarenderelés nélkül
    const handlePointerMove = (e) => {
        const el = posterRef.current;
        if (!el || e.pointerType === "touch") return;
        const rect = el.getBoundingClientRect();
        const x = (e.clientX - rect.left) / rect.width;
        const y = (e.clientY - rect.top) / rect.height;
        el.style.setProperty("--rx", `${(0.5 - y) * MAX_TILT}deg`);
        el.style.setProperty("--ry", `${(x - 0.5) * MAX_TILT}deg`);
        el.style.setProperty("--gx", `${x * 100}%`);
        el.style.setProperty("--gy", `${y * 100}%`);
    };

    const handlePointerLeave = () => {
        const el = posterRef.current;
        if (!el) return;
        el.style.setProperty("--rx", "0deg");
        el.style.setProperty("--ry", "0deg");
    };

    return (
        <Link
            to={`/movies/${movie.id}`}
            className="group flex h-full flex-col gap-3 rounded-xl outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50"
            onPointerMove={handlePointerMove}
            onPointerLeave={handlePointerLeave}
        >
            <div
                ref={posterRef}
                className="relative aspect-[2/3] overflow-hidden rounded-xl bg-muted ring-1 ring-border transition-[transform,box-shadow] duration-300 ease-out [transform:perspective(900px)_rotateX(var(--rx,0deg))_rotateY(var(--ry,0deg))] group-hover:shadow-2xl group-hover:shadow-primary/20 group-hover:ring-primary/60"
            >
                {movie.imageUrl ? (
                    <img
                        src={movie.imageUrl}
                        alt={movie.title}
                        loading="lazy"
                        className="size-full object-cover transition-transform duration-500 group-hover:scale-110"
                    />
                ) : (
                    <div className="flex size-full items-center justify-center text-muted-foreground">
                        <ImageOff className="size-10" />
                    </div>
                )}
                {/* Fényvisszaverődés, ami követi a kurzort */}
                <div className="pointer-events-none absolute inset-0 opacity-0 transition-opacity duration-300 group-hover:opacity-100 bg-[radial-gradient(circle_at_var(--gx,50%)_var(--gy,50%),rgba(255,255,255,0.28),transparent_55%)]" />
                <div className="absolute inset-0 bg-gradient-to-t from-black/85 via-black/10 to-transparent opacity-0 transition-opacity duration-300 group-hover:opacity-100" />
                <span className="absolute inset-x-3 bottom-3 translate-y-2 rounded-md bg-primary py-1.5 text-center text-sm font-semibold text-primary-foreground opacity-0 transition-all duration-300 group-hover:translate-y-0 group-hover:opacity-100">
                    Details
                </span>
                {discountPercent > 0 && (
                    <span className="absolute top-2 left-2 rounded-md bg-destructive px-2 py-0.5 text-xs font-bold text-white shadow">
                        -{discountPercent}%
                    </span>
                )}
            </div>

            <div className="flex flex-1 flex-col gap-1 px-0.5">
                <h3 className="line-clamp-2 leading-snug font-semibold transition-colors group-hover:text-primary">
                    {movie.title}
                </h3>
                <p className="mt-auto text-sm">
                    {hasDiscount ? (
                        <>
                            <span className="text-muted-foreground line-through">{formatPrice(movie.price)}</span>
                            <span className="ml-2 font-bold text-destructive">{formatPrice(movie.discountedPrice)}</span>
                        </>
                    ) : (
                        <span className="font-semibold text-foreground/90">{formatPrice(movie.price)}</span>
                    )}
                </p>
            </div>
        </Link>
    );
};

export default MovieCard;
