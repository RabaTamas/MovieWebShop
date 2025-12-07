import { useEffect, useState } from "react";
import { useAuth } from "../contexts/AuthContext";
import API_BASE_URL from "../config/api";

const Profile = () => {
    const { user, token, logout } = useAuth();
    const [profile, setProfile] = useState(null);
    const [newEmail, setNewEmail] = useState("");
    const [emailMessage, setEmailMessage] = useState("");
    const [currentPassword, setCurrentPassword] = useState("");
    const [newPassword, setNewPassword] = useState("");
    const [passwordMessage, setPasswordMessage] = useState("");
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState("");

    useEffect(() => {
        if (!token) {
            setError("No authentication token found");
            setIsLoading(false);
            return;
        }

        const fetchProfile = async () => {
            try {
                const res = await fetch(`${API_BASE_URL}/api/user/profile`, {
                    headers: {
                        Authorization: `Bearer ${token}`
                    }
                });

                if (!res.ok) {
                    if (res.status === 401) {
                        setError("Authentication failed. Please log in again.");
                        logout();
                        return;
                    }
                    throw new Error(`Profile fetch failed: ${res.status}`);
                }

                const data = await res.json();
                setProfile(data);
                setIsLoading(false);
            } catch (err) {
                console.error("Profile fetch error:", err);
                setError("Failed to get the profile");
                setIsLoading(false);
            }
        };

        fetchProfile();
    }, [token, logout]);

    const handleEmailUpdate = async (e) => {
        e.preventDefault();
        setEmailMessage("");

        try {
            const res = await fetch(`${API_BASE_URL}/api/user/email`, {
                method: "PUT",
                headers: {
                    "Content-Type": "application/json",
                    Authorization: `Bearer ${token}`
                },
                body: JSON.stringify({ newEmail })
            });

            if (res.ok) {
                setEmailMessage("Email updtated!");
                setProfile(prev => ({ ...prev, email: newEmail }));
                setNewEmail("");
            } else {
                const errorData = await res.text();
                setEmailMessage(errorData || "Error happened");
            }
        } catch (err) {
            console.error("Email update error:", err);
            setEmailMessage("Unsuccessful connection to the server.");
        }
    };

    const handlePasswordChange = async (e) => {
        e.preventDefault();
        setPasswordMessage("");

        try {
            const res = await fetch(`${API_BASE_URL}/api/user/password`, {
                method: "PUT",
                headers: {
                    "Content-Type": "application/json",
                    Authorization: `Bearer ${token}`
                },
                body: JSON.stringify({
                    currentPassword,
                    newPassword
                })
            });

            if (res.ok) {
                setPasswordMessage("Password successfully changed");
                setCurrentPassword("");
                setNewPassword("");
            } else {
                const errorData = await res.text();
                setPasswordMessage(errorData || "Error happened");
            }
        } catch (err) {
            console.error("Password change error:", err);
            setPasswordMessage("Unsuccessful connection to the server.");
        }
    };

    if (isLoading) return <p>Bet�lt�s...</p>;
    if (error) return <p className="error-message">{error}</p>;
    if (!profile) return <p>Unsuccessful to load the profile</p>;

    return (
        <div style={{ maxWidth: "500px", margin: "2rem auto", padding: "1rem", border: "1px solid #ccc", borderRadius: "8px" }}>
            <h2>Profile</h2>
            <p><strong>Email:</strong> {profile.email}</p>

            <hr />

            <h3>Change email</h3>
            <form onSubmit={handleEmailUpdate}>
                <div className="form-group">
                    <input
                        type="email"
                        placeholder="New email"
                        value={newEmail}
                        onChange={(e) => setNewEmail(e.target.value)}
                        required
                        className="form-control"
                    />
                    <button type="submit" className="btn btn-primary">Update</button>
                </div>
                {emailMessage && <p className={emailMessage.includes("updtated") ? "success-message" : "error-message"}>{emailMessage}</p>}
            </form>

            <hr />

            <h3>Change password</h3>
            <form onSubmit={handlePasswordChange}>
                <div className="form-group">
                    <input
                        type="password"
                        placeholder="Current password"
                        value={currentPassword}
                        onChange={(e) => setCurrentPassword(e.target.value)}
                        required
                        className="form-control"
                    />
                </div>
                <div className="form-group">
                    <input
                        type="password"
                        placeholder="New password"
                        value={newPassword}
                        onChange={(e) => setNewPassword(e.target.value)}
                        required
                        minLength={8}
                        className="form-control"
                    />
                </div>
                <button type="submit" className="btn btn-primary">Change</button>
                {passwordMessage && <p className={passwordMessage.includes("successful") ? "success-message" : "error-message"}>{passwordMessage}</p>}
            </form>
        </div>
    );
};

export default Profile;