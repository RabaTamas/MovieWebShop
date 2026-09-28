import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { AlertCircle, ArrowLeft, CheckCircle2, CircleCheck, Plus, Tags } from "lucide-react";
import { useAuth } from "../contexts/AuthContext";
import API_BASE_URL from "../config/api";
import { cn, formatPrice } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { LoadingState } from "@/components/ui/spinner";
import { PageContainer, PageHeader } from "@/components/ui/page";

const MovieCategories = () => {
    const { id } = useParams();
    const navigate = useNavigate();
    const { token } = useAuth();

    const [movie, setMovie] = useState(null);
    const [allCategories, setAllCategories] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [saving, setSaving] = useState(false);
    const [success, setSuccess] = useState(false);

    // Fetch movie and all categories when component mounts
    useEffect(() => {
        const fetchData = async () => {
            try {
                // Fetch movie details
                const movieResponse = await fetch(`${API_BASE_URL}/api/movie/${id}`, {
                    headers: {
                        'Authorization': `Bearer ${token}`
                    }
                });

                if (!movieResponse.ok) {
                    throw new Error(`Error ${movieResponse.status}: ${movieResponse.statusText}`);
                }

                const movieData = await movieResponse.json();
                setMovie(movieData);

                // Fetch all categories
                const categoriesResponse = await fetch(`${API_BASE_URL}/api/category`, {
                    headers: {
                        'Authorization': `Bearer ${token}`
                    }
                });

                if (!categoriesResponse.ok) {
                    throw new Error(`Error ${categoriesResponse.status}: ${categoriesResponse.statusText}`);
                }

                const categoriesData = await categoriesResponse.json();
                setAllCategories(categoriesData);
                setLoading(false);
            } catch (err) {
                console.error("Failed to fetch data:", err);
                setError(err.message);
                setLoading(false);
            }
        };

        fetchData();
    }, [id, token]);

    const isMovieCategoryAssigned = (categoryId) => {
        return movie?.categories?.some(c => c.id === categoryId) || false;
    };

    const handleCategoryToggle = async (categoryId) => {
        setSaving(true);
        setSuccess(false);
        setError(null);

        try {
            const isAssigned = isMovieCategoryAssigned(categoryId);

            const response = await fetch(`${API_BASE_URL}/api/movie/${id}/category/${categoryId}`, {
                method: isAssigned ? 'DELETE' : 'POST',
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });

            if (!response.ok) {
                throw new Error(`Error ${response.status}: ${response.statusText}`);
            }

            // Update the local state to reflect the change
            if (isAssigned) {
                setMovie({
                    ...movie,
                    categories: movie.categories.filter(c => c.id !== categoryId)
                });
            } else {
                const categoryToAdd = allCategories.find(c => c.id === categoryId);
                setMovie({
                    ...movie,
                    categories: [...movie.categories, categoryToAdd]
                });
            }

            setSuccess(true);
            setTimeout(() => setSuccess(false), 3000); // Hide success message after 3 seconds
        } catch (err) {
            console.error("Failed to update category assignment:", err);
            setError(err.message);
        } finally {
            setSaving(false);
        }
    };

    if (loading) {
        return <LoadingState label="Loading data..." />;
    }

    if (!movie) {
        return (
            <PageContainer size="md">
                <Alert variant={error ? "destructive" : "warning"}>
                    <AlertCircle />
                    <AlertDescription>{error ? `Error: ${error}` : "Movie not found"}</AlertDescription>
                </Alert>
            </PageContainer>
        );
    }

    return (
        <PageContainer size="lg" className="max-w-5xl">
            <PageHeader title={<>Manage Categories for “{movie.title}”</>} icon={Tags}>
                <Button variant="outline" onClick={() => navigate('/admin/movies')}>
                    <ArrowLeft /> Back to Movies
                </Button>
            </PageHeader>

            {success && (
                <Alert variant="success" className="mb-6">
                    <CheckCircle2 />
                    <AlertDescription>Category assignment updated successfully!</AlertDescription>
                </Alert>
            )}

            {error && (
                <Alert variant="destructive" className="mb-6">
                    <AlertCircle />
                    <AlertDescription>{error}</AlertDescription>
                </Alert>
            )}

            <div className="grid items-start gap-6 md:grid-cols-2">
                <div className="space-y-6">
                    <Card>
                        <CardHeader>
                            <CardTitle>Movie Details</CardTitle>
                        </CardHeader>
                        <CardContent className="flex gap-4">
                            <img
                                src={movie.imageUrl}
                                alt={movie.title}
                                className="h-36 w-24 shrink-0 rounded-lg object-cover ring-1 ring-border"
                            />
                            <div className="space-y-1.5">
                                <h2 className="text-lg font-semibold">{movie.title}</h2>
                                <p className="text-sm text-muted-foreground">Price: <strong className="text-foreground">{formatPrice(movie.price)}</strong></p>
                                {movie.discountedPrice && (
                                    <p className="text-sm text-muted-foreground">Discounted Price: <strong className="text-destructive">{formatPrice(movie.discountedPrice)}</strong></p>
                                )}
                            </div>
                        </CardContent>
                    </Card>

                    <Card>
                        <CardHeader>
                            <CardTitle>Current Categories</CardTitle>
                        </CardHeader>
                        <CardContent>
                            {movie.categories && movie.categories.length > 0 ? (
                                <div className="flex flex-wrap gap-2">
                                    {movie.categories.map(category => (
                                        <Badge key={category.id} className="px-2.5 py-1 text-sm">{category.name}</Badge>
                                    ))}
                                </div>
                            ) : (
                                <p className="text-sm text-muted-foreground">No categories assigned</p>
                            )}
                        </CardContent>
                    </Card>
                </div>

                <Card>
                    <CardHeader>
                        <CardTitle>All Categories</CardTitle>
                        <CardDescription>Click a category to assign or remove it.</CardDescription>
                    </CardHeader>
                    <CardContent>
                        <div className="divide-y overflow-hidden rounded-lg border">
                            {allCategories.map(category => {
                                const assigned = isMovieCategoryAssigned(category.id);
                                return (
                                    <button
                                        key={category.id}
                                        type="button"
                                        className={cn(
                                            "flex w-full cursor-pointer items-center justify-between px-4 py-2.5 text-left text-sm transition-colors disabled:cursor-wait disabled:opacity-60",
                                            assigned ? "bg-primary/15 font-medium text-foreground hover:bg-primary/20" : "hover:bg-muted"
                                        )}
                                        onClick={() => handleCategoryToggle(category.id)}
                                        disabled={saving}
                                    >
                                        {category.name}
                                        {assigned ? (
                                            <CircleCheck className="size-4 text-primary" />
                                        ) : (
                                            <Plus className="size-4 text-muted-foreground" />
                                        )}
                                    </button>
                                );
                            })}
                        </div>
                    </CardContent>
                </Card>
            </div>
        </PageContainer>
    );
};

export default MovieCategories;
