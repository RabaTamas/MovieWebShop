const ProductSection = ({ movie, onAddToCart, isPurchased = false }) => (
    <>
        <section className="py-5 bg-gradient-to-br from-slate-50 to-slate-100">
            <div className="container px-4 px-lg-5 my-5">
                <div className="row gx-5 align-items-start">
                    {/* Movie Image - Now Smaller and Styled */}
                    <div className="col-md-4">
                        <div className="position-relative overflow-hidden rounded-3 shadow-lg hover-lift">
                            <img
                                className="img-fluid rounded-3 transition-transform"
                                src={movie.imageUrl}
                                alt={movie.title}
                                style={{
                                    maxHeight: '400px',
                                    width: '100%',
                                    objectFit: 'cover',
                                    filter: 'brightness(1.05) contrast(1.1)'
                                }}
                            />
                            <div className="position-absolute top-0 start-0 w-100 h-100 bg-gradient-to-t from-black/20 to-transparent opacity-0 hover:opacity-100 transition-opacity"></div>
                        </div>
                    </div>

                    {/* Movie Details */}
                    <div className="col-md-8">
                        <div className="ps-md-4">
                            {/* Title with Modern Typography */}
                            <h1 className="display-4 fw-bold text-dark mb-3" style={{
                                background: 'linear-gradient(135deg, #1e293b, #475569)',
                                WebkitBackgroundClip: 'text',
                                WebkitTextFillColor: 'transparent',
                                backgroundClip: 'text'
                            }}>
                                {movie.title}
                            </h1>

                            {/* TMDB Rating Section */}
                            {movie.tmdbInfo && (
                                <div className="mb-4">
                                    <div className="d-flex align-items-center gap-3 p-3 bg-white rounded-3 shadow-sm border">
                                        <div className="d-flex align-items-center">
                                            <div
                                                className="badge d-flex align-items-center justify-content-center me-3"
                                                style={{
                                                    width: '60px',
                                                    height: '60px',
                                                    borderRadius: '50%',
                                                    background: movie.tmdbInfo.voteAverage >= 7
                                                        ? 'linear-gradient(135deg, #10b981, #059669)'
                                                        : movie.tmdbInfo.voteAverage >= 5
                                                            ? 'linear-gradient(135deg, #f59e0b, #d97706)'
                                                            : 'linear-gradient(135deg, #ef4444, #dc2626)',
                                                    fontSize: '1.1rem',
                                                    fontWeight: 'bold',
                                                    color: 'white'
                                                }}
                                            >
                                                {movie.tmdbInfo.voteAverage.toFixed(1)}
                                            </div>
                                            <div>
                                                <div className="fw-semibold text-dark">TMDB Rating</div>
                                                <small className="text-muted">
                                                    {movie.tmdbInfo.voteCount.toLocaleString()} votes
                                                </small>
                                            </div>
                                        </div>
                                        {movie.tmdbInfo.releaseDate && (
                                            <div className="ms-auto text-end">
                                                <div className="fw-semibold text-dark">Release Date</div>
                                                <small className="text-muted">
                                                    {new Date(movie.tmdbInfo.releaseDate).toLocaleDateString('en-US', {
                                                        year: 'numeric',
                                                        month: 'long',
                                                        day: 'numeric'
                                                    })}
                                                </small>
                                            </div>
                                        )}
                                    </div>
                                </div>
                            )}

                            {/* Price Section with Modern Design */}
                            <div className="mb-4">
                                <div className="p-3 bg-white rounded-3 shadow-sm border">
                                    {movie.discountedPrice ? (
                                        <div className="d-flex align-items-center gap-3">
                                            <span
                                                className="fs-4 text-decoration-line-through text-muted"
                                                style={{ opacity: 0.6 }}
                                            >
                                                {movie.price.toLocaleString()} Ft
                                            </span>
                                            <span
                                                className="fs-2 fw-bold"
                                                style={{
                                                    background: 'linear-gradient(135deg, #dc2626, #ef4444)',
                                                    WebkitBackgroundClip: 'text',
                                                    WebkitTextFillColor: 'transparent',
                                                    backgroundClip: 'text'
                                                }}
                                            >
                                                {movie.discountedPrice.toLocaleString()} Ft
                                            </span>
                                            <span className="badge bg-danger ms-2 px-3 py-2">
                                                -{Math.round((1 - movie.discountedPrice / movie.price) * 100)}% OFF
                                            </span>
                                        </div>
                                    ) : (
                                        <span
                                            className="fs-2 fw-bold"
                                            style={{
                                                background: 'linear-gradient(135deg, #1e293b, #475569)',
                                                WebkitBackgroundClip: 'text',
                                                WebkitTextFillColor: 'transparent',
                                                backgroundClip: 'text'
                                            }}
                                        >
                                            {movie.price.toLocaleString()} Ft
                                        </span>
                                    )}
                                </div>
                            </div>

                            {/* Description */}
                            <div className="mb-4">
                                <p className="lead text-muted lh-lg" style={{ fontSize: '1.1rem' }}>
                                    {movie.description}
                                </p>
                            </div>

                            {/* Add to Cart Button */}
                            <div className="d-flex gap-3">
                                {isPurchased ? (
                                    <div className="alert alert-success d-flex align-items-center" role="alert">
                                        <i className="bi-check-circle-fill me-2"></i>
                                        You already own this movie! Watch it in My Movies.
                                    </div>
                                ) : (
                                    <button
                                        className="btn btn-lg px-4 py-3 fw-semibold position-relative overflow-hidden hover-lift-btn"
                                        onClick={onAddToCart}
                                        style={{
                                            background: 'linear-gradient(135deg, #3b82f6, #1d4ed8)',
                                            border: 'none',
                                            color: 'white',
                                            borderRadius: '12px',
                                            transition: 'all 0.3s ease',
                                            boxShadow: '0 4px 15px rgba(59, 130, 246, 0.3)'
                                        }}
                                    >
                                        <i className="bi-cart-plus me-2"></i>
                                        Add to Cart
                                    </button>
                                )}
                            </div>

                        </div>
                    </div>
                </div>
            </div>
        </section>

        <style>{`
            .hover-lift {
                transition: transform 0.3s ease, box-shadow 0.3s ease;
            }
            .hover-lift:hover {
                transform: translateY(-5px);
                box-shadow: 0 15px 35px rgba(0, 0, 0, 0.1) !important;
            }
            .transition-transform {
                transition: transform 0.3s ease;
            }
            .hover-lift:hover .transition-transform {
                transform: scale(1.02);
            }
            .hover-lift-btn:hover {
                transform: translateY(-2px);
                box-shadow: 0 8px 25px rgba(59, 130, 246, 0.4) !important;
            }
        `}</style>
    </>
);

export default ProductSection;