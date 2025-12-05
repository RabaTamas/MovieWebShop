import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext"
import API_BASE_URL from '../config/api';

const MovieForm = () => {
    const { id } = useParams();
    const navigate = useNavigate();
    const { token } = useAuth();
    const isEditMode = !!id;

    const [formData, setFormData] = useState({
        title: "",
        description: "",
        price: 0,
        discountedPrice: null,
        imageUrl: "",
        categories: []
    });

    const [allCategories, setAllCategories] = useState([]);
    const [loading, setLoading] = useState(isEditMode);
    const [error, setError] = useState(null);
    const [submitting, setSubmitting] = useState(false);

    // Fetch categories when component mounts
    useEffect(() => {
        const fetchCategories = async () => {
            try {
                const response = await fetch('${API_BASE_URL}/api/category', {
                    headers: {
                        'Authorization': `Bearer ${token}`
                    }
                });

                if (!response.ok) {
                    throw new Error(`Error ${response.status}: ${response.statusText}`);
                }

                const data = await response.json();
                setAllCategories(data);
            } catch (err) {
                console.error("Failed to fetch categories:", err);
                setError(err.message);
            }
        };

        fetchCategories();
    }, [token]);

    // If in edit mode, fetch movie data
    useEffect(() => {
        const fetchMovie = async () => {
            if (!isEditMode) return;

            try {
                const response = await fetch(`${API_BASE_URL}/api/movie/${id}`, {
                    headers: {
                        'Authorization': `Bearer ${token}`
                    }
                });

                if (!response.ok) {
                    throw new Error(`Error ${response.status}: ${response.statusText}`);
                }

                const data = await response.json();
                setFormData({
                    id: data.id,
                    title: data.title,
                    description: data.description,
                    price: data.price,
                    discountedPrice: data.discountedPrice,
                    imageUrl: data.imageUrl,
                    categories: data.categories || []
                });
                setLoading(false);
            } catch (err) {
                console.error("Failed to fetch movie details:", err);
                setError(err.message);
                setLoading(false);
            }
        };

        fetchMovie();
    }, [id, isEditMode, token]);

    const handleInputChange = (e) => {
        const { name, value, type } = e.target;

        setFormData(prev => ({
            ...prev,
            [name]: type === 'number' ? (value === "" ? null : parseInt(value, 10)) : value
        }));
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        setSubmitting(true);
        setError(null);

        try {
            const movieData = { ...formData };
            if (!isEditMode) {
                // Not sending categories for new movie - they'll be added separately
                delete movieData.categories;
            }

            const response = await fetch(
                isEditMode ? `${API_BASE_URL}/api/movie/${id}` : '${API_BASE_URL}/api/movie',
                {
                    method: isEditMode ? 'PUT' : 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'Authorization': `Bearer ${token}`
                    },
                    body: JSON.stringify(movieData)
                }
            );

            if (!response.ok) {
                // Try to get error message from response
                let errorMsg = `Error ${response.status}: ${response.statusText}`;
                try {
                    const errorData = await response.json();
                    if (errorData.message) {
                        errorMsg = errorData.message;
                    }
                } catch (e) {
                    // If we can't parse JSON, just use the default error message
                }
                throw new Error(errorMsg);
            }

            // Redirect to movies list
            navigate('/admin/movies');
        } catch (err) {
            console.error("Failed to save movie:", err);
            setError(err.message);
            setSubmitting(false);
        }
    };

    if (loading) {
        return <div className="container mt-4"><div className="spinner-border" role="status"></div> Loading movie data...</div>;
    }

    return (
        <div className="container mt-4">
            <h1>{isEditMode ? 'Edit Movie' : 'Add New Movie'}</h1>

            {error && <div className="alert alert-danger">{error}</div>}

            <form onSubmit={handleSubmit} className="mt-4">
                <div className="mb-3">
                    <label htmlFor="title" className="form-label">Title *</label>
                    <input
                        type="text"
                        className="form-control"
                        id="title"
                        name="title"
                        value={formData.title}
                        onChange={handleInputChange}
                        required
                    />
                </div>

                <div className="mb-3">
                    <label htmlFor="description" className="form-label">Description *</label>
                    <textarea
                        className="form-control"
                        id="description"
                        name="description"
                        rows="4"
                        value={formData.description}
                        onChange={handleInputChange}
                        required
                    ></textarea>
                </div>

                <div className="row mb-3">
                    <div className="col-md-6">
                        <label htmlFor="price" className="form-label">Price (in USD) *</label>
                        <input
                            type="number"
                            className="form-control"
                            id="price"
                            name="price"
                            value={formData.price}
                            onChange={handleInputChange}
                            min="0"
                            required
                        />
                    </div>

                    <div className="col-md-6">
                        <label htmlFor="discountedPrice" className="form-label">Discounted Price (optional)</label>
                        <input
                            type="number"
                            className="form-control"
                            id="discountedPrice"
                            name="discountedPrice"
                            value={formData.discountedPrice || ""}
                            onChange={handleInputChange}
                            min="0"
                        />
                    </div>
                </div>

                <div className="mb-3">
                    <label htmlFor="imageUrl" className="form-label">Image URL *</label>
                    <input
                        type="url"
                        className="form-control"
                        id="imageUrl"
                        name="imageUrl"
                        value={formData.imageUrl}
                        onChange={handleInputChange}
                        required
                    />
                    {formData.imageUrl && (
                        <div className="mt-2">
                            <p>Image Preview:</p>
                            <img
                                src={formData.imageUrl}
                                alt="Movie preview"
                                style={{ maxHeight: '200px' }}
                                className="img-thumbnail"
                            />
                        </div>
                    )}
                </div>

                <div className="d-flex justify-content-between mt-4">
                    <button
                        type="button"
                        className="btn btn-secondary"
                        onClick={() => navigate('/admin/movies')}
                    >
                        Cancel
                    </button>
                    <button
                        type="submit"
                        className="btn btn-primary"
                        disabled={submitting}
                    >
                        {submitting ? (
                            <>
                                <span className="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span>
                                Saving...
                            </>
                        ) : (
                            'Save Movie'
                        )}
                    </button>
                </div>
            </form>
        </div>
    );
};

export default MovieForm;