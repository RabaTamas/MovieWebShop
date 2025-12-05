import { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";

import API_BASE_URL from "../config/api";

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
                        endpoint = "${API_BASE_URL}/movie/admin/deleted";
                        break;
                    case "all":
                        endpoint = "${API_BASE_URL}/api/movie/admin/all";
                        break;
                    default:
                        endpoint = "${API_BASE_URL}/api/movie"; // Active movies only
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
            alert("Movie deleted successfully. You can find it in the 'Deleted Movies' view.");
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
            alert("Movie restored successfully!");
        } catch (err) {
            console.error("Failed to restore movie:", err);
            setError(`Failed to restore movie: ${err.message}`);
        }
    };

    const handleApiError = async (response, action) => {
        if (response.status === 401) {
            alert("Unauthorized. Please check your admin permissions.");
            return;
        }

        if (response.status === 403) {
            alert(`Forbidden. You don't have permission to ${action} movies.`);
            return;
        }

        if (response.status === 404) {
            alert("Movie was not found on server. Refreshing view...");
            // Refresh the current view
            setViewMode(prevMode => prevMode);
            return;
        }

        // Handle other error status codes
        const errorText = await response.text();
        throw new Error(`Error ${response.status}: ${response.statusText} - ${errorText}`);
    };

    if (loading) {
        return <div className="container mt-4"><div className="spinner-border" role="status"></div> Loading movies...</div>;
    }

    if (error) {
        return (
            <div className="container mt-4">
                <div className="alert alert-danger">
                    Error: {error}
                    <button
                        className="btn btn-sm btn-outline-secondary ms-2"
                        onClick={() => window.location.reload()}
                    >
                        Refresh Page
                    </button>
                </div>
            </div>
        );
    }

    // Determine which movies to display based on view mode
    const displayedMovies = viewMode === "deleted" ? deletedMovies : movies;
    const isDeletedView = viewMode === "deleted";

    return (
        <div className="container mt-4">
            <div className="d-flex justify-content-between align-items-center mb-4">
                <h1>Manage Movies</h1>
                <Link to="/admin/movies/add" className="btn btn-primary">
                    <i className="bi bi-plus-circle me-2"></i>Add New Movie
                </Link>
            </div>

            {/* View selector tabs */}
            <ul className="nav nav-tabs mb-4">
                <li className="nav-item">
                    <button
                        className={`nav-link ${viewMode === "active" ? "active" : ""}`}
                        onClick={() => setViewMode("active")}
                    >
                        Active Movies
                    </button>
                </li>
                <li className="nav-item">
                    <button
                        className={`nav-link ${viewMode === "deleted" ? "active" : ""}`}
                        onClick={() => setViewMode("deleted")}
                    >
                        Deleted Movies
                    </button>
                </li>
                <li className="nav-item">
                    <button
                        className={`nav-link ${viewMode === "all" ? "active" : ""}`}
                        onClick={() => setViewMode("all")}
                    >
                        All Movies
                    </button>
                </li>
            </ul>

            {displayedMovies.length === 0 ? (
                <div className="alert alert-info">
                    {isDeletedView
                        ? "No deleted movies found."
                        : viewMode === "active"
                            ? <>No active movies found. <Link to="/admin/movies/add">Add your first movie</Link></>
                            : "No movies found."
                    }
                </div>
            ) : (
                <div className="table-responsive">
                    <table className="table table-striped table-hover">
                        <thead>
                            <tr>
                                <th>ID</th>
                                <th>Image</th>
                                <th>Title</th>
                                <th>Price</th>
                                <th>Discounted Price</th>
                                {viewMode === "all" && <th>Status</th>}
                                <th>Actions</th>
                            </tr>
                        </thead>
                        <tbody>
                            {displayedMovies.map(movie => (
                                <tr key={movie.id} className={movie.isDeleted ? "table-danger" : ""}>
                                    <td>{movie.id}</td>
                                    <td>
                                        <img
                                            src={movie.imageUrl}
                                            alt={movie.title}
                                            style={{ width: '50px', height: '70px', objectFit: 'cover' }}
                                            onError={(e) => {
                                                e.target.src = '/api/placeholder/50/70';
                                            }}
                                        />
                                    </td>
                                    <td>{movie.title}</td>
                                    <td>{movie.price} Ft</td>
                                    <td>{movie.discountedPrice ? `${movie.discountedPrice} Ft` : '-'}</td>
                                    {viewMode === "all" && (
                                        <td>
                                            <span className={`badge ${movie.isDeleted ? "bg-danger" : "bg-success"}`}>
                                                {movie.isDeleted ? "Deleted" : "Active"}
                                            </span>
                                        </td>
                                    )}
                                    <td>
                                        <div className="btn-group">
                                            {/* If in deleted view or it's a deleted movie in all view */}
                                            {(isDeletedView || (viewMode === "all" && movie.isDeleted)) ? (
                                                // Actions for deleted movies
                                                <>
                                                    <button
                                                        className="btn btn-sm btn-outline-success"
                                                        onClick={() => handleRestoreMovie(movie.id)}
                                                    >
                                                        <i className="bi bi-arrow-counterclockwise"></i> Restore
                                                    </button>
                                                    
                                                </>
                                            ) : (
                                                // Actions for active movies
                                                <>
                                                    <Link to={`/admin/movies/edit/${movie.id}`} className="btn btn-sm btn-outline-primary">
                                                        <i className="bi bi-pencil"></i> Edit
                                                    </Link>
                                                    <button
                                                        className="btn btn-sm btn-outline-danger"
                                                        onClick={() => handleSoftDeleteMovie(movie.id)}
                                                    >
                                                        <i className="bi bi-trash"></i> Delete
                                                    </button>
                                                    <Link to={`/admin/movies/categories/${movie.id}`} className="btn btn-sm btn-outline-secondary">
                                                        <i className="bi bi-tags"></i> Categories
                                                    </Link>
                                                </>
                                            )}
                                        </div>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}
        </div>
    );
};

export default AdminMovies;