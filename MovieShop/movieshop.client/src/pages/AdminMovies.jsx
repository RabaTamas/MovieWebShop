import { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import { toast } from "sonner";
import { AlertCircle, Film, FileVideo, Pencil, Plus, RefreshCw, RotateCcw, Tags, Trash2 } from "lucide-react";
import { useAuth } from "../contexts/AuthContext";

import API_BASE_URL from "../config/api";
import { cn, formatPrice } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { LoadingState } from "@/components/ui/spinner";
import { EmptyState, PageContainer, PageHeader } from "@/components/ui/page";

const viewTabs = [
    { key: "active", label: "Active Movies" },
    { key: "deleted", label: "Deleted Movies" },
    { key: "all", label: "All Movies" },
];

const AdminMovies = () => {
    const { token } = useAuth();
    const [movies, setMovies] = useState([]);
    const [deletedMovies, setDeletedMovies] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [viewMode, setViewMode] = useState("active"); // active, deleted, all

    // Fetch movies when component mounts or viewMode changes
    useEffect(() => {
        const fetchMovies = async () => {
            try {
                setLoading(true);
                let endpoint;

                // Select endpoint based on view mode
                switch (viewMode) {
                    case "deleted":
                        endpoint = `${API_BASE_URL}/api/movie/admin/deleted`;
                        break;
                    case "all":
                        endpoint = `${API_BASE_URL}/api/movie/admin/all`;
                        break;
                    default:
                        endpoint = `${API_BASE_URL}/api/movie`; // Active movies only
                        break;
                }

                const response = await fetch(endpoint, {
                    headers: {
                        'Authorization': `Bearer ${token}`
                    }
                });

                if (!response.ok) {
                    const contentType = response.headers.get("content-type");
                    if (contentType && contentType.indexOf("application/json") === -1) {
                        throw new Error(`Server returned ${response.status}: ${response.statusText} (not JSON)`);
                    }
                    throw new Error(`Error ${response.status}: ${response.statusText}`);
                }

                const data = await response.json();

                if (viewMode === "deleted") {
                    setDeletedMovies(data);
                } else if (viewMode === "all") {
                    setMovies(data);
                } else {
                    // For active view, filter out any deleted movies that might be in the response
                    setMovies(data.filter(movie => !movie.isDeleted));
                }

                setLoading(false);
            } catch (err) {
                console.error("Failed to fetch movies:", err);
                setError(err.message);
                setLoading(false);
            }
        };

        if (token) {
            fetchMovies();
        } else {
            setError("Authentication token is missing");
            setLoading(false);
        }
    }, [token, viewMode]);

    const handleSoftDeleteMovie = async (id) => {
        if (!window.confirm("Are you sure you want to delete this movie? It will be moved to the deleted items list.")) {
            return;
        }

        try {
            const deleteUrl = `${API_BASE_URL}/api/movie/${id}`;
            const response = await fetch(deleteUrl, {
                method: 'DELETE',
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });

            if (!response.ok) {
                handleApiError(response, "delete");
                return;
            }

            // Successfully deleted - remove the movie from the active list
            setMovies(movies.filter(movie => movie.id !== id));
            toast.success("Movie deleted successfully. You can find it in the 'Deleted Movies' view.");
        } catch (err) {
            console.error("Failed to delete movie:", err);
            setError(`Failed to delete movie: ${err.message}`);
        }
    };

    const handleRestoreMovie = async (id) => {
        try {
            const restoreUrl = `${API_BASE_URL}/api/movie/${id}/restore`;
            const response = await fetch(restoreUrl, {
                method: 'PATCH',
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });

            if (!response.ok) {
                handleApiError(response, "restore");
                return;
            }

            // Successfully restored - remove from deleted list
            setDeletedMovies(deletedMovies.filter(movie => movie.id !== id));
            toast.success("Movie restored successfully!");
        } catch (err) {
            console.error("Failed to restore movie:", err);
            setError(`Failed to restore movie: ${err.message}`);
        }
    };

    const handleApiError = async (response, action) => {
        if (response.status === 401) {
            toast.error("Unauthorized. Please check your admin permissions.");
            return;
        }

        if (response.status === 403) {
            toast.error(`Forbidden. You don't have permission to ${action} movies.`);
            return;
        }

        if (response.status === 404) {
            toast.error("Movie was not found on server. Refreshing view...");
            // Refresh the current view
            setViewMode(prevMode => prevMode);
            return;
        }

        // Handle other error status codes
        const errorText = await response.text();
        throw new Error(`Error ${response.status}: ${response.statusText} - ${errorText}`);
    };

    if (error) {
        return (
            <PageContainer size="md">
                <Alert variant="destructive">
                    <AlertCircle />
                    <AlertDescription>
                        <span>Error: {error}</span>
                        <Button size="sm" variant="outline" className="mt-2" onClick={() => window.location.reload()}>
                            <RefreshCw /> Refresh Page
                        </Button>
                    </AlertDescription>
                </Alert>
            </PageContainer>
        );
    }

    // Determine which movies to display based on view mode
    const displayedMovies = viewMode === "deleted" ? deletedMovies : movies;
    const isDeletedView = viewMode === "deleted";

    return (
        <PageContainer size="xl">
            <PageHeader title="Manage Movies" icon={Film}>
                <Button asChild>
                    <Link to="/admin/movies/add"><Plus />Add New Movie</Link>
                </Button>
            </PageHeader>

            {/* View selector tabs */}
            <div className="mb-6 inline-flex rounded-lg bg-muted p-1" role="tablist">
                {viewTabs.map(tab => (
                    <button
                        key={tab.key}
                        type="button"
                        role="tab"
                        aria-selected={viewMode === tab.key}
                        className={cn(
                            "cursor-pointer rounded-md px-3 py-1.5 text-sm font-medium transition-colors",
                            viewMode === tab.key ? "bg-background text-foreground shadow-sm dark:bg-input/40" : "text-muted-foreground hover:text-foreground"
                        )}
                        onClick={() => setViewMode(tab.key)}
                    >
                        {tab.label}
                    </button>
                ))}
            </div>

            {loading ? (
                <LoadingState label="Loading movies..." className="min-h-[30vh]" />
            ) : displayedMovies.length === 0 ? (
                <EmptyState
                    icon={Film}
                    title={isDeletedView ? "No deleted movies found." : viewMode === "active" ? "No active movies found." : "No movies found."}
                >
                    {viewMode === "active" && (
                        <Button asChild><Link to="/admin/movies/add">Add your first movie</Link></Button>
                    )}
                </EmptyState>
            ) : (
                <Card className="py-0">
                    <Table>
                        <TableHeader>
                            <TableRow className="hover:bg-transparent">
                                <TableHead className="w-14">ID</TableHead>
                                <TableHead className="w-16">Image</TableHead>
                                <TableHead>Title</TableHead>
                                <TableHead>Price</TableHead>
                                <TableHead>Discounted Price</TableHead>
                                {viewMode === "all" && <TableHead>Status</TableHead>}
                                <TableHead className="text-right">Actions</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {displayedMovies.map(movie => (
                                <TableRow key={movie.id} className={cn(movie.isDeleted && "bg-destructive/5")}>
                                    <TableCell className="text-muted-foreground">{movie.id}</TableCell>
                                    <TableCell>
                                        <img
                                            src={movie.imageUrl}
                                            alt={movie.title}
                                            className="h-16 w-11 rounded object-cover ring-1 ring-border"
                                            onError={(e) => {
                                                e.target.style.visibility = 'hidden';
                                            }}
                                        />
                                    </TableCell>
                                    <TableCell className="font-medium">{movie.title}</TableCell>
                                    <TableCell className="whitespace-nowrap">{formatPrice(movie.price)}</TableCell>
                                    <TableCell className="whitespace-nowrap">
                                        {movie.discountedPrice ? <span className="text-destructive">{formatPrice(movie.discountedPrice)}</span> : <span className="text-muted-foreground">-</span>}
                                    </TableCell>
                                    {viewMode === "all" && (
                                        <TableCell>
                                            <Badge variant={movie.isDeleted ? "danger" : "success"}>
                                                {movie.isDeleted ? "Deleted" : "Active"}
                                            </Badge>
                                        </TableCell>
                                    )}
                                    <TableCell>
                                        <div className="flex flex-wrap justify-end gap-1">
                                            {/* If in deleted view or it's a deleted movie in all view */}
                                            {(isDeletedView || (viewMode === "all" && movie.isDeleted)) ? (
                                                <Button variant="ghost" size="sm" className="text-success hover:text-success" onClick={() => handleRestoreMovie(movie.id)}>
                                                    <RotateCcw /> Restore
                                                </Button>
                                            ) : (
                                                <>
                                                    <Button variant="ghost" size="sm" asChild>
                                                        <Link to={`/admin/movies/edit/${movie.id}`}><Pencil /> Edit</Link>
                                                    </Button>
                                                    <Button variant="ghost" size="sm" asChild>
                                                        <Link to={`/admin/movies/categories/${movie.id}`}><Tags /> Categories</Link>
                                                    </Button>
                                                    <Button variant="ghost" size="sm" asChild>
                                                        <Link to={`/admin/movies/${movie.id}/video`}><FileVideo /> Video</Link>
                                                    </Button>
                                                    <Button
                                                        variant="ghost"
                                                        size="sm"
                                                        className="text-destructive hover:text-destructive"
                                                        onClick={() => handleSoftDeleteMovie(movie.id)}
                                                    >
                                                        <Trash2 /> Delete
                                                    </Button>
                                                </>
                                            )}
                                        </div>
                                    </TableCell>
                                </TableRow>
                            ))}
                        </TableBody>
                    </Table>
                </Card>
            )}
        </PageContainer>
    );
};

export default AdminMovies;
