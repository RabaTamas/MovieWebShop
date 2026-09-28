import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { AlertCircle, ChevronRight, KeyRound, Mail, ShieldCheck, UserRound } from "lucide-react";
import { useAuth } from "../contexts/AuthContext";
import API_BASE_URL from "../config/api";
import { cn } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { LoadingState } from "@/components/ui/spinner";
import { PageContainer } from "@/components/ui/page";
import NotificationSettings from "@/components/pwa/NotificationSettings";

const FormMessage = ({ message, success }) =>
    message ? (
        <p className={cn("text-sm", success ? "text-success" : "text-destructive")}>{message}</p>
    ) : null;

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
                setEmailMessage("Email updated!");
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

    if (isLoading) return <LoadingState label="Loading profile..." />;
    if (error || !profile) {
        return (
            <PageContainer size="md">
                <Alert variant="destructive">
                    <AlertCircle />
                    <AlertDescription>{error || "Unsuccessful to load the profile"}</AlertDescription>
                </Alert>
            </PageContainer>
        );
    }

    const displayName = profile.name || profile.userName || user?.name;

    return (
        <PageContainer size="md" className="space-y-6">
            {/* Header */}
            <div className="flex items-center gap-4">
                <Avatar className="size-16 ring-2 ring-primary/40">
                    <AvatarFallback className="bg-primary/15 text-xl text-primary">
                        {(displayName || profile.email || "U").slice(0, 1).toUpperCase()}
                    </AvatarFallback>
                </Avatar>
                <div className="min-w-0">
                    <h1 className="text-3xl font-bold tracking-tight">Profile</h1>
                    <p className="flex items-center gap-1.5 truncate text-muted-foreground">
                        {displayName && <><UserRound className="size-4" />{displayName} · </>}
                        <Mail className="size-4" /> {profile.email}
                    </p>
                </div>
            </div>

            <div className="grid gap-6 md:grid-cols-2">
                <Card>
                    <CardHeader>
                        <CardTitle className="flex items-center gap-2"><Mail className="size-4 text-primary" /> Change email</CardTitle>
                        <CardDescription>Current: {profile.email}</CardDescription>
                    </CardHeader>
                    <CardContent>
                        <form onSubmit={handleEmailUpdate} className="space-y-3">
                            <div className="space-y-2">
                                <Label htmlFor="new-email">New email</Label>
                                <Input
                                    id="new-email"
                                    type="email"
                                    placeholder="New email"
                                    value={newEmail}
                                    onChange={(e) => setNewEmail(e.target.value)}
                                    required
                                />
                            </div>
                            <Button type="submit">Update</Button>
                            <FormMessage message={emailMessage} success={emailMessage.includes("updated")} />
                        </form>
                    </CardContent>
                </Card>

                <Card>
                    <CardHeader>
                        <CardTitle className="flex items-center gap-2"><KeyRound className="size-4 text-primary" /> Change password</CardTitle>
                        <CardDescription>Use at least 8 characters.</CardDescription>
                    </CardHeader>
                    <CardContent>
                        <form onSubmit={handlePasswordChange} className="space-y-3">
                            <div className="space-y-2">
                                <Label htmlFor="current-password">Current password</Label>
                                <Input
                                    id="current-password"
                                    type="password"
                                    autoComplete="current-password"
                                    placeholder="Current password"
                                    value={currentPassword}
                                    onChange={(e) => setCurrentPassword(e.target.value)}
                                    required
                                />
                            </div>
                            <div className="space-y-2">
                                <Label htmlFor="new-password">New password</Label>
                                <Input
                                    id="new-password"
                                    type="password"
                                    autoComplete="new-password"
                                    placeholder="New password"
                                    value={newPassword}
                                    onChange={(e) => setNewPassword(e.target.value)}
                                    required
                                    minLength={8}
                                />
                            </div>
                            <Button type="submit">Change</Button>
                            <FormMessage message={passwordMessage} success={passwordMessage.includes("successful")} />
                        </form>
                    </CardContent>
                </Card>
            </div>

            <Card>
                <CardContent className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                    <div className="flex items-center gap-4">
                        <div className="flex size-11 shrink-0 items-center justify-center rounded-full bg-primary/15 text-primary">
                            <ShieldCheck className="size-5" />
                        </div>
                        <div>
                            <h2 className="font-semibold">Security</h2>
                            <p className="text-sm text-muted-foreground">Protect your account with two-factor authentication.</p>
                        </div>
                    </div>
                    <Button variant="outline" asChild>
                        <Link to="/profile/2fa">
                            Manage Two-Factor Authentication
                            <ChevronRight />
                        </Link>
                    </Button>
                </CardContent>
            </Card>

            <NotificationSettings />
        </PageContainer>
    );
};

export default Profile;
