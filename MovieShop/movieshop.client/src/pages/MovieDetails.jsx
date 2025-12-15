import { useParams, useNavigate } from "react-router-dom";
import { useEffect, useState } from "react";
import { useAuth } from "../contexts/AuthContext";
import ProductSection from "../components/ProductSection";
import ReviewList from "../components/ReviewList";

import API_BASE_URL from "../config/api";

const MovieDetails = () => {
    const { id } = useParams();
    const navigate = useNavigate();
    const { user, token } = useAuth();
    const [movie, setMovie] = useState(null);
    const [loading, setLoading] = useState(true);
    const [isPurchased, setIsPurchased] = useState(false);

    useEffect(() => {
        setLoading(true);
        fetch(`${API_BASE_URL}/api/Movie/${id}`)
            .then((res) => {
                if (!res.ok) {
                    throw new Error("Movie not found");
                }
                return res.json();
            })
            .then((data) => {
                setMovie(data);
                setLoading(false);
            })
            .catch((error) => {
                console.error(error);
                setLoading(false);
                navigate("/");
            });
    }, [id, navigate]);

    // Check if user has already purchased this movie
    useEffect(() => {
        if (user && token) {
            fetch(`${API_BASE_URL}/api/Movie/purchased`, {
                headers: { Authorization: `Bearer ${token}` }
            })
                .then(res => res.json())
                .then(movies => {
                    const purchased = movies.some(m => m.id === parseInt(id));
                    setIsPurchased(purchased);
                })
                .catch(err => console.error("Error checking purchased movies:", err));
        }
    }, [user, token, id]);

    const handleAddToCart = async () => {
        if (!user) {
            navigate("/login");
            return;
        }

        if (isPurchased) {
            alert("You already own this movie! Watch it in My Movies.");
            navigate("/my-movies");
            return;
        }

        try {
            const response = await fetch(`${API_BASE_URL}/api/ShoppingCart/add`, {
                method: "POST",
                headers: {
                    "Content-Type": "application/json",
                    Authorization: `Bearer ${token}`
                },
                body: JSON.stringify({
                    movieId: movie.id,
                    quantity: 1,
                }),
            });

            if (!response.ok) {
                const errorData = await response.json();
                throw new Error(errorData.message || "Failed to add movie to cart");
            }

            navigate("/cart");
        } catch (error) {
            console.error("Error adding to cart:", error);
            alert(error.message);
        }
    };

    if (loading) {
        return <div className="container mt-4">Loading movie details...</div>;
    }

    if (!movie) {
        return <div className="container mt-4">Movie not found</div>;
    }

    return (
        <div className="container">
            <ProductSection movie={movie} onAddToCart={handleAddToCart} isPurchased={isPurchased} />
            <ReviewList movieId={movie.id} />
        </div>
    );
};

export default MovieDetails;