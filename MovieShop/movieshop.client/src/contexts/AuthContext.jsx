import { createContext, useState, useEffect, useContext } from "react";

const AuthContext = createContext();

export const AuthProvider = ({ children }) => {
    const [user, setUser] = useState(null);
    const [token, setToken] = useState(null);
    const [loading, setLoading] = useState(true);

    // Load user from token on initial mount
    useEffect(() => {
        const loadUserFromToken = () => {
            setLoading(true);
            const storedToken = localStorage.getItem("jwt");
            if (storedToken) {
                try {
                    const userData = JSON.parse(atob(storedToken.split('.')[1]));
                    // Check if token is expired
                    const expirationTime = userData.exp * 1000; // Convert to milliseconds
                    if (Date.now() >= expirationTime) {
                        // Token is expired
                        console.log("Token expired, logging out");
                        localStorage.removeItem("jwt");
                        setUser(null);
                        setToken(null);
                    } else {
                        // Valid token
                        setUser({
                            id: parseInt(userData.nameid || userData.sub || userData.id),
                            name: userData.name,
                            email: userData.email,
                            role: userData.role || userData["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"]
                        });
                        setToken(storedToken);
                    }
                } catch (e) {
                    console.error("Invalid token", e);
                    localStorage.removeItem("jwt");
                }
            }
            setLoading(false);
        };
        loadUserFromToken();
    }, []);

    // Login function to set user and token
    const login = (authResult) => {
        localStorage.setItem("jwt", authResult.token);
        setUser(authResult.user);
        setToken(authResult.token);
    };

    // Logout function
    const logout = () => {
        localStorage.removeItem("jwt");
        setUser(null);
        setToken(null);
    };

    // The value object that will be provided to consumers of this context
    const value = {
        user,
        token,
        loading,
        login,
        logout,
        setUser 
    };

    return (
        <AuthContext.Provider value={value}>
            {children}
        </AuthContext.Provider>
    );
};

export const useAuth = () => useContext(AuthContext);