import { Navigate, useLocation } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";

const PrivateRoute = ({ children }) => {
    const { user, loading } = useAuth();
    const location = useLocation();

    // Show loading while checking authentication
    if (loading) {
        return (
            <div className="d-flex justify-content-center align-items-center" style={{ height: '50vh' }}>
                <div className="spinner-border text-primary" role="status">
                    <span className="visually-hidden">Loading...</span>
                </div>
            </div>
        );
    }

    // If not logged in, redirect to login with current location as state
    // This allows redirecting back after successful login
    if (!user) {
        return <Navigate to="/login" state={{ from: location }} replace />;
    }

    // If authenticated, render the protected component
    return children;
};

export default PrivateRoute;