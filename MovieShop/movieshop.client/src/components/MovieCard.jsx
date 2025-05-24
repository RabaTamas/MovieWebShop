import { Link } from "react-router-dom";

const MovieCard = ({ movie }) => {
    return (
        <div className="card h-100">
            <img src={movie.imageUrl} className="card-img-top" alt={movie.title} style={{ height: "300px", objectFit: "cover" }} />
            <div className="card-body d-flex flex-column">
                <h5 className="card-title">{movie.title}</h5>
                <p className="card-text mb-2">
                    {movie.discountedPrice != null ? (
                        <>
                            <span className="text-muted text-decoration-line-through">{movie.price} Ft</span>
                            <span className="ms-2 fw-bold text-danger">{movie.discountedPrice} Ft</span>
                        </>
                    ) : (
                        <span>{movie.price} Ft</span>
                    )}
                </p>
                <Link to={`/movies/${movie.id}`} className="btn btn-outline-dark mt-auto">
                    Details
                </Link>
            </div>
        </div>
    );
};

export default MovieCard;
