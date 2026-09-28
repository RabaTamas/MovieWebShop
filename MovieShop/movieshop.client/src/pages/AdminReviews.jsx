import { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import { AlertCircle, MessageSquareText, Trash2 } from "lucide-react";
import { useAuth } from "../contexts/AuthContext";

import API_BASE_URL from "../config/api";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { LoadingState } from "@/components/ui/spinner";
import { PageContainer, PageHeader } from "@/components/ui/page";

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
        return <LoadingState label="Loading reviews..." />;
    }

    if (error) {
        return (
            <PageContainer size="md">
                <Alert variant="destructive">
                    <AlertCircle />
                    <AlertDescription>Error: {error}</AlertDescription>
                </Alert>
            </PageContainer>
        );
    }

    return (
        <PageContainer size="xl">
            <PageHeader
                title="Manage Reviews"
                icon={MessageSquareText}
                description="As an admin, you can only delete reviews. Users can create and edit their own reviews."
            />

            <Card className="py-0">
                <Table>
                    <TableHeader>
                        <TableRow className="hover:bg-transparent">
                            <TableHead className="w-14">ID</TableHead>
                            <TableHead>Movie</TableHead>
                            <TableHead>User</TableHead>
                            <TableHead>Content</TableHead>
                            <TableHead>Date</TableHead>
                            <TableHead className="text-right">Actions</TableHead>
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {reviews.length === 0 ? (
                            <TableRow>
                                <TableCell colSpan={6} className="py-10 text-center text-muted-foreground">No reviews found</TableCell>
                            </TableRow>
                        ) : (
                            reviews.map(review => (
                                <TableRow key={review.id}>
                                    <TableCell className="text-muted-foreground">{review.id}</TableCell>
                                    <TableCell className="font-medium">
                                        {review.movie ? (
                                            <Link to={`/movies/${review.movie.id}`} className="hover:text-primary hover:underline">
                                                {review.movie.title}
                                            </Link>
                                        ) : (
                                            <span className="text-muted-foreground">Movie not found</span>
                                        )}
                                    </TableCell>
                                    <TableCell>{review.user ? review.user.name : 'Unknown User'}</TableCell>
                                    <TableCell className="max-w-md min-w-60 whitespace-normal text-muted-foreground">
                                        {review.content.length > 100
                                            ? `${review.content.substring(0, 100)}...`
                                            : review.content
                                        }
                                    </TableCell>
                                    <TableCell className="whitespace-nowrap text-muted-foreground">{new Date(review.createdAt).toLocaleDateString()}</TableCell>
                                    <TableCell className="text-right">
                                        <Button
                                            variant="ghost"
                                            size="sm"
                                            className="text-destructive hover:text-destructive"
                                            onClick={() => handleDeleteReview(review.id)}
                                            title="Delete review"
                                        >
                                            <Trash2 /> Delete
                                        </Button>
                                    </TableCell>
                                </TableRow>
                            ))
                        )}
                    </TableBody>
                </Table>
            </Card>
        </PageContainer>
    );
};

export default AdminReviews;
