import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { AlertCircle, Clapperboard, ImageOff, Play, Users } from 'lucide-react';
import { useAuth } from '../contexts/AuthContext';
import API_BASE_URL from '../config/api';
import { Button } from '@/components/ui/button';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Skeleton } from '@/components/ui/skeleton';
import { EmptyState, PageContainer, PageHeader } from '@/components/ui/page';

const MyMovies = () => {
    const { token } = useAuth();
    const [movies, setMovies] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);

    useEffect(() => {
        const fetchPurchasedMovies = async () => {
            try {
                const response = await fetch(`${API_BASE_URL}/api/Movie/purchased`, {
                    headers: {
                        'Authorization': `Bearer ${token}`
                    }
                });

                if (!response.ok) {
                    throw new Error('Failed to fetch purchased movies');
                }

                const data = await response.json();
                setMovies(data);
            } catch (err) {
                setError(err.message);
            } finally {
                setLoading(false);
            }
        };

        if (token) {
            fetchPurchasedMovies();
        }
    }, [token]);

    return (
        <PageContainer size="xl">
            <PageHeader
                title="My Movies"
                icon={Clapperboard}
                description={!loading && !error ? `${movies.length} movie${movies.length !== 1 ? 's' : ''} in your library` : 'Your personal streaming library'}
            />

            {loading ? (
                <div className="grid grid-cols-2 gap-x-4 gap-y-8 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5">
                    {Array.from({ length: 5 }).map((_, i) => (
                        <div key={i} className="space-y-3">
                            <Skeleton className="aspect-[2/3] w-full rounded-xl" />
                            <Skeleton className="h-4 w-3/4" />
                        </div>
                    ))}
                </div>
            ) : error ? (
                <Alert variant="destructive">
                    <AlertCircle />
                    <AlertDescription>Error: {error}</AlertDescription>
                </Alert>
            ) : movies.length === 0 ? (
                <EmptyState
                    icon={Clapperboard}
                    title="No movies purchased yet"
                    description="Browse our collection and purchase movies to watch them here."
                >
                    <Button asChild><Link to="/">Browse Movies</Link></Button>
                </EmptyState>
            ) : (
                <div className="grid grid-cols-2 gap-x-4 gap-y-8 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5">
                    {movies.map(movie => (
                        <div key={movie.id} className="group flex flex-col gap-3">
                            <Link
                                to={`/my-movies/${movie.id}/watch`}
                                className="relative aspect-[2/3] overflow-hidden rounded-xl bg-muted ring-1 ring-border transition-all duration-300 group-hover:-translate-y-1 group-hover:shadow-2xl group-hover:ring-primary/50"
                                aria-label={`Watch ${movie.title}`}
                            >
                                {movie.imageUrl ? (
                                    <img
                                        src={movie.imageUrl}
                                        alt={movie.title}
                                        loading="lazy"
                                        className="size-full object-cover transition-transform duration-500 group-hover:scale-105"
                                    />
                                ) : (
                                    <div className="flex size-full items-center justify-center text-muted-foreground">
                                        <ImageOff className="size-10" />
                                    </div>
                                )}
                                <div className="absolute inset-0 flex items-center justify-center bg-black/50 opacity-0 transition-opacity duration-300 group-hover:opacity-100">
                                    <span className="flex size-16 items-center justify-center rounded-full bg-primary text-primary-foreground shadow-xl shadow-primary/40">
                                        <Play className="ml-1 size-7 fill-current" />
                                    </span>
                                </div>
                            </Link>
                            <div className="space-y-1 px-0.5">
                                <h3 className="line-clamp-1 font-semibold">{movie.title}</h3>
                                {movie.description && (
                                    <p className="line-clamp-2 text-xs text-muted-foreground">
                                        {movie.description}
                                    </p>
                                )}
                            </div>
                            <div className="mt-auto flex gap-2">
                                <Button asChild size="sm" className="flex-1">
                                    <Link to={`/my-movies/${movie.id}/watch`}>
                                        <Play className="fill-current" />
                                        Watch Now
                                    </Link>
                                </Button>
                                <Button asChild size="icon-sm" variant="outline" title="Watch Party" aria-label="Watch Party">
                                    <Link to={`/my-movies/${movie.id}/watch-party`}>
                                        <Users />
                                    </Link>
                                </Button>
                            </div>
                        </div>
                    ))}
                </div>
            )}
        </PageContainer>
    );
};

export default MyMovies;
