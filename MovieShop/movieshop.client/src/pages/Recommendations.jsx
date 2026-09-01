import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import MovieCard from '../components/MovieCard';
import API_BASE_URL from '../config/api';
import { useAuth } from '../contexts/AuthContext';

function HorizontalRow({ movies, badge, badgeClass }) {
    return (
        <div className="scroll-row">
            {movies.map(movie => (
                <div key={movie.id} className="scroll-row-item">
                    <span
                        className={`badge ${badgeClass} position-absolute top-0 start-0 m-2`}
                        style={{ zIndex: 1, fontSize: '0.65rem' }}
                    >
                        {badge}
                    </span>
                    <MovieCard movie={movie} />
                    {movie.reason && (
                        <div className="small text-muted text-truncate mt-1 px-1" title={movie.reason}>
                            <i className="bi bi-info-circle me-1" />{movie.reason}
                        </div>
                    )}
                </div>
            ))}
        </div>
    );
}

HorizontalRow.propTypes = {
    movies: PropTypes.array.isRequired,
    badge: PropTypes.node.isRequired,
    badgeClass: PropTypes.string.isRequired,
};

export default function Recommendations() {
    const { token } = useAuth();
    const [data, setData] = useState(null);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        if (!token) { setLoading(false); return; }
        fetch(`${API_BASE_URL}/api/Recommendation`, {
            headers: { Authorization: `Bearer ${token}` }
        })
            .then(r => r.ok ? r.json() : null)
            .then(d => { setData(d); setLoading(false); })
            .catch(() => setLoading(false));
    }, [token]);

    if (!token) return (
        <div className="container mt-5 text-center">
            <div style={{ fontSize: '3rem' }}>🎬</div>
            <h4 className="mt-3">Log in to see your recommendations</h4>
            <p className="text-muted">We'll suggest movies based on your purchase history.</p>
            <Link to="/login" className="btn btn-primary mt-2">Log in</Link>
        </div>
    );

    if (loading) return (
        <div className="container mt-5 d-flex justify-content-center">
            <div className="text-center">
                <div className="spinner-border text-primary mb-3" role="status" />
                <div>Loading recommendations…</div>
            </div>
        </div>
    );

    const hasCategory      = data?.categoryBased?.length > 0;
    const hasCollaborative = data?.collaborativeBased?.length > 0;

    if (!hasCategory && !hasCollaborative) return (
        <div className="container mt-5 text-center">
            <div style={{ fontSize: '3rem' }}>🛒</div>
            <h4 className="mt-3">No recommendations yet</h4>
            <p className="text-muted">Purchase some movies and we'll start suggesting titles you'll love.</p>
            <Link to="/" className="btn btn-outline-primary mt-2">Browse movies</Link>
        </div>
    );

    const openAiChat = () => {
        const shown = [
            ...(data?.categoryBased || []),
            ...(data?.collaborativeBased || [])
        ].map(m => m.title);

        const message = shown.length > 0
            ? `I already see these movies recommended on my recommendations page: ${shown.join(', ')}. What other movies would you suggest for me that are different from these?`
            : 'What movies would you recommend for me based on my purchase history?';

        globalThis.dispatchEvent(new CustomEvent('chatbot:open', { detail: { message } }));
    };

    return (
        <div className="container mt-4">
            <div className="mb-4 d-flex justify-content-between align-items-start flex-wrap gap-3">
                <div>
                    <h3>
                        <i className="bi bi-stars me-2 text-warning"></i>
                        {" "}Your Recommendations
                    </h3>
                    <p className="text-muted mb-0">
                        Personalised picks based on your purchase history and customers with similar taste.
                    </p>
                </div>
                <button
                    className="btn btn-outline-primary"
                    onClick={openAiChat}
                    title="Ask the AI chatbot for more personalised suggestions"
                >
                    🤖 Ask AI for more suggestions
                </button>
            </div>

            {hasCategory && (
                <section className="mb-5">
                    <h5 className="mb-3">
                        <i className="bi bi-stars me-2 text-warning"></i>
                        {" "}Recommended for You
                        <span className="badge bg-warning text-dark ms-2" style={{ fontSize: '0.7rem' }}>
                            Category match
                        </span>
                    </h5>
                    <HorizontalRow
                        movies={data.categoryBased}
                        badge={<><i className="bi bi-stars me-1" />Recommended</>}
                        badgeClass="bg-warning text-dark"
                    />
                </section>
            )}

            {hasCollaborative && (
                <section className="mb-5">
                    <h5 className="mb-3">
                        <i className="bi bi-people me-2 text-info"></i>
                        {" "}Customers Also Bought
                        <span className="badge bg-info text-dark ms-2" style={{ fontSize: '0.7rem' }}>
                            Similar taste
                        </span>
                    </h5>
                    <HorizontalRow
                        movies={data.collaborativeBased}
                        badge={<><i className="bi bi-people me-1" />Popular pick</>}
                        badgeClass="bg-info text-dark"
                    />
                </section>
            )}
        </div>
    );
}
