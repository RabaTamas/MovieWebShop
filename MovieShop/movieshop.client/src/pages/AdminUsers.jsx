import { useState, useEffect } from "react";
import { AlertCircle, ShieldCheck, ShieldOff, Trash2, Users } from "lucide-react";
import { useAuth } from "../contexts/AuthContext";
import { UserRoles } from "../constants/UserRoles";
import API_BASE_URL from "../config/api";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card } from "@/components/ui/card";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { LoadingState } from "@/components/ui/spinner";
import { PageContainer, PageHeader } from "@/components/ui/page";

const AdminUsers = () => {
    const { token } = useAuth();
    const [users, setUsers] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);

    // Fetch all users when component mounts
    useEffect(() => {
        const fetchUsers = async () => {
            try {
                const response = await fetch(`${API_BASE_URL}/api/user/all`, {
                    headers: {
                        'Authorization': `Bearer ${token}`
                    }
                });

                if (!response.ok) {
                    throw new Error(`Error ${response.status}: ${response.statusText}`);
                }

                const data = await response.json();
                setUsers(data);
                setLoading(false);
            } catch (err) {
                console.error("Failed to fetch users:", err);
                setError(err.message);
                setLoading(false);
            }
        };

        if (token) {
            fetchUsers();
        } else {
            setError("Authentication token is missing");
            setLoading(false);
        }
    }, [token]);

    const handleRoleChange = async (userId, makeAdmin) => {
        const confirmMessage = makeAdmin
            ? "Are you sure you want to give admin rights to this user?"
            : "Are you sure you want to remove admin rights from this user?";

        if (!window.confirm(confirmMessage)) {
            return;
        }

        try {
            const endpoint = makeAdmin
                ? `${API_BASE_URL}/api/auth/make-admin/${userId}`
                : `${API_BASE_URL}/api/auth/remove-admin/${userId}`;

            const response = await fetch(endpoint, {
                method: 'POST',
                headers: {
                    'Authorization': `Bearer ${token}`,
                    'Content-Type': 'application/json'
                }
            });

            if (!response.ok) {
                throw new Error(`Error ${response.status}: ${response.statusText}`);
            }

            // Update the user's role in the state
            setUsers(users.map(user =>
                user.id === userId
                    ? { ...user, role: makeAdmin ? UserRoles.Admin : UserRoles.User }
                    : user
            ));
        } catch (err) {
            console.error("Failed to update user role:", err);
            setError(err.message);
        }
    };

    const handleDeleteUser = async (userId) => {
        if (!window.confirm("Are you sure you want to delete this user? This action cannot be undone.")) {
            return;
        }

        try {
            const response = await fetch(`${API_BASE_URL}/api/user/${userId}`, {
                method: 'DELETE',
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });

            if (!response.ok) {
                throw new Error(`Error ${response.status}: ${response.statusText}`);
            }

            // Remove the deleted user from the state
            setUsers(users.filter(user => user.id !== userId));
        } catch (err) {
            console.error("Failed to delete user:", err);
            setError(err.message);
        }
    };

    if (loading) {
        return <LoadingState label="Loading users..." />;
    }

    if (error) {
        return (
            <PageContainer size="md">
                <Alert variant="destructive">
                    <AlertCircle />
                    <AlertDescription>Error: {error}</AlertDescription>
                </Alert>
            </PageContainer>
        );
    }

    return (
        <PageContainer size="xl">
            <PageHeader title="Manage Users" icon={Users} description={`${users.length} registered users`} />

            <Card className="py-0">
                <Table>
                    <TableHeader>
                        <TableRow className="hover:bg-transparent">
                            <TableHead className="w-16">ID</TableHead>
                            <TableHead>Name</TableHead>
                            <TableHead>Email</TableHead>
                            <TableHead>Role</TableHead>
                            <TableHead className="text-right">Actions</TableHead>
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {users.map(user => (
                            <TableRow key={user.id}>
                                <TableCell className="text-muted-foreground">{user.id}</TableCell>
                                <TableCell className="font-medium">{user.name}</TableCell>
                                <TableCell className="text-muted-foreground">{user.email}</TableCell>
                                <TableCell>
                                    <Badge variant={user.role === UserRoles.Admin ? 'warning' : 'secondary'}>
                                        {user.role === UserRoles.Admin && <ShieldCheck />}
                                        {user.role}
                                    </Badge>
                                </TableCell>
                                <TableCell>
                                    <div className="flex justify-end gap-1">
                                        {user.role === UserRoles.Admin ? (
                                            <Button variant="ghost" size="sm" onClick={() => handleRoleChange(user.id, false)}>
                                                <ShieldOff /> Remove Admin Rights
                                            </Button>
                                        ) : (
                                            <Button variant="ghost" size="sm" onClick={() => handleRoleChange(user.id, true)}>
                                                <ShieldCheck /> Make Admin
                                            </Button>
                                        )}
                                        <Button
                                            variant="ghost"
                                            size="sm"
                                            className="text-destructive hover:text-destructive"
                                            onClick={() => handleDeleteUser(user.id)}
                                        >
                                            <Trash2 /> Delete
                                        </Button>
                                    </div>
                                </TableCell>
                            </TableRow>
                        ))}
                    </TableBody>
                </Table>
            </Card>
        </PageContainer>
    );
};

export default AdminUsers;
