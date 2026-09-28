import { Navigate } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";
import { UserRoles } from "../constants/UserRoles";
import { LoadingState } from "@/components/ui/spinner";

const AdminRoute = ({ children }) => {
    const { user, loading } = useAuth();

    // Show loading while checking authentication
    if (loading) {
        return <LoadingState />;
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