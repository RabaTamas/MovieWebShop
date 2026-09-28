import { useParams, useNavigate } from "react-router-dom";
import { useEffect, useState } from "react";
import { toast } from "sonner";
import { Images, Info, Users } from "lucide-react";
import { useAuth } from "../contexts/AuthContext";
import ProductSection from "../components/ProductSection";
import ReviewList from "../components/ReviewList";
import CastRow from "../components/movie/CastRow";
import ImageGallery from "../components/movie/ImageGallery";
import OfflineState from "../components/pwa/OfflineState";

import API_BASE_URL from "../config/api";
import { formatPrice } from "@/lib/utils";
import { LoadingState } from "@/components/ui/spinner";
import { Skeleton } from "@/components/ui/skeleton";
import { PageContainer } from "@/components/ui/page";
import { Reveal } from "@/components/motion/Reveal";

const SectionTitle = ({ icon: Icon, children }) => (
    <h2 className="mb-5 flex items-center gap-3 text-2xl font-bold tracking-tight">
        <span className="h-7 w-1.5 rounded-full bg-primary" />
        {Icon && <Icon className="size-5 text-primary" />}
        {children}
    </h2>
);

const formatUsd = (value) =>
    value > 0 ? `$${value.toLocaleString("en-US", { maximumFractionDigits: 0 })}` : null;

const MovieDetails = () => {
    const { id } = useParams();
    const navigate = useNavigate();
    const { user, token } = useAuth();
    const [movie, setMovie] = useState(null);
    const [loading, setLoading] = useState(true);
    const [isPurchased, setIsPurchased] = useState(false);
    const [extras, setExtras] = useState(null);
    const [extrasLoading, setExtrasLoading] = useState(true);
    const [offline, setOffline] = useState(false);

    useEffect(() => {
        setLoading(true);
        setOffline(false);
        fetch(`${API_BASE_URL}/api/Movie/${id}`)
            .then((res) => {
                if (!res.ok) {
                    throw new Error("Movie not found");
                }
                return res.json();
            })
            .then((data) => {
                setMovie(data);
                setLoading(false);
            })
            .catch((error) => {
                console.error(error);
                setLoading(false);
                // Offline ne dobjuk vissza a főoldalra: az adatlap a szerverről jön, ezt jelezzük
                if (!navigator.onLine) {
                    setOffline(true);
                    return;
                }
                navigate("/");
            });
    }, [id, navigate]);

    // Bővített TMDB-adatok (háttérképek, galéria, stáb) — opcionális, a filmadatlap nélküle is teljes
    useEffect(() => {
        setExtras(null);
        setExtrasLoading(true);
        fetch(`${API_BASE_URL}/api/Movie/${id}/tmdb`)
            .then((res) => (res.ok ? res.json() : null))
            .then((data) => setExtras(data))
            .catch(() => setExtras(null))
            .finally(() => setExtrasLoading(false));
    }, [id]);

    // Check if user has already purchased this movie
    useEffect(() => {
        if (user && token) {
            fetch(`${API_BASE_URL}/api/Movie/purchased`, {
                headers: { Authorization: `Bearer ${token}` }
            })
                .then(res => res.json())
                .then(movies => {
                    const purchased = movies.some(m => m.id === parseInt(id));
                    setIsPurchased(purchased);
                })
                .catch(err => console.error("Error checking purchased movies:", err));
        }
    }, [user, token, id]);

    const handleAddToCart = async () => {
        if (!user) {
            navigate("/login");
            return;
        }

        if (isPurchased) {
            toast.info("You already own this movie! Watch it in My Movies.");
            navigate("/my-movies");
            return;
        }

        try {
            const response = await fetch(`${API_BASE_URL}/api/ShoppingCart/add`, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    Authorization: `Bearer ${token}`
                },
                body: JSON.stringify({
                    movieId: movie.id,
                    quantity: 1,
                }),
            });

            if (!response.ok) {
                const errorData = await response.json();
                throw new Error(errorData.message || "Failed to add movie to cart");
            }

            navigate("/cart");
        } catch (error) {
            console.error("Error adding to cart:", error);
            toast.error(error.message);
        }
    };

    if (loading) {
        return <LoadingState label="Loading movie details..." />;
    }

    if (offline) {
        return <PageContainer size="md"><OfflineState /></PageContainer>;
    }

    if (!movie) {
        return <PageContainer><p className="text-muted-foreground">Movie not found</p></PageContainer>;
    }

    const facts = extras
        ? [
            ["Original title", extras.originalTitle !== movie.title ? extras.originalTitle : null],
            ["Release date", extras.releaseDate && new Date(extras.releaseDate).toLocaleDateString("en-US", { year: "numeric", month: "long", day: "numeric" })],
            ["Runtime", extras.runtime ? `${extras.runtime} minutes` : null],
            ["Language", extras.originalLanguage?.toUpperCase()],
            ["Director", extras.directors?.join(", ")],
            ["Budget", formatUsd(extras.budget)],
            ["Box office", formatUsd(extras.revenue)],
            ["Price here", formatPrice(movie.discountedPrice ?? movie.price)],
        ].filter(([, value]) => value)
        : [];

    return (
        <div>
            <ProductSection movie={movie} onAddToCart={handleAddToCart} isPurchased={isPurchased} extras={extras} />

            <div className="mx-auto max-w-7xl space-y-16 px-4 py-14 sm:px-6 lg:px-8">
                {extrasLoading ? (
                    <div className="space-y-6">
                        <Skeleton className="h-8 w-48" />
                        <div className="flex gap-4">
                            {Array.from({ length: 6 }).map((_, i) => <Skeleton key={i} className="size-28 rounded-full" />)}
                        </div>
                        <Skeleton className="h-72 w-full rounded-xl" />
                    </div>
                ) : extras && (
                    <div className="grid items-start gap-12 lg:grid-cols-[1fr_320px]">
                        <div className="min-w-0 space-y-14">
                            {extras.cast?.length > 0 && (
                                <Reveal>
                                    <SectionTitle icon={Users}>Top Cast</SectionTitle>
                                    <CastRow cast={extras.cast} />
                                </Reveal>
                            )}

                            {extras.backdrops?.length > 0 && (
                                <Reveal>
                                    <SectionTitle icon={Images}>Gallery</SectionTitle>
                                    <ImageGallery images={extras.backdrops} title={movie.title} />
                                </Reveal>
                            )}
                        </div>

                        {facts.length > 0 && (
                            <Reveal delay={0.1} className="lg:sticky lg:top-24">
                                <div className="overflow-hidden rounded-2xl border bg-card">
                                    <div className="flex items-center gap-2 border-b bg-muted/40 px-5 py-3 font-semibold">
                                        <Info className="size-4 text-primary" /> Movie facts
                                    </div>
                                    <dl className="divide-y">
                                        {facts.map(([label, value]) => (
                                            <div key={label} className="flex justify-between gap-4 px-5 py-3 text-sm">
                                                <dt className="text-muted-foreground">{label}</dt>
                                                <dd className="text-right font-medium">{value}</dd>
                                            </div>
                                        ))}
                                    </dl>
                                    {extras.posters?.length > 1 && (
                                        <div className="grid grid-cols-4 gap-1.5 border-t p-3">
                                            {extras.posters.slice(0, 4).map((poster) => (
                                                <img
                                                    key={poster.url}
                                                    src={poster.thumbnailUrl}
                                                    alt=""
                                                    decoding="async"
                                                    className="aspect-[2/3] w-full rounded-md object-cover transition-transform duration-300 hover:-translate-y-1 hover:scale-105"
                                                />
                                            ))}
                                        </div>
                                    )}
                                </div>
                            </Reveal>
                        )}
                    </div>
                )}

                <Reveal>
                    <ReviewList movieId={movie.id} />
                </Reveal>
            </div>
        </div>
    );
};

export default MovieDetails;
