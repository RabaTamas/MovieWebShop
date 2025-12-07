import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import API_BASE_URL from '../config/api';

const MyMovies = () => {
    const { token } = useAuth();
    const [movies, setMovies] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);

    useEffect(() => {
        const fetchPurchasedMovies = async () => {
            try {
                const response = await fetch(`${API_BASE_URL}/api/Movie/purchased`, {
                    headers: {
                        'Authorization': `Bearer ${token}`
                    }
                });

                if (!response.ok) {
                    throw new Error('Failed to fetch purchased movies');
                }

                const data = await response.json();
                setMovies(data);
            } catch (err) {
                setError(err.message);
            } finally {
                setLoading(false);
            }
        };

        if (token) {
            fetchPurchasedMovies();
        }
    }, [token]);

    if (loading) {
        return (
            <div className="container mt-5">
                <div className="text-center">
                    <div className="spinner-border text-primary" role="status">
                        <span className="visually-hidden">Loading...</span>
                    </div>
                </div>
            </div>
        );
    }

    if (error) {
        return (
            <div className="container mt-5">
                <div className="alert alert-danger" role="alert">
                    Error: {error}
                </div>
            </div>
        );
    }

    return (
        <div className="container mt-5">
            <h1 className="mb-4">My Movies</h1>
            
            {movies.length === 0 ? (
                <div className="alert alert-info">
                    <h4>No movies purchased yet</h4>
                    <p>Browse our collection and purchase movies to watch them here.</p>
                    <Link to="/" className="btn btn-primary">Browse Movies</Link>
                </div>
            ) : (
                <div className="row">
                    {movies.map(movie => (
                        <div key={movie.id} className="col-md-4 col-lg-3 mb-4">
                            <div className="card h-100">
                                <img 
                                    src={movie.imageUrl || 'https://via.placeholder.com/300x450?text=No+Image'} 
                                    className="card-img-top" 
                                    alt={movie.title}
                                    style={{ height: '300px', objectFit: 'cover' }}
                                />
                                <div className="card-body d-flex flex-column">
                                    <h5 className="card-title">{movie.title}</h5>
                                    <p className="card-text text-muted small flex-grow-1">
                                        {movie.description?.substring(0, 100)}...
                                    </p>
                                    <div className="mt-auto">
                                        <Link 
                                            to={`/my-movies/${movie.id}/watch`} 
                                            className="btn btn-primary w-100"
                                        >
                                            <i className="bi bi-play-circle me-2"></i>
                                            Watch Now
                                        </Link>
                                    </div>
                                </div>
                            </div>
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
};

export default MyMovies;
