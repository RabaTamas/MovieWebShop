import { Link } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";

const Unauthorized = () => {
    const { user } = useAuth();

    return (
        <div className="container mt-5">
            <div className="row justify-content-center">
                <div className="col-md-6 text-center">
                    <div className="card">
                        <div className="card-body">
                            <h1 className="display-1 text-danger">403</h1>
                            <h2 className="mb-4">Access Denied</h2>
                            <p className="mb-4">
                                {user
                                    ? "You don't have permission to access this page. Admin privileges required."
                                    : "You need to be logged in as an administrator to access this page."
                                }
                            </p>
                            <div className="d-flex gap-2 justify-content-center">
                                <Link to="/" className="btn btn-primary">
                                    Go Home
                                </Link>
                                {!user && (
                                    <Link to="/login" className="btn btn-outline-primary">
                                        Login
                                    </Link>
                                )}
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default Unauthorized;