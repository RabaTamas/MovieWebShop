import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";

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
                const movieResponse = await fetch(`https://localhost:7289/api/movie/${id}`, {
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
                const categoriesResponse = await fetch('https://localhost:7289/api/category', {
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

            const response = await fetch(`https://localhost:7289/api/movie/${id}/category/${categoryId}`, {
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
        return <div className="container mt-4"><div className="spinner-border" role="status"></div> Loading data...</div>;
    }

    if (error) {
        return <div className="container mt-4 alert alert-danger">Error: {error}</div>;
    }

    if (!movie) {
        return <div className="container mt-4 alert alert-warning">Movie not found</div>;
    }

    return (
        <div className="container mt-4">
            <div className="d-flex justify-content-between align-items-center mb-4">
                <h1>Manage Categories for "{movie.title}"</h1>
                <button
                    className="btn btn-secondary"
                    onClick={() => navigate('/admin/movies')}
                >
                    Back to Movies
                </button>
            </div>

            {success && (
                <div className="alert alert-success">Category assignment updated successfully!</div>
            )}

            {error && (
                <div className="alert alert-danger">{error}</div>
            )}

            <div className="row">
                <div className="col-md-6">
                    <div className="card mb-4">
                        <div className="card-header">
                            <h3 className="card-title">Movie Details</h3>
                        </div>
                        <div className="card-body">
                            <div className="d-flex">
                                <div className="me-3">
                                    <img
                                        src={movie.imageUrl}
                                        alt={movie.title}
                                        className="img-thumbnail"
                                        style={{ width: '100px', height: '140px', objectFit: 'cover' }}
                                    />
                                </div>
                                <div>
                                    <h5>{movie.title}</h5>
                                    <p><strong>Price:</strong> ${movie.price}</p>
                                    {movie.discountedPrice && (
                                        <p><strong>Discounted Price:</strong> ${movie.discountedPrice}</p>
                                    )}
                                </div>
                            </div>
                        </div>
                    </div>

                    <div className="card">
                        <div className="card-header">
                            <h3 className="card-title">Current Categories</h3>
                        </div>
                        <div className="card-body">
                            {movie.categories && movie.categories.length > 0 ? (
                                <div className="d-flex flex-wrap gap-2">
                                    {movie.categories.map(category => (
                                        <span key={category.id} className="badge bg-primary">{category.name}</span>
                                    ))}
                                </div>
                            ) : (
                                <p className="text-muted">No categories assigned</p>
                            )}
                        </div>
                    </div>
                </div>

                <div className="col-md-6">
                    <div className="card">
                        <div className="card-header">
                            <h3 className="card-title">All Categories</h3>
                        </div>
                        <div className="card-body">
                            <div className="list-group">
                                {allCategories.map(category => (
                                    <button
                                        key={category.id}
                                        className={`list-group-item list-group-item-action d-flex justify-content-between align-items-center ${isMovieCategoryAssigned(category.id) ? 'active' : ''}`}
                                        onClick={() => handleCategoryToggle(category.id)}
                                        disabled={saving}
                                    >
                                        {category.name}
                                        {isMovieCategoryAssigned(category.id) ? (
                                            <i className="bi bi-check-circle-fill"></i>
                                        ) : (
                                            <i className="bi bi-plus-circle"></i>
                                        )}
                                    </button>
                                ))}
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default MovieCategories;