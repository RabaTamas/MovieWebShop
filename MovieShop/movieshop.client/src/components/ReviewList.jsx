import { useEffect, useState } from "react";
import { useAuth } from "../contexts/AuthContext";
import { useNavigate } from "react-router-dom";
import { toast } from "sonner";
import { Check, ChevronDown, ChevronUp, Clock, Info, MessageSquareText, Pencil, PenLine, Send, Trash2 } from "lucide-react";
import API_BASE_URL from "../config/api";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Textarea } from "@/components/ui/textarea";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";

const avatarTones = [
    "bg-amber-500/20 text-amber-500",
    "bg-rose-500/20 text-rose-500",
    "bg-sky-500/20 text-sky-500",
    "bg-emerald-500/20 text-emerald-500",
    "bg-violet-500/20 text-violet-500",
    "bg-orange-500/20 text-orange-500",
    "bg-teal-500/20 text-teal-500",
    "bg-fuchsia-500/20 text-fuchsia-500",
];

const getInitials = (name) => {
    if (!name || typeof name !== 'string') return 'U';
    return name.split(' ').map(n => n[0]).join('').toUpperCase().slice(0, 2);
};

const getAvatarTone = (name) => {
    if (!name || typeof name !== 'string') return "bg-muted text-muted-foreground";
    return avatarTones[name.charCodeAt(0) % avatarTones.length];
};

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
                toast.error(data || "Failed to add review");
            }
        } catch (err) {
            console.error(err);
            toast.error("Something went wrong while adding review.");
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
                toast.error(data || "Failed to update review");
            }
        } catch (err) {
            console.error(err);
            toast.error("Something went wrong while updating review.");
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
                toast.error(data || "Failed to delete review");
            }
        } catch (err) {
            console.error(err);
            toast.error("Something went wrong while deleting review.");
        }
    };

    // Ownership check: user ID first (most reliable), username as fallback
    const isUserReview = (review) => {
        if (!user) return false;

        if (user.id && review.userId) {
            return user.id === review.userId;
        }

        const currentUserName = user.name || user.userName || user.Name || user.UserName;
        const reviewUserName = review.userName || review.UserName;
        return currentUserName === reviewUserName;
    };

    const cancelEdit = () => {
        setEditingReview(null);
        setEditContent("");
    };

    const currentUserName = user?.name || user?.userName || 'User';

    return (
        <Card className="gap-0 overflow-hidden py-0">
            {/* Header Section */}
            <div className="flex flex-col gap-4 p-6 sm:flex-row sm:items-center sm:justify-between">
                <div className="flex items-center gap-3">
                    <div className="flex size-11 items-center justify-center rounded-full bg-primary/15 text-primary">
                        <MessageSquareText className="size-5" />
                    </div>
                    <div>
                        <h2 className="text-xl font-bold">Customer Reviews</h2>
                        <p className="text-sm text-muted-foreground">
                            {reviews.length === 0 ? 'No reviews yet' : `${reviews.length} review${reviews.length !== 1 ? 's' : ''}`}
                        </p>
                    </div>
                </div>

                <Button variant={isExpanded ? "outline" : "secondary"} onClick={() => setIsExpanded(!isExpanded)}>
                    {isExpanded ? <ChevronUp /> : <ChevronDown />}
                    {isExpanded ? "Hide Reviews" : "Show Reviews"}
                </Button>
            </div>

            {/* Reviews Content */}
            {isExpanded && (
                <div className="border-t bg-muted/30 p-4 sm:p-6">
                    <div className="max-h-[500px] space-y-3 overflow-y-auto pr-1">
                        {reviews.length === 0 ? (
                            <div className="py-10 text-center">
                                <div className="mx-auto mb-3 flex size-16 items-center justify-center rounded-full bg-muted">
                                    <MessageSquareText className="size-7 text-muted-foreground" />
                                </div>
                                <h3 className="font-semibold">No reviews yet</h3>
                                <p className="text-sm text-muted-foreground">Be the first to share your thoughts about this movie!</p>
                            </div>
                        ) : (
                            reviews.map((review) => (
                                <div key={review.id} className="rounded-xl border bg-card p-4 sm:p-5">
                                    {/* Review Header */}
                                    <div className="mb-3 flex items-start justify-between gap-3">
                                        <div className="flex items-center gap-3">
                                            <Avatar className="size-10">
                                                <AvatarFallback className={cn("text-sm", getAvatarTone(review.userName))}>
                                                    {getInitials(review.userName)}
                                                </AvatarFallback>
                                            </Avatar>
                                            <div>
                                                <h3 className="leading-tight font-semibold">{review.userName}</h3>
                                                <p className="flex items-center gap-1 text-xs text-muted-foreground">
                                                    <Clock className="size-3" />
                                                    {new Date(review.createdAt).toLocaleDateString('en-US', {
                                                        year: 'numeric',
                                                        month: 'short',
                                                        day: 'numeric',
                                                        hour: '2-digit',
                                                        minute: '2-digit'
                                                    })}
                                                </p>
                                            </div>
                                        </div>

                                        {/* Action Buttons - Show for user's own reviews */}
                                        {isUserReview(review) && (
                                            <div className="flex gap-1">
                                                <Button variant="ghost" size="icon-sm" aria-label="Edit review" onClick={() => handleEditReview(review)}>
                                                    <Pencil />
                                                </Button>
                                                <Button
                                                    variant="ghost"
                                                    size="icon-sm"
                                                    aria-label="Delete review"
                                                    className="text-destructive hover:text-destructive"
                                                    onClick={() => handleDeleteReview(review.id)}
                                                >
                                                    <Trash2 />
                                                </Button>
                                            </div>
                                        )}
                                    </div>

                                    {/* Review Content */}
                                    {editingReview && editingReview.id === review.id ? (
                                        <div className="space-y-3">
                                            <Textarea
                                                rows={4}
                                                value={editContent}
                                                onChange={(e) => setEditContent(e.target.value)}
                                                placeholder="Update your review..."
                                            />
                                            <div className="flex gap-2">
                                                <Button variant="success" onClick={handleUpdateReview}>
                                                    <Check />Save Changes
                                                </Button>
                                                <Button variant="outline" onClick={cancelEdit}>
                                                    Cancel
                                                </Button>
                                            </div>
                                        </div>
                                    ) : (
                                        <p className="leading-relaxed whitespace-pre-line text-foreground/90">{review.content}</p>
                                    )}
                                </div>
                            ))
                        )}
                    </div>

                    {/* Add Review Section */}
                    {user && !isAdding && !editingReview && (
                        <div className="mt-6 flex justify-center border-t pt-6">
                            <Button size="lg" onClick={() => setIsAdding(true)}>
                                <PenLine />
                                Write a Review
                            </Button>
                        </div>
                    )}

                    {/* Add Review Form */}
                    {isAdding && (
                        <div className="mt-6 space-y-3 rounded-xl border bg-card p-4 sm:p-5">
                            <div className="flex items-center gap-3">
                                <Avatar className="size-10">
                                    <AvatarFallback className={cn("text-sm", getAvatarTone(currentUserName))}>
                                        {getInitials(currentUserName)}
                                    </AvatarFallback>
                                </Avatar>
                                <div>
                                    <h3 className="leading-tight font-semibold">{currentUserName}</h3>
                                    <p className="text-xs text-muted-foreground">Writing a review...</p>
                                </div>
                            </div>

                            <Textarea
                                rows={4}
                                value={newReview}
                                onChange={(e) => setNewReview(e.target.value)}
                                placeholder="Share your thoughts about this movie..."
                            />

                            <div className="flex gap-2">
                                <Button onClick={handleAddReview} disabled={!newReview.trim()}>
                                    <Send />Submit Review
                                </Button>
                                <Button
                                    variant="outline"
                                    onClick={() => {
                                        setIsAdding(false);
                                        setNewReview("");
                                    }}
                                >
                                    Cancel
                                </Button>
                            </div>
                        </div>
                    )}

                    {/* Login Prompt for Non-Users */}
                    {!user && (
                        <div className="mt-6 flex justify-center border-t pt-6">
                            <div className="inline-flex items-center gap-2 rounded-lg bg-primary/10 px-4 py-3 text-sm text-muted-foreground">
                                <Info className="size-4 text-primary" />
                                <span>
                                    <button
                                        type="button"
                                        className="cursor-pointer font-semibold text-primary hover:underline"
                                        onClick={() => navigate("/login")}
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
        </Card>
    );
};

export default ReviewList;
