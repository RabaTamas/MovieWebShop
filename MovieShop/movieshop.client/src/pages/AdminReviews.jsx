import { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";

import API_BASE_URL from "../config/api";

const AdminReviews = () => {
    const { token } = useAuth();
    const [reviews, setReviews] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);

    // Fetch all reviews when component mounts
    useEffect(() => {
        const fetchReviews = async () => {
            try {
                const response = await fetch(`${API_BASE_URL}/api/review`, {
                    headers: {
                        'Authorization': `Bearer ${token}`
                    }
                });

                if (!response.ok) {
                    throw new Error(`Error ${response.status}: ${response.statusText}`);
                }

                const data = await response.json();
                setReviews(data);
                setLoading(false);
            } catch (err) {
                console.error("Failed to fetch reviews:", err);
                setError(err.message);
                setLoading(false);
            }
        };

        if (token) {
            fetchReviews();
        } else {
            setError("Authentication token is missing");
            setLoading(false);
        }
    }, [token]);

    const handleDeleteReview = async (reviewId) => {
        if (!window.confirm("Are you sure you want to delete this review?")) {
            return;
        }

        try {
            const response = await fetch(`${API_BASE_URL}/api/review/${reviewId}`, {
                method: 'DELETE',
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });

            if (!response.ok) {
                throw new Error(`Error ${response.status}: ${response.statusText}`);
            }

            // Remove the deleted review from the state
            setReviews(reviews.filter(review => review.id !== reviewId));
        } catch (err) {
            console.error("Failed to delete review:", err);
            setError(err.message);
        }
    };

    if (loading) {
        return <div className="container mt-4"><div className="spinner-border" role="status"></div> Loading reviews...</div>;
    }

    if (error) {
        return <div className="container mt-4 alert alert-danger">Error: {error}</div>;
    }

    return (
        <div className="container mt-4">
            <h1 className="mb-4">Manage Reviews</h1>
            <p className="text-muted mb-4">As an admin, you can only delete reviews. Users can create and edit their own reviews.</p>

            <div className="table-responsive">
                <table className="table table-striped table-hover">
                    <thead>
                        <tr>
                            <th>ID</th>
                            <th>Movie</th>
                            <th>User</th>
                            <th>Content</th>
                            <th>Date</th>
                            <th>Actions</th>
                        </tr>
                    </thead>
                    <tbody>
                        {reviews.length === 0 ? (
                            <tr>
                                <td colSpan="6" className="text-center">No reviews found</td>
                            </tr>
                        ) : (
                            reviews.map(review => (
                                <tr key={review.id}>
                                    <td>{review.id}</td>
                                    <td>
                                        {review.movie ? (
                                            <Link to={`/movies/${review.movie.id}`}>
                                                {review.movie.title}
                                            </Link>
                                        ) : (
                                            <span className="text-muted">Movie not found</span>
                                        )}
                                    </td>
                                    <td>{review.user ? review.user.name : 'Unknown User'}</td>
                                    <td>
                                        <div className="review-content">
                                            {review.content.length > 100
                                                ? `${review.content.substring(0, 100)}...`
                                                : review.content
                                            }
                                        </div>
                                    </td>
                                    <td>{new Date(review.createdAt).toLocaleDateString()}</td>
                                    <td>
                                        <button
                                            className="btn btn-sm btn-danger"
                                            onClick={() => handleDeleteReview(review.id)}
                                            title="Delete review"
                                        >
                                            <i className="bi bi-trash"></i> Delete
                                        </button>
                                    </td>
                                </tr>
                            ))
                        )}
                    </tbody>
                </table>
            </div>
        </div>
    );
};

export default AdminReviews;