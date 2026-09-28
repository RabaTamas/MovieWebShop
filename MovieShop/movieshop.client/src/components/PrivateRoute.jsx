import { Navigate, useLocation } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";
import { LoadingState } from "@/components/ui/spinner";

const PrivateRoute = ({ children }) => {
    const { user, loading } = useAuth();
    const location = useLocation();

    // Show loading while checking authentication
    if (loading) {
        return <LoadingState />;
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