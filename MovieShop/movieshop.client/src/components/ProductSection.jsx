import { Link } from "react-router-dom";
import { motion } from "motion/react";
import { CalendarDays, CheckCircle2, Clapperboard, Clock, Globe, ImageOff, Play, ShoppingCart } from "lucide-react";

import { formatPrice } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import RatingRing from "@/components/movie/RatingRing";

const EASE = [0.22, 1, 0.36, 1];

const heroContainer = {
    hidden: {},
    show: { transition: { staggerChildren: 0.09, delayChildren: 0.15 } },
};

const heroItem = {
    hidden: { opacity: 0, y: 26, filter: "blur(6px)" },
    show: { opacity: 1, y: 0, filter: "blur(0px)", transition: { duration: 0.7, ease: EASE } },
};

const formatRuntime = (minutes) => {
    if (!minutes) return null;
    const h = Math.floor(minutes / 60);
    const m = minutes % 60;
    return h > 0 ? `${h}h ${m}m` : `${m}m`;
};

/**
 * Filmadatlap „hero" szekciója: teljes szélességű TMDB háttérkép lassú Ken Burns
 * zoommal, szemcsés mozis réteggel, belebegő poszterrel és lépcsőzetesen előtűnő adatokkal.
 * extras: a /api/Movie/{id}/tmdb bővített adatai (opcionális — nélküle a poszterből dolgozik).
 */
const ProductSection = ({ movie, onAddToCart, isPurchased = false, extras = null }) => {
    const rating = extras?.voteAverage ?? movie.tmdbInfo?.voteAverage ?? 0;
    const voteCount = extras?.voteCount ?? movie.tmdbInfo?.voteCount ?? 0;
    const releaseDate = extras?.releaseDate ?? movie.tmdbInfo?.releaseDate;
    const genres = extras?.genres?.length ? extras.genres : (movie.categories ?? []).map((c) => c.name);
    const runtime = formatRuntime(extras?.runtime);
    const backdrop = extras?.backdropUrl;
    const discount = movie.discountedPrice ? Math.round((1 - movie.discountedPrice / movie.price) * 100) : 0;

    return (
        <section className="film-grain relative isolate overflow-hidden border-b">
            {/* Háttér: TMDB backdrop Ken Burns effekttel, vagy elmosott poszter */}
            <div className="absolute inset-0 -z-20 overflow-hidden" aria-hidden="true">
                {backdrop ? (
                    <motion.img
                        src={backdrop}
                        alt=""
                        initial={{ opacity: 0 }}
                        animate={{ opacity: 1 }}
                        transition={{ duration: 1.2 }}
                        className="size-full animate-kenburns object-cover object-center brightness-110 saturate-125"
                    />
                ) : (
                    movie.imageUrl && (
                        <div
                            className="size-full scale-110 bg-cover bg-center opacity-40 blur-3xl"
                            style={{ backgroundImage: `url(${movie.imageUrl})` }}
                        />
                    )
                )}
            </div>
            {/* Átmenetek: alul és a szöveg mögött (bal oldalon) sötétít, jobb felül a háttérkép érvényesül */}
            <div className="absolute inset-0 -z-10 bg-gradient-to-t from-background via-background/55 to-background/5" aria-hidden="true" />
            <div className="absolute inset-0 -z-10 bg-gradient-to-r from-background/95 via-background/55 to-transparent md:via-background/40" aria-hidden="true" />
            <div className="absolute inset-0 -z-10 bg-[radial-gradient(ellipse_at_75%_35%,transparent_30%,color-mix(in_oklch,var(--background)_55%,transparent)_100%)]" aria-hidden="true" />

            <div className="relative z-[2] mx-auto grid max-w-7xl items-end gap-8 px-4 pt-10 pb-12 md:min-h-[min(78vh,760px)] md:pt-20 sm:px-6 md:grid-cols-[minmax(0,300px)_1fr] md:gap-12 lg:px-8">
                {/* Poszter */}
                <motion.div
                    initial={{ opacity: 0, y: 40, rotateY: -18, scale: 0.92 }}
                    animate={{ opacity: 1, y: 0, rotateY: 0, scale: 1 }}
                    transition={{ duration: 1, ease: EASE }}
                    style={{ transformPerspective: 1200 }}
                    className="mx-auto w-full max-w-[260px] md:max-w-none"
                >
                    <div className="group relative aspect-[2/3] overflow-hidden rounded-2xl bg-muted shadow-[0_30px_80px_-20px_rgba(0,0,0,0.8)] ring-1 ring-white/15">
                        {movie.imageUrl ? (
                            <img className="size-full object-cover transition-transform duration-700 group-hover:scale-105" src={movie.imageUrl} alt={movie.title} />
                        ) : (
                            <div className="flex size-full items-center justify-center text-muted-foreground">
                                <ImageOff className="size-12" />
                            </div>
                        )}
                        {/* Átsuhanó fényvisszaverődés */}
                        <div className="pointer-events-none absolute inset-0 -translate-x-full bg-gradient-to-r from-transparent via-white/25 to-transparent transition-transform duration-1000 group-hover:translate-x-full" />
                        {discount > 0 && (
                            <span className="absolute top-3 left-3 rounded-lg bg-destructive px-2.5 py-1 text-sm font-bold text-white shadow-lg">
                                -{discount}%
                            </span>
                        )}
                    </div>
                </motion.div>

                {/* Adatok */}
                <motion.div variants={heroContainer} initial="hidden" animate="show" className="flex flex-col gap-5">
                    <motion.div variants={heroItem} className="flex flex-wrap items-center gap-2 text-sm">
                        {releaseDate && (
                            <span className="flex items-center gap-1.5 rounded-full bg-white/10 px-3 py-1 backdrop-blur">
                                <CalendarDays className="size-3.5" /> {new Date(releaseDate).getFullYear()}
                            </span>
                        )}
                        {runtime && (
                            <span className="flex items-center gap-1.5 rounded-full bg-white/10 px-3 py-1 backdrop-blur">
                                <Clock className="size-3.5" /> {runtime}
                            </span>
                        )}
                        {extras?.originalLanguage && (
                            <span className="flex items-center gap-1.5 rounded-full bg-white/10 px-3 py-1 uppercase backdrop-blur">
                                <Globe className="size-3.5" /> {extras.originalLanguage}
                            </span>
                        )}
                        {genres.map((genre) => (
                            <Badge key={genre} variant="warning" className="rounded-full px-3 py-1 text-sm">
                                {genre}
                            </Badge>
                        ))}
                    </motion.div>

                    <motion.h1
                        variants={heroItem}
                        className="font-display text-6xl leading-[0.9] tracking-wide text-balance drop-shadow-[0_4px_30px_rgba(0,0,0,0.6)] sm:text-7xl lg:text-8xl"
                    >
                        {movie.title}
                    </motion.h1>

                    {extras?.tagline && (
                        <motion.p variants={heroItem} className="text-lg text-primary italic sm:text-xl">
                            “{extras.tagline}”
                        </motion.p>
                    )}

                    {/* Értékelés + rendező */}
                    {(voteCount > 0 || extras?.directors?.length > 0) && (
                        <motion.div variants={heroItem} className="flex flex-wrap items-center gap-x-8 gap-y-4">
                            {voteCount > 0 && (
                                <div className="flex items-center gap-3">
                                    <RatingRing value={rating} />
                                    <div>
                                        <div className="font-semibold">TMDB Rating</div>
                                        <div className="text-sm text-muted-foreground">{voteCount.toLocaleString()} votes</div>
                                    </div>
                                </div>
                            )}
                            {extras?.directors?.length > 0 && (
                                <div className="flex items-center gap-3">
                                    <div className="flex size-12 items-center justify-center rounded-full bg-white/10 backdrop-blur">
                                        <Clapperboard className="size-5 text-primary" />
                                    </div>
                                    <div>
                                        <div className="text-sm text-muted-foreground">Directed by</div>
                                        <div className="font-semibold">{extras.directors.join(", ")}</div>
                                    </div>
                                </div>
                            )}
                        </motion.div>
                    )}

                    <motion.p variants={heroItem} className="max-w-2xl text-base leading-relaxed text-foreground/80 sm:text-lg">
                        {movie.description}
                    </motion.p>

                    {/* Ár + vásárlás */}
                    <motion.div variants={heroItem} className="flex flex-wrap items-center gap-x-6 gap-y-4">
                        <div className="flex items-baseline gap-3">
                            {movie.discountedPrice ? (
                                <>
                                    <span className="text-4xl font-bold text-destructive">{formatPrice(movie.discountedPrice)}</span>
                                    <span className="text-xl text-muted-foreground line-through">{formatPrice(movie.price)}</span>
                                </>
                            ) : (
                                <span className="text-4xl font-bold">{formatPrice(movie.price)}</span>
                            )}
                        </div>

                        {isPurchased ? (
                            <div className="flex flex-wrap items-center gap-3 rounded-xl border border-success/30 bg-success/10 px-4 py-2.5 text-success backdrop-blur">
                                <CheckCircle2 className="size-5" />
                                <span className="font-medium">You already own this movie! Watch it in My Movies.</span>
                                <Button size="sm" variant="success" asChild>
                                    <Link to={`/my-movies/${movie.id}/watch`}><Play className="fill-current" />Watch</Link>
                                </Button>
                            </div>
                        ) : (
                            <motion.div whileHover={{ scale: 1.04 }} whileTap={{ scale: 0.97 }}>
                                <Button
                                    size="lg"
                                    className="group/cta relative h-13 overflow-hidden px-8 text-base shadow-[0_10px_40px_-8px] shadow-primary/60"
                                    onClick={onAddToCart}
                                >
                                    {/* Átsuhanó fénycsík hoverre */}
                                    <span className="pointer-events-none absolute inset-0 -translate-x-full bg-gradient-to-r from-transparent via-white/40 to-transparent transition-transform duration-700 group-hover/cta:translate-x-full" />
                                    <ShoppingCart className="size-5" />
                                    Add to Cart
                                </Button>
                            </motion.div>
                        )}
                    </motion.div>
                </motion.div>
            </div>
        </section>
    );
};

export default ProductSection;
