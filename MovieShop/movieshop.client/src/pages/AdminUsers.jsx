import { useState, useEffect } from "react";
import { useAuth } from "../contexts/AuthContext";
import { UserRoles } from "../constants/UserRoles";
import API_BASE_URL from '../config/api';

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
        return <div className="container mt-4"><div className="spinner-border" role="status"></div> Loading users...</div>;
    }

    if (error) {
        return <div className="container mt-4 alert alert-danger">Error: {error}</div>;
    }

    return (
        <div className="container mt-4">
            <h1>Manage Users</h1>

            <div className="table-responsive">
                <table className="table table-striped table-hover">
                    <thead>
                        <tr>
                            <th>ID</th>
                            <th>Name</th>
                            <th>Email</th>
                            <th>Role</th>
                            <th>Actions</th>
                        </tr>
                    </thead>
                    <tbody>
                        {users.map(user => (
                            <tr key={user.id}>
                                <td>{user.id}</td>
                                <td>{user.name}</td>
                                <td>{user.email}</td>
                                <td>
                                    <span className={`badge ${user.role === UserRoles.Admin ? 'bg-danger' : 'bg-primary'}`}>
                                        {user.role}
                                    </span>
                                </td>
                                <td>
                                    <div className="btn-group">
                                        {user.role === UserRoles.Admin ? (
                                            <button
                                                className="btn btn-sm btn-warning"
                                                onClick={() => handleRoleChange(user.id, false)}
                                            >
                                                <i className="bi bi-person"></i> Remove Admin Rights
                                            </button>
                                        ) : (
                                            <button
                                                className="btn btn-sm btn-info"
                                                onClick={() => handleRoleChange(user.id, true)}
                                            >
                                                <i className="bi bi-person-fill"></i> Make Admin
                                            </button>
                                        )}
                                        <button
                                            className="btn btn-sm btn-danger"
                                            onClick={() => handleDeleteUser(user.id)}
                                        >
                                            <i className="bi bi-trash"></i> Delete
                                        </button>
                                    </div>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
        </div>
    );
};

export default AdminUsers;