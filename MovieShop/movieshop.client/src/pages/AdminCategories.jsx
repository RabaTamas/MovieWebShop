import { useState, useEffect } from "react";
import { useAuth } from "../contexts/AuthContext";

const AdminCategories = () => {
    const { token } = useAuth();
    const [categories, setCategories] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [newCategory, setNewCategory] = useState("");
    const [editingCategory, setEditingCategory] = useState(null);
    const [saving, setSaving] = useState(false);
    const [deletingId, setDeletingId] = useState(null);

    // Fetch all categories when component mounts
    useEffect(() => {
        const fetchCategories = async () => {
            try {
                const response = await fetch('https://localhost:7289/api/category', {
                    headers: {
                        'Authorization': `Bearer ${token}`
                    }
                });

                if (!response.ok) {
                    throw new Error(`Error ${response.status}: ${response.statusText}`);
                }

                const data = await response.json();
                setCategories(data);
                setLoading(false);
            } catch (err) {
                console.error("Failed to fetch categories:", err);
                setError(err.message);
                setLoading(false);
            }
        };

        if (token) {
            fetchCategories();
        } else {
            setError("Authentication token is missing");
            setLoading(false);
        }
    }, [token]);

    const handleAddCategory = async (e) => {
        e.preventDefault();
        if (!newCategory.trim()) return;

        setSaving(true);
        setError(null);
        try {
            const response = await fetch('https://localhost:7289/api/category', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${token}`
                },
                body: JSON.stringify({ name: newCategory.trim() })
            });

            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(`Error ${response.status}: ${errorText}`);
            }

            const addedCategory = await response.json();
            setCategories([...categories, addedCategory]);
            setNewCategory("");
        } catch (err) {
            console.error("Failed to add category:", err);
            setError(`Failed to add category: ${err.message}`);
        } finally {
            setSaving(false);
        }
    };

    const handleEditCategory = (category) => {
        setEditingCategory({
            id: category.id,
            name: category.name
        });
        setError(null);
    };

    const handleUpdateCategory = async (e) => {
        e.preventDefault();
        if (!editingCategory || !editingCategory.name.trim()) return;

        setSaving(true);
        setError(null);
        try {
            const response = await fetch(`https://localhost:7289/api/category/${editingCategory.id}`, {
                method: 'PUT',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${token}`
                },
                body: JSON.stringify({ id: editingCategory.id, name: editingCategory.name.trim() })
            });

            if (!response.ok) {
                const errorText = await response.text();
                throw new Error(`Error ${response.status}: ${errorText}`);
            }

            // Update the category in the local state
            setCategories(categories.map(cat =>
                cat.id === editingCategory.id ? { ...cat, name: editingCategory.name.trim() } : cat
            ));
            setEditingCategory(null);
        } catch (err) {
            console.error("Failed to update category:", err);
            setError(`Failed to update category: ${err.message}`);
        } finally {
            setSaving(false);
        }
    };

    const handleDeleteCategory = async (id) => {
        // Check if category exists in current state
        const categoryToDelete = categories.find(cat => cat.id === id);
        if (!categoryToDelete) {
            setError("Category not found in current list. Please refresh the page.");
            return;
        }

        if (!window.confirm(`Are you sure you want to delete the category "${categoryToDelete.name}"? This may affect movies that have this category assigned.`)) {
            return;
        }

        setDeletingId(id);
        setError(null);
        try {
            console.log(`Attempting to delete category with ID: ${id}`);

            const response = await fetch(`https://localhost:7289/api/category/${id}`, {
                method: 'DELETE',
                headers: {
                    'Authorization': `Bearer ${token}`
                }
            });

            if (!response.ok) {
                let errorMessage = `Error ${response.status}: ${response.statusText}`;

                // Try to get more detailed error message from response
                try {
                    const errorText = await response.text();
                    if (errorText) {
                        errorMessage = `Error ${response.status}: ${errorText}`;
                    }
                } catch (e) {
                    // If we can't read the response text, use the default message
                }

                if (response.status === 404) {
                    throw new Error(`Category with ID ${id} was not found. It may have been already deleted.`);
                }

                throw new Error(errorMessage);
            }

            // Remove the deleted category from the state
            setCategories(categories.filter(cat => cat.id !== id));
            console.log(`Successfully deleted category with ID: ${id}`);
        } catch (err) {
            console.error("Failed to delete category:", err);
            setError(`Failed to delete category: ${err.message}`);
        } finally {
            setDeletingId(null);
        }
    };

    if (loading) {
        return (
            <div className="container mt-4">
                <div className="d-flex align-items-center">
                    <div className="spinner-border me-2" role="status"></div>
                    <span>Loading categories...</span>
                </div>
            </div>
        );
    }

    return (
        <div className="container mt-4">
            <h1 className="mb-4">Manage Categories</h1>

            {/* Error Alert */}
            {error && (
                <div className="alert alert-danger alert-dismissible fade show" role="alert">
                    {error}
                    <button
                        type="button"
                        className="btn-close"
                        onClick={() => setError(null)}
                        aria-label="Close"
                    ></button>
                </div>
            )}

            {/* Add New Category Form */}
            <div className="card mb-4">
                <div className="card-header">
                    <h5 className="card-title mb-0">Add New Category</h5>
                </div>
                <div className="card-body">
                    <form onSubmit={handleAddCategory} className="d-flex">
                        <input
                            type="text"
                            className="form-control me-2"
                            placeholder="Category name"
                            value={newCategory}
                            onChange={(e) => setNewCategory(e.target.value)}
                            required
                        />
                        <button
                            type="submit"
                            className="btn btn-primary"
                            disabled={saving || !newCategory.trim()}
                        >
                            {saving ? (
                                <span className="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>
                            ) : (
                                <i className="bi bi-plus-circle me-1"></i>
                            )}
                            Add
                        </button>
                    </form>
                </div>
            </div>

            {/* Edit Category Form - shown only when editing */}
            {editingCategory && (
                <div className="card mb-4">
                    <div className="card-header d-flex justify-content-between align-items-center">
                        <h5 className="card-title mb-0">Edit Category</h5>
                        <button
                            type="button"
                            className="btn-close"
                            aria-label="Close"
                            onClick={() => setEditingCategory(null)}
                        ></button>
                    </div>
                    <div className="card-body">
                        <form onSubmit={handleUpdateCategory} className="d-flex">
                            <input
                                type="text"
                                className="form-control me-2"
                                value={editingCategory.name}
                                onChange={(e) => setEditingCategory({ ...editingCategory, name: e.target.value })}
                                required
                            />
                            <button
                                type="submit"
                                className="btn btn-success"
                                disabled={saving || !editingCategory.name.trim()}
                            >
                                {saving ? (
                                    <span className="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>
                                ) : (
                                    <i className="bi bi-check-circle me-1"></i>
                                )}
                                Save
                            </button>
                        </form>
                    </div>
                </div>
            )}

            {/* Categories List */}
            <div className="card">
                <div className="card-header">
                    <h5 className="card-title mb-0">All Categories ({categories.length})</h5>
                </div>
                <div className="card-body">
                    {categories.length === 0 ? (
                        <p className="text-muted">No categories found</p>
                    ) : (
                        <div className="table-responsive">
                            <table className="table table-hover">
                                <thead>
                                    <tr>
                                        <th>ID</th>
                                        <th>Name</th>
                                        <th>Actions</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {categories.map(category => (
                                        <tr key={category.id}>
                                            <td>{category.id}</td>
                                            <td>{category.name}</td>
                                            <td>
                                                <div className="btn-group">
                                                    <button
                                                        className="btn btn-sm btn-outline-primary"
                                                        onClick={() => handleEditCategory(category)}
                                                        disabled={editingCategory !== null || deletingId === category.id}
                                                    >
                                                        <i className="bi bi-pencil"></i> Edit
                                                    </button>
                                                    <button
                                                        className="btn btn-sm btn-outline-danger"
                                                        onClick={() => handleDeleteCategory(category.id)}
                                                        disabled={deletingId === category.id}
                                                    >
                                                        {deletingId === category.id ? (
                                                            <span className="spinner-border spinner-border-sm me-1" role="status" aria-hidden="true"></span>
                                                        ) : (
                                                            <i className="bi bi-trash"></i>
                                                        )}
                                                        Delete
                                                    </button>
                                                </div>
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    )}
                </div>
            </div>
        </div>
    );
};

export default AdminCategories;