import { Navigate } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";
import { UserRoles } from "../constants/UserRoles";

const AdminRoute = ({ children }) => {
    const { user, loading } = useAuth();

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

    // If not logged in, redirect to login
    if (!user) {
        return <Navigate to="/login" replace />;
    }

    // If logged in but not admin, redirect to unauthorized page
    if (user.role !== UserRoles.Admin) {
        return <Navigate to="/unauthorized" replace />;
    }

    // If admin, render the protected component
    return children;
};

export default AdminRoute;