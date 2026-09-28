import { Link } from "react-router-dom";
import { Home, LogIn, ShieldAlert } from "lucide-react";
import { useAuth } from "../contexts/AuthContext";
import { Button } from "@/components/ui/button";

const Unauthorized = () => {
    const { user } = useAuth();

    return (
        <div className="flex flex-1 items-center justify-center px-4 py-20">
            <div className="max-w-md text-center">
                <div className="mx-auto mb-4 flex size-16 items-center justify-center rounded-full bg-destructive/15 text-destructive">
                    <ShieldAlert className="size-8" />
                </div>
                <h1 className="font-display text-[9rem] leading-none tracking-wider text-destructive">403</h1>
                <h2 className="mb-3 text-2xl font-bold">Access Denied</h2>
                <p className="mb-8 text-muted-foreground">
                    {user
                        ? "You don't have permission to access this page. Admin privileges required."
                        : "You need to be logged in as an administrator to access this page."
                    }
                </p>
                <div className="flex justify-center gap-3">
                    <Button asChild>
                        <Link to="/"><Home />Go Home</Link>
                    </Button>
                    {!user && (
                        <Button variant="outline" asChild>
                            <Link to="/login"><LogIn />Login</Link>
                        </Button>
                    )}
                </div>
            </div>
        </div>
    );
};

export default Unauthorized;
