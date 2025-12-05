import { useState, useEffect } from "react";
import { useAuth } from "../contexts/AuthContext";
import { UserRoles } from "../constants/UserRoles";
import API_BASE_URL from '../config/api';

const AdminShoppingCarts = () => {
    const { token, user } = useAuth();
    const [carts, setCarts] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [expandedCart, setExpandedCart] = useState(null);
    const [selectedUser, setSelectedUser] = useState(null);
    const [users, setUsers] = useState([]);

    useEffect(() => {
        // Check if user is admin
        if (!user || user.role !== UserRoles.Admin) {
            setError("Unauthorized access. Admin privileges required.");
            setLoading(false);
            return;
        }

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

                // If users are loaded, fetch the first user's cart
                if (data.length > 0) {
                    setSelectedUser(data[0]);
                    fetchCartForUser(data[0].id);
                } else {
                    setLoading(false);
                }
            } catch (err) {
                console.error("Failed to fetch users:", err);
                setError(err.message);
                setLoading(false);
            }
        };

        fetchUsers();
    }, [token, user]);

    const fetchCartForUser = async (userId) => {
        setLoading(true);
        try {
            const response = await fetch(`${API_BASE_URL}/api/admin/shopping-carts/${userId}`, {
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });

            if (response.status === 404) {
                // If cart doesn't exist, show empty cart
                setCarts([{ id: 0, userId: userId, items: [] }]);
                setLoading(false);
                return;
            }

            if (!response.ok) {
                throw new Error(`Error ${response.status}: ${response.statusText}`);
            }

            const data = await response.json();
            setCarts([data]); // Set as array with single cart for consistency
            setLoading(false);
        } catch (err) {
            console.error(`Failed to fetch cart for user ${userId}:`, err);
            setError(err.message);
            setLoading(false);
        }
    };

    const handleSelectUser = (userId) => {
        const user = users.find(u => u.id === userId);
        setSelectedUser(user);
        fetchCartForUser(userId);
    };

    const handleClearCart = async (userId) => {
        if (!window.confirm("Are you sure you want to clear this user's cart?")) {
            return;
        }

        try {
            const response = await fetch(`${API_BASE_URL}/api/admin/shopping-carts/${userId}/clear`, {
                method: 'DELETE',
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });

            if (!response.ok) {
                throw new Error(`Error ${response.status}: ${response.statusText}`);
            }

            // Refresh cart data
            fetchCartForUser(userId);
            alert("Cart cleared successfully!");
        } catch (err) {
            console.error("Failed to clear cart:", err);
            alert(`Failed to clear cart: ${err.message}`);
        }
    };

    const handleRemoveItem = async (userId, movieId) => {
        if (!window.confirm("Are you sure you want to remove this item from the cart?")) {
            return;
        }

        try {
            const response = await fetch(`${API_BASE_URL}/api/admin/shopping-carts/${userId}/remove/${movieId}`, {
                method: 'DELETE',
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });

            if (!response.ok) {
                throw new Error(`Error ${response.status}: ${response.statusText}`);
            }

            // Refresh cart data
            fetchCartForUser(userId);
        } catch (err) {
            console.error("Failed to remove item:", err);
            alert(`Failed to remove item: ${err.message}`);
        }
    };

    const handleUpdateQuantity = async (userId, movieId, quantity) => {
        try {
            const response = await fetch(`${API_BASE_URL}/api/admin/shopping-carts/${userId}/update`, {
                method: 'PUT',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${token}`
                },
                body: JSON.stringify({
                    movieId,
                    quantity
                })
            });

            if (!response.ok) {
                throw new Error(`Error ${response.status}: ${response.statusText}`);
            }

            // Refresh cart data
            fetchCartForUser(userId);
        } catch (err) {
            console.error("Failed to update quantity:", err);
            alert(`Failed to update quantity: ${err.message}`);
        }
    };

    const calculateTotal = (items) => {
        return items.reduce((sum, item) => sum + (item.priceAtOrder * item.quantity), 0);
    };

    if (loading) {
        return <div className="container mt-4"><div className="spinner-border" role="status"></div> Loading shopping carts...</div>;
    }

    if (error) {
        return (
            <div className="container mt-4">
                <div className="alert alert-danger">
                    Error: {error}
                    <button className="btn btn-sm btn-outline-secondary ms-2" onClick={() => window.location.reload()}>
                        Refresh Page
                    </button>
                </div>
            </div>
        );
    }

    return (
        <div className="container mt-4">
            <h1>Manage Shopping Carts</h1>

            <div className="row mb-4">
                <div className="col-md-6">
                    <div className="card">
                        <div className="card-header bg-primary text-white">
                            <h5 className="mb-0">Select User</h5>
                        </div>
                        <div className="card-body">
                            <select
                                className="form-select"
                                value={selectedUser?.id || ''}
                                onChange={(e) => handleSelectUser(parseInt(e.target.value))}
                            >
                                {users.map(user => (
                                    <option key={user.id} value={user.id}>
                                        {user.name} ({user.email})
                                    </option>
                                ))}
                            </select>
                        </div>
                    </div>
                </div>
            </div>

            {selectedUser && carts.length > 0 && (
                <div className="card mb-4">
                    <div className="card-header d-flex justify-content-between align-items-center">
                        <h5 className="mb-0">
                            Cart for {selectedUser.name} ({selectedUser.email})
                        </h5>
                        <button
                            className="btn btn-danger btn-sm"
                            onClick={() => handleClearCart(selectedUser.id)}
                            disabled={!carts[0]?.items?.length}
                        >
                            <i className="bi bi-trash me-1"></i>
                            Clear Cart
                        </button>
                    </div>
                    <div className="card-body">
                        {carts[0]?.items?.length ? (
                            <div className="table-responsive">
                                <table className="table table-striped">
                                    <thead>
                                        <tr>
                                            <th>Movie ID</th>
                                            <th>Title</th>
                                            <th>Price</th>
                                            <th>Quantity</th>
                                            <th>Subtotal</th>
                                            <th>Actions</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {carts[0].items.map(item => (
                                            <tr key={item.movieId}>
                                                <td>{item.movieId}</td>
                                                <td>{item.title}</td>
                                                <td>${item.priceAtOrder}</td>
                                                <td>
                                                    <div className="input-group input-group-sm" style={{ maxWidth: "150px" }}>
                                                        <button
                                                            className="btn btn-outline-secondary"
                                                            onClick={() => handleUpdateQuantity(selectedUser.id, item.movieId, Math.max(1, item.quantity - 1))}
                                                            disabled={item.quantity <= 1}
                                                        >
                                                            -
                                                        </button>
                                                        <input
                                                            type="number"
                                                            className="form-control text-center"
                                                            value={item.quantity}
                                                            min="1"
                                                            onChange={(e) => {
                                                                const value = parseInt(e.target.value);
                                                                if (value > 0) {
                                                                    handleUpdateQuantity(selectedUser.id, item.movieId, value);
                                                                }
                                                            }}
                                                        />
                                                        <button
                                                            className="btn btn-outline-secondary"
                                                            onClick={() => handleUpdateQuantity(selectedUser.id, item.movieId, item.quantity + 1)}
                                                        >
                                                            +
                                                        </button>
                                                    </div>
                                                </td>
                                                <td>${(item.priceAtOrder * item.quantity).toFixed(2)}</td>
                                                <td>
                                                    <button
                                                        className="btn btn-sm btn-outline-danger"
                                                        onClick={() => handleRemoveItem(selectedUser.id, item.movieId)}
                                                    >
                                                        <i className="bi bi-trash"></i> Remove
                                                    </button>
                                                </td>
                                            </tr>
                                        ))}
                                    </tbody>
                                    <tfoot>
                                        <tr>
                                            <td colSpan="4" className="text-end fw-bold">Total:</td>
                                            <td colSpan="2" className="fw-bold">${calculateTotal(carts[0].items).toFixed(2)}</td>
                                        </tr>
                                    </tfoot>
                                </table>
                            </div>
                        ) : (
                            <div className="alert alert-info">
                                This user's cart is empty.
                            </div>
                        )}
                    </div>
                </div>
            )}
        </div>
    );
};

export default AdminShoppingCarts;