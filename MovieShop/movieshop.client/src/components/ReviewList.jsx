import { useEffect, useState } from "react";
import { useAuth } from "../contexts/AuthContext";
import { useNavigate } from "react-router-dom";
import API_BASE_URL from '../config/api';

const ReviewList = ({ movieId }) => {
    const [reviews, setReviews] = useState([]);
    const [isExpanded, setIsExpanded] = useState(false);
    const [newReview, setNewReview] = useState("");
    const [isAdding, setIsAdding] = useState(false);
    const [editingReview, setEditingReview] = useState(null);
    const [editContent, setEditContent] = useState("");
    const { user, token } = useAuth();
    const navigate = useNavigate();

    const fetchReviews = async () => {
        try {
            const res = await fetch(`${API_BASE_URL}/api/Review/movie/${movieId}`);
            if (!res.ok) throw new Error("Failed to fetch reviews");
            const data = await res.json();
            setReviews(data);
        } catch (err) {
            console.error(err);
        }
    };

    // Fetch reviews on component mount to get the count
    useEffect(() => {
        fetchReviews();
    }, [movieId]);

    // Fetch reviews again when expanded
    useEffect(() => {
        if (isExpanded) {
            fetchReviews();
        }
    }, [isExpanded, movieId]);

    const handleAddReview = async () => {
        if (!user) {
            navigate("/login");
            return;
        }

        if (!newReview.trim()) return;

        try {
            const res = await fetch(`${API_BASE_URL}/api/Review/movie/${movieId}`, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    "Authorization": `Bearer ${token}`
                },
                body: JSON.stringify({ content: newReview })
            });

            if (res.ok) {
                setNewReview("");
                setIsAdding(false);
                fetchReviews();
            } else {
                const data = await res.text();
                alert(data || "Failed to add review");
            }
        } catch (err) {
            console.error(err);
            alert("Something went wrong while adding review.");
        }
    };

    const handleEditReview = (review) => {
        setEditingReview(review);
        setEditContent(review.content);
    };

    const handleUpdateReview = async () => {
        if (!editContent.trim() || !editingReview) return;

        try {
            const res = await fetch(`${API_BASE_URL}/api/Review/${editingReview.id}`, {
                method: "PUT",
                headers: {
                    "Content-Type": "application/json",
                    "Authorization": `Bearer ${token}`
                },
                body: JSON.stringify({ content: editContent })
            });

            if (res.ok) {
                setEditingReview(null);
                setEditContent("");
                fetchReviews();
            } else {
                const data = await res.text();
                alert(data || "Failed to update review");
            }
        } catch (err) {
            console.error(err);
            alert("Something went wrong while updating review.");
        }
    };

    const handleDeleteReview = async (reviewId) => {
        if (!confirm("Are you sure you want to delete this review?")) return;

        try {
            const res = await fetch(`${API_BASE_URL}/api/Review/${reviewId}`, {
                method: "DELETE",
                headers: {
                    "Authorization": `Bearer ${token}`
                }
            });

            if (res.ok) {
                fetchReviews();
            } else {
                const data = await res.text();
                alert(data || "Failed to delete review");
            }
        } catch (err) {
            console.error(err);
            alert("Something went wrong while deleting review.");
        }
    };

    // Fixed ownership checking function
    const isUserReview = (review) => {
        console.log('=== OWNERSHIP CHECK DEBUG ===');
        console.log('User object:', user);
        console.log('User ID from context:', user?.id);
        console.log('Review UserId:', review.userId);
        console.log('User logged in:', !!user);

        if (!user) {
            console.log('No user logged in');
            return false;
        }

        // First try to compare by User ID (most reliable)
        if (user.id && review.userId) {
            const currentUserId = user.id;
            const reviewUserId = review.userId;

            console.log('Comparing User IDs:', currentUserId, 'vs', reviewUserId);
            const matches = currentUserId === reviewUserId;
            console.log('User IDs match:', matches);
            console.log('=============================');

            return matches;
        }

        // Fallback to username comparison if User ID is not available
        const currentUserName = user.name || user.userName || user.Name || user.UserName;
        const reviewUserName = review.userName || review.UserName;

        console.log('Fallback to username comparison:', currentUserName, 'vs', reviewUserName);
        const matches = currentUserName === reviewUserName;
        console.log('Names match:', matches);
        console.log('=============================');

        return matches;
    };
    const cancelEdit = () => {
        setEditingReview(null);
        setEditContent("");
    };

    const getInitials = (name) => {
        if (!name || typeof name !== 'string') return 'U';
        return name.split(' ').map(n => n[0]).join('').toUpperCase().slice(0, 2);
    };

    const getRandomGradient = (name) => {
        if (!name || typeof name !== 'string') {
            return 'linear-gradient(135deg, #64748b, #475569)'; // Default gradient
        }

        const gradients = [
            'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
            'linear-gradient(135deg, #f093fb 0%, #f5576c 100%)',
            'linear-gradient(135deg, #4facfe 0%, #00f2fe 100%)',
            'linear-gradient(135deg, #43e97b 0%, #38f9d7 100%)',
            'linear-gradient(135deg, #fa709a 0%, #fee140 100%)',
            'linear-gradient(135deg, #a8edea 0%, #fed6e3 100%)',
            'linear-gradient(135deg, #ffecd2 0%, #fcb69f 100%)',
            'linear-gradient(135deg, #ff8a80 0%, #ea4c46 100%)'
        ];
        const index = name.charCodeAt(0) % gradients.length;
        return gradients[index];
    };

    return (
        <div className="mt-5 bg-white rounded-4 shadow-sm border p-4">
            {/* Header Section */}
            <div className="d-flex align-items-center justify-content-between mb-4">
                <div className="d-flex align-items-center gap-3">
                    <div
                        className="d-flex align-items-center justify-content-center rounded-circle"
                        style={{
                            width: '48px',
                            height: '48px',
                            background: 'linear-gradient(135deg, #3b82f6, #1d4ed8)',
                            color: 'white'
                        }}
                    >
                        <i className="bi-chat-dots fs-5"></i>
                    </div>
                    <div>
                        <h3 className="mb-1 fw-bold text-dark">Customer Reviews</h3>
                        <small className="text-muted">
                            {reviews.length === 0 ? 'No reviews yet' : `${reviews.length} review${reviews.length !== 1 ? 's' : ''}`}
                        </small>
                    </div>
                </div>

                <button
                    className="btn px-4 py-2 fw-semibold"
                    onClick={() => setIsExpanded(!isExpanded)}
                    style={{
                        background: isExpanded
                            ? 'linear-gradient(135deg, #ef4444, #dc2626)'
                            : 'linear-gradient(135deg, #10b981, #059669)',
                        border: 'none',
                        color: 'white',
                        borderRadius: '10px',
                        transition: 'all 0.3s ease'
                    }}
                >
                    <i className={`bi-chevron-${isExpanded ? 'up' : 'down'} me-2`}></i>
                    {isExpanded ? "Hide Reviews" : "Show Reviews"}
                </button>
            </div>

            {/* Reviews Content */}
            {isExpanded && (
                <div
                    className="rounded-3 p-4"
                    style={{
                        background: 'linear-gradient(135deg, #f8fafc, #e2e8f0)',
                        maxHeight: "500px",
                        overflowY: "auto"
                    }}
                >
                    {reviews.length === 0 ? (
                        <div className="text-center py-5">
                            <div
                                className="d-flex align-items-center justify-content-center rounded-circle mx-auto mb-3"
                                style={{
                                    width: '80px',
                                    height: '80px',
                                    background: 'linear-gradient(135deg, #e2e8f0, #cbd5e1)',
                                    color: '#64748b'
                                }}
                            >
                                <i className="bi-chat-text fs-2"></i>
                            </div>
                            <h5 className="text-muted mb-2">No reviews yet</h5>
                            <p className="text-muted small">Be the first to share your thoughts about this movie!</p>
                        </div>
                    ) : (
                        <div className="row g-3">
                            {reviews.map((review) => (
                                <div key={review.id} className="col-12">
                                    <div
                                        className="bg-white rounded-3 p-4 shadow-sm border position-relative"
                                        style={{
                                            transition: 'all 0.3s ease',
                                            borderLeft: '4px solid transparent',
                                            borderImage: getRandomGradient(review.userName),
                                            borderImageSlice: 1
                                        }}
                                    >
                                        {/* Review Header */}
                                        <div className="d-flex align-items-start justify-content-between mb-3">
                                            <div className="d-flex align-items-center gap-3">
                                                <div
                                                    className="d-flex align-items-center justify-content-center rounded-circle fw-bold"
                                                    style={{
                                                        width: '45px',
                                                        height: '45px',
                                                        background: getRandomGradient(review.userName),
                                                        color: 'white',
                                                        fontSize: '0.9rem'
                                                    }}
                                                >
                                                    {getInitials(review.userName)}
                                                </div>
                                                <div>
                                                    <h6 className="mb-1 fw-bold text-dark">{review.userName}</h6>
                                                    <small className="text-muted d-flex align-items-center">
                                                        <i className="bi-clock me-1"></i>
                                                        {new Date(review.createdAt).toLocaleDateString('en-US', {
                                                            year: 'numeric',
                                                            month: 'short',
                                                            day: 'numeric',
                                                            hour: '2-digit',
                                                            minute: '2-digit'
                                                        })}
                                                    </small>
                                                    

                                                    
                                                </div>
                                            </div>

                                            {/* Action Buttons - Show for user's own reviews */}
                                            {isUserReview(review) && (
                                                <div className="d-flex gap-2">
                                                    <button
                                                        className="btn btn-sm px-3 py-1"
                                                        onClick={() => handleEditReview(review)}
                                                        style={{
                                                            background: 'linear-gradient(135deg, #3b82f6, #1d4ed8)',
                                                            border: 'none',
                                                            color: 'white',
                                                            borderRadius: '8px'
                                                        }}
                                                    >
                                                        <i className="bi-pencil"></i>
                                                    </button>
                                                    <button
                                                        className="btn btn-sm px-3 py-1"
                                                        onClick={() => handleDeleteReview(review.id)}
                                                        style={{
                                                            background: 'linear-gradient(135deg, #ef4444, #dc2626)',
                                                            border: 'none',
                                                            color: 'white',
                                                            borderRadius: '8px'
                                                        }}
                                                    >   
                                                        <i className="bi-trash"></i>
                                                    </button>
                                                </div>
                                            )}
                                        </div>

                                        {/* Review Content */}
                                        {editingReview && editingReview.id === review.id ? (
                                            <div>
                                                <textarea
                                                    className="form-control border-0 shadow-sm"
                                                    rows="4"
                                                    value={editContent}
                                                    onChange={(e) => setEditContent(e.target.value)}
                                                    style={{
                                                        borderRadius: '12px',
                                                        background: '#f8fafc',
                                                        fontSize: '1rem',
                                                        resize: 'vertical'
                                                    }}
                                                    placeholder="Update your review..."
                                                />
                                                <div className="mt-3 d-flex gap-2">
                                                    <button
                                                        className="btn px-4 py-2 fw-semibold"
                                                        onClick={handleUpdateReview}
                                                        style={{
                                                            background: 'linear-gradient(135deg, #10b981, #059669)',
                                                            border: 'none',
                                                            color: 'white',
                                                            borderRadius: '10px'
                                                        }}
                                                    >
                                                        <i className="bi-check-lg me-2"></i>Save Changes
                                                    </button>
                                                    <button
                                                        className="btn btn-outline-secondary px-4 py-2 fw-semibold"
                                                        onClick={cancelEdit}
                                                        style={{ borderRadius: '10px' }}
                                                    >
                                                        Cancel
                                                    </button>
                                                </div>
                                            </div>
                                        ) : (
                                            <div className="ps-4 border-start border-3" style={{ borderColor: '#e2e8f0' }}>
                                                <p className="mb-0 text-dark lh-lg" style={{ fontSize: '1.05rem' }}>
                                                    {review.content}
                                                </p>
                                            </div>
                                        )}
                                    </div>
                                </div>
                            ))}
                        </div>
                    )}

                    {/* Add Review Section */}
                    {user && !isAdding && !editingReview && (
                        <div className="text-center mt-4 pt-4 border-top">
                            <button
                                className="btn btn-lg px-5 py-3 fw-semibold"
                                onClick={() => setIsAdding(true)}
                                style={{
                                    background: 'linear-gradient(135deg, #8b5cf6, #7c3aed)',
                                    border: 'none',
                                    color: 'white',
                                    borderRadius: '12px',
                                    transition: 'all 0.3s ease'
                                }}
                            >
                                <i className="bi-plus-circle me-2"></i>
                                Write a Review
                            </button>
                        </div>
                    )}

                    {/* Add Review Form */}
                    {isAdding && (
                        <div className="mt-4 p-4 bg-white rounded-3 shadow-sm border">
                            <div className="d-flex align-items-center gap-3 mb-3">
                                <div
                                    className="d-flex align-items-center justify-content-center rounded-circle fw-bold"
                                    style={{
                                        width: '45px',
                                        height: '45px',
                                        background: getRandomGradient(user?.name || user?.userName || 'User'),
                                        color: 'white'
                                    }}
                                >
                                    {getInitials(user?.name || user?.userName || 'User')}
                                </div>
                                <div>
                                    <h6 className="mb-0 fw-bold text-dark">{user?.name || user?.userName || 'User'}</h6>
                                    <small className="text-muted">Writing a review...</small>
                                </div>
                            </div>

                            <textarea
                                className="form-control border-0 shadow-sm mb-3"
                                rows="4"
                                value={newReview}
                                onChange={(e) => setNewReview(e.target.value)}
                                placeholder="Share your thoughts about this movie..."
                                style={{
                                    borderRadius: '12px',
                                    background: '#f8fafc',
                                    fontSize: '1rem',
                                    resize: 'vertical'
                                }}
                            />

                            <div className="d-flex gap-2">
                                <button
                                    className="btn px-4 py-2 fw-semibold"
                                    onClick={handleAddReview}
                                    disabled={!newReview.trim()}
                                    style={{
                                        background: newReview.trim()
                                            ? 'linear-gradient(135deg, #10b981, #059669)'
                                            : '#e2e8f0',
                                        border: 'none',
                                        color: newReview.trim() ? 'white' : '#64748b',
                                        borderRadius: '10px',
                                        transition: 'all 0.3s ease'
                                    }}
                                >
                                    <i className="bi-send me-2"></i>Submit Review
                                </button>
                                <button
                                    className="btn btn-outline-secondary px-4 py-2 fw-semibold"
                                    onClick={() => {
                                        setIsAdding(false);
                                        setNewReview("");
                                    }}
                                    style={{ borderRadius: '10px' }}
                                >
                                    Cancel
                                </button>
                            </div>
                        </div>
                    )}

                    {/* Login Prompt for Non-Users */}
                    {!user && (
                        <div className="text-center mt-4 pt-4 border-top">
                            <div
                                className="d-inline-flex align-items-center gap-2 px-4 py-3 rounded-3"
                                style={{ background: 'rgba(59, 130, 246, 0.1)' }}
                            >
                                <i className="bi-info-circle text-primary"></i>
                                <span className="text-muted">
                                    <button
                                        className="btn btn-link p-0 fw-semibold text-primary"
                                        onClick={() => navigate("/login")}
                                        style={{ textDecoration: 'none' }}
                                    >
                                        Sign in
                                    </button>
                                    {" "}to write a review
                                </span>
                            </div>
                        </div>
                    )}
                </div>
            )}
        </div>
    );
};

export default ReviewList;