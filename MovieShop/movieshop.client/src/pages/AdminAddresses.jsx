import { useState, useEffect } from "react";
import { useAuth } from "../contexts/AuthContext";

const AdminAddresses = () => {
    const { token } = useAuth();
    const [addresses, setAddresses] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [editingAddress, setEditingAddress] = useState(null);
    const [editForm, setEditForm] = useState({
        street: "",
        city: "",
        zip: ""
    });

    // Fetch all addresses when component mounts
    useEffect(() => {
        const fetchAddresses = async () => {
            try {
                const response = await fetch(`https://localhost:7289/api/admin/addresses`, {
                    headers: {
                        'Authorization': `Bearer ${token}`
                    }
                });

                if (!response.ok) {
                    throw new Error(`Error ${response.status}: ${response.statusText}`);
                }

                const data = await response.json();
                setAddresses(data);
                setLoading(false);
            } catch (err) {
                console.error("Failed to fetch addresses:", err);
                setError(err.message);
                setLoading(false);
            }
        };

        if (token) {
            fetchAddresses();
        } else {
            setError("Authentication token is missing");
            setLoading(false);
        }
    }, [token]);

    const handleEdit = (address) => {
        setEditingAddress(address.id);
        setEditForm({
            street: address.street,
            city: address.city,
            zip: address.zip
        });
    };

    const handleCancelEdit = () => {
        setEditingAddress(null);
        setEditForm({
            street: "",
            city: "",
            zip: ""
        });
    };

    const handleSaveEdit = async (addressId) => {
        // Validation
        if (!editForm.street || !editForm.city || !editForm.zip) {
            alert("All fields must be filled!");
            return;
        }

        if (!/^\d{4}$/.test(editForm.zip)) {
            alert("Zip must be 4 numbers");
            return;
        }

        try {
            const response = await fetch(`https://localhost:7289/api/admin/addresses/${addressId}`, {
                method: 'PUT',
                headers: {
                    'Authorization': `Bearer ${token}`,
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(editForm)
            });

            if (!response.ok) {
                throw new Error(`Error ${response.status}: ${response.statusText}`);
            }

            const updatedAddress = await response.json();

            // Update the address in the state
            setAddresses(addresses.map(address =>
                address.id === addressId ? { ...address, ...updatedAddress } : address
            ));

            setEditingAddress(null);
            setEditForm({ street: "", city: "", zip: "" });
        } catch (err) {
            console.error("Failed to update address:", err);
            setError(err.message);
        }
    };

    const handleDelete = async (addressId) => {
        if (!window.confirm("Are you sure you want to delete this address? This action cannot be undone.")) {
            return;
        }

        try {
            const response = await fetch(`https://localhost:7289/api/admin/addresses/${addressId}`, {
                method: 'DELETE',
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });

            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(errorText || `Error ${response.status}: ${response.statusText}`);
            }

            // Remove the deleted address from the state
            setAddresses(addresses.filter(address => address.id !== addressId));
        } catch (err) {
            console.error("Failed to delete address:", err);
            alert(`Failed to delete address: ${err.message}`);
        }
    };

    const handleInputChange = (e) => {
        const { name, value } = e.target;
        setEditForm(prev => ({ ...prev, [name]: value }));
    };

    if (loading) {
        return (
            <div className="container mt-4">
                <div className="d-flex align-items-center">
                    <div className="spinner-border me-2" role="status"></div>
                    <span>Loading addresses...</span>
                </div>
            </div>
        );
    }

    if (error) {
        return (
            <div className="container mt-4">
                <div className="alert alert-danger">
                    <strong>Error:</strong> {error}
                </div>
            </div>
        );
    }

    return (
        <div className="container mt-4">
            <div className="d-flex justify-content-between align-items-center mb-4">
                <h1>Manage Addresses</h1>
                <div className="text-muted">
                    Total: {addresses.length} addresses
                </div>
            </div>

            {addresses.length === 0 ? (
                <div className="alert alert-info">
                    <i className="bi bi-info-circle me-2"></i>
                    No addresses found in the system.
                </div>
            ) : (
                <div className="table-responsive">
                    <table className="table table-striped table-hover">
                        <thead className="table-dark">
                            <tr>
                                <th>ID</th>
                                <th>User</th>
                                <th>Street</th>
                                <th>City</th>
                                <th>Zip</th>
                                <th>Orders</th>
                                <th>Actions</th>
                            </tr>
                        </thead>
                        <tbody>
                            {addresses.map(address => (
                                <tr key={address.id}>
                                    <td>{address.id}</td>
                                    <td>
                                        <div>
                                            <strong>{address.userName}</strong>
                                            <br />
                                            <small className="text-muted">{address.userEmail}</small>
                                        </div>
                                    </td>
                                    <td>
                                        {editingAddress === address.id ? (
                                            <input
                                                type="text"
                                                name="street"
                                                value={editForm.street}
                                                onChange={handleInputChange}
                                                className="form-control form-control-sm"
                                                maxLength="100"
                                            />
                                        ) : (
                                            address.street
                                        )}
                                    </td>
                                    <td>
                                        {editingAddress === address.id ? (
                                            <input
                                                type="text"
                                                name="city"
                                                value={editForm.city}
                                                onChange={handleInputChange}
                                                className="form-control form-control-sm"
                                                maxLength="50"
                                            />
                                        ) : (
                                            address.city
                                        )}
                                    </td>
                                    <td>
                                        {editingAddress === address.id ? (
                                            <input
                                                type="text"
                                                name="zip"
                                                value={editForm.zip}
                                                onChange={handleInputChange}
                                                className="form-control form-control-sm"
                                                maxLength="4"
                                                pattern="\d{4}"
                                            />
                                        ) : (
                                            address.zip
                                        )}
                                    </td>
                                    <td>
                                        <div>
                                            <small className="text-muted">
                                                Billing: {address.billingOrdersCount || 0}
                                                <br />
                                                Shipping: {address.shippingOrdersCount || 0}
                                            </small>
                                        </div>
                                    </td>
                                    <td>
                                        <div className="btn-group btn-group-sm">
                                            {editingAddress === address.id ? (
                                                <>
                                                    <button
                                                        className="btn btn-success"
                                                        onClick={() => handleSaveEdit(address.id)}
                                                        title="Save changes"
                                                    >
                                                        <i className="bi bi-check"></i>
                                                    </button>
                                                    <button
                                                        className="btn btn-secondary"
                                                        onClick={handleCancelEdit}
                                                        title="Cancel"
                                                    >
                                                        <i className="bi bi-x"></i>
                                                    </button>
                                                </>
                                            ) : (
                                                <>
                                                    <button
                                                        className="btn btn-outline-primary"
                                                        onClick={() => handleEdit(address)}
                                                        title="Edit address"
                                                    >
                                                        <i className="bi bi-pencil"></i>
                                                    </button>
                                                    <button
                                                        className="btn btn-outline-danger"
                                                        onClick={() => handleDelete(address.id)}
                                                        title="Delete address"
                                                        disabled={
                                                            (address.billingOrdersCount > 0) ||
                                                            (address.shippingOrdersCount > 0)
                                                        }
                                                    >
                                                        <i className="bi bi-trash"></i>
                                                    </button>
                                                </>
                                            )}
                                        </div>
                                        {((address.billingOrdersCount > 0) || (address.shippingOrdersCount > 0)) && (
                                            <div className="mt-1">
                                                <small className="text-warning">
                                                    <i className="bi bi-exclamation-triangle"></i>
                                                    Used in orders
                                                </small>
                                            </div>
                                        )}
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            )}
        </div>
    );
};

export default AdminAddresses;