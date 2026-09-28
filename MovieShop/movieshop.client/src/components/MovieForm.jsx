import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { AlertCircle, ArrowLeft, Film, ImageOff, Save } from "lucide-react";
import { useAuth } from "../contexts/AuthContext"

import API_BASE_URL from "../config/api";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Label } from "@/components/ui/label";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { LoadingState, Spinner } from "@/components/ui/spinner";
import { PageContainer, PageHeader } from "@/components/ui/page";

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

    const [, setAllCategories] = useState([]);
    const [loading, setLoading] = useState(isEditMode);
    const [error, setError] = useState(null);
    const [submitting, setSubmitting] = useState(false);

    // Fetch categories when component mounts
    useEffect(() => {
        const fetchCategories = async () => {
            try {
                const response = await fetch(`${API_BASE_URL}/api/category`, {
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
                isEditMode ? `${API_BASE_URL}/api/movie/${id}` : `${API_BASE_URL}/api/movie`,
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
                } catch {
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
        return <LoadingState label="Loading movie data..." />;
    }

    return (
        <PageContainer size="lg" className="max-w-5xl">
            <PageHeader
                title={isEditMode ? 'Edit Movie' : 'Add New Movie'}
                icon={Film}
                description={isEditMode ? formData.title : 'Categories and video can be added after saving.'}
            >
                <Button variant="outline" onClick={() => navigate('/admin/movies')}>
                    <ArrowLeft /> Back to Movies
                </Button>
            </PageHeader>

            {error && (
                <Alert variant="destructive" className="mb-6">
                    <AlertCircle />
                    <AlertDescription>{error}</AlertDescription>
                </Alert>
            )}

            <form onSubmit={handleSubmit}>
                <div className="grid items-start gap-6 lg:grid-cols-[1fr_260px]">
                    <Card>
                        <CardContent className="space-y-5">
                            <div className="space-y-2">
                                <Label htmlFor="title">Title *</Label>
                                <Input
                                    type="text"
                                    id="title"
                                    name="title"
                                    value={formData.title}
                                    onChange={handleInputChange}
                                    required
                                />
                            </div>

                            <div className="space-y-2">
                                <Label htmlFor="description">Description *</Label>
                                <Textarea
                                    id="description"
                                    name="description"
                                    rows={5}
                                    value={formData.description}
                                    onChange={handleInputChange}
                                    required
                                />
                            </div>

                            <div className="grid gap-5 sm:grid-cols-2">
                                <div className="space-y-2">
                                    <Label htmlFor="price">Price (Ft) *</Label>
                                    <Input
                                        type="number"
                                        id="price"
                                        name="price"
                                        value={formData.price}
                                        onChange={handleInputChange}
                                        min="0"
                                        required
                                    />
                                </div>

                                <div className="space-y-2">
                                    <Label htmlFor="discountedPrice">Discounted Price (optional)</Label>
                                    <Input
                                        type="number"
                                        id="discountedPrice"
                                        name="discountedPrice"
                                        value={formData.discountedPrice || ""}
                                        onChange={handleInputChange}
                                        min="0"
                                    />
                                </div>
                            </div>

                            <div className="space-y-2">
                                <Label htmlFor="imageUrl">Image URL *</Label>
                                <Input
                                    type="url"
                                    id="imageUrl"
                                    name="imageUrl"
                                    value={formData.imageUrl}
                                    onChange={handleInputChange}
                                    required
                                />
                            </div>
                        </CardContent>
                    </Card>

                    {/* Image Preview */}
                    <div className="space-y-2">
                        <p className="text-sm font-medium">Image Preview</p>
                        <div className="aspect-[2/3] overflow-hidden rounded-xl border bg-muted">
                            {formData.imageUrl ? (
                                <img src={formData.imageUrl} alt="Movie preview" className="size-full object-cover" />
                            ) : (
                                <div className="flex size-full flex-col items-center justify-center gap-2 text-muted-foreground">
                                    <ImageOff className="size-8" />
                                    <span className="text-xs">No image URL</span>
                                </div>
                            )}
                        </div>
                    </div>
                </div>

                <div className="mt-6 flex justify-between gap-3">
                    <Button type="button" variant="outline" onClick={() => navigate('/admin/movies')}>
                        Cancel
                    </Button>
                    <Button type="submit" disabled={submitting}>
                        {submitting ? <><Spinner />Saving...</> : <><Save />Save Movie</>}
                    </Button>
                </div>
            </form>
        </PageContainer>
    );
};

export default MovieForm;
