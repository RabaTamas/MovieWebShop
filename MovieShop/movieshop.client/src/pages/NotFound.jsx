import { Link } from "react-router-dom";

const NotFound = () => {
    return (
        <div className="container mt-5">
            <div className="row justify-content-center">
                <div className="col-md-6 text-center">
                    <div className="card">
                        <div className="card-body">
                            <h1 className="display-1 text-muted">404</h1>
                            <h2 className="mb-4">Page Not Found</h2>
                            <p className="mb-4">
                                The page you're looking for doesn't exist or has been moved.
                            </p>
                            <Link to="/" className="btn btn-primary">
                                Go Home
                            </Link>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default NotFound;