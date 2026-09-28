import { useState, useEffect } from "react";
import { AlertCircle, Check, FolderTree, Pencil, Plus, Trash2, X } from "lucide-react";
import { useAuth } from "../contexts/AuthContext";

import API_BASE_URL from "../config/api";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardAction } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { LoadingState, Spinner } from "@/components/ui/spinner";
import { PageContainer, PageHeader } from "@/components/ui/page";

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
                const response = await fetch(`${API_BASE_URL}/api/category`, {
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
            const response = await fetch(`${API_BASE_URL}/api/category`, {
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
            const response = await fetch(`${API_BASE_URL}/api/category/${editingCategory.id}`, {
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
            const response = await fetch(`${API_BASE_URL}/api/category/${id}`, {
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
                } catch {
                    // If we can't read the response text, use the default message
                }

                if (response.status === 404) {
                    throw new Error(`Category with ID ${id} was not found. It may have been already deleted.`);
                }

                throw new Error(errorMessage);
            }

            // Remove the deleted category from the state
            setCategories(categories.filter(cat => cat.id !== id));
        } catch (err) {
            console.error("Failed to delete category:", err);
            setError(`Failed to delete category: ${err.message}`);
        } finally {
            setDeletingId(null);
        }
    };

    if (loading) {
        return <LoadingState label="Loading categories..." />;
    }

    return (
        <PageContainer size="lg" className="max-w-4xl">
            <PageHeader title="Manage Categories" icon={FolderTree} />

            <div className="space-y-6">
                {/* Error Alert */}
                {error && (
                    <Alert variant="destructive" className="pr-10">
                        <AlertCircle />
                        <AlertDescription>{error}</AlertDescription>
                        <button
                            type="button"
                            className="absolute top-2.5 right-2.5 cursor-pointer rounded p-1 opacity-70 hover:opacity-100"
                            onClick={() => setError(null)}
                            aria-label="Close"
                        >
                            <X className="size-4" />
                        </button>
                    </Alert>
                )}

                {/* Add / Edit Category Form */}
                <div className="grid gap-6 md:grid-cols-2">
                    <Card className="gap-4">
                        <CardHeader>
                            <CardTitle>Add New Category</CardTitle>
                        </CardHeader>
                        <CardContent>
                            <form onSubmit={handleAddCategory} className="flex gap-2">
                                <Input
                                    type="text"
                                    placeholder="Category name"
                                    value={newCategory}
                                    onChange={(e) => setNewCategory(e.target.value)}
                                    required
                                />
                                <Button type="submit" disabled={saving || !newCategory.trim()}>
                                    {saving ? <Spinner /> : <Plus />}
                                    Add
                                </Button>
                            </form>
                        </CardContent>
                    </Card>

                    {/* Edit Category Form - shown only when editing */}
                    {editingCategory && (
                        <Card className="gap-4 border-primary/40">
                            <CardHeader>
                                <CardTitle>Edit Category</CardTitle>
                                <CardAction>
                                    <Button variant="ghost" size="icon-sm" aria-label="Close" onClick={() => setEditingCategory(null)}>
                                        <X />
                                    </Button>
                                </CardAction>
                            </CardHeader>
                            <CardContent>
                                <form onSubmit={handleUpdateCategory} className="flex gap-2">
                                    <Input
                                        type="text"
                                        value={editingCategory.name}
                                        onChange={(e) => setEditingCategory({ ...editingCategory, name: e.target.value })}
                                        autoFocus
                                        required
                                    />
                                    <Button type="submit" variant="success" disabled={saving || !editingCategory.name.trim()}>
                                        {saving ? <Spinner /> : <Check />}
                                        Save
                                    </Button>
                                </form>
                            </CardContent>
                        </Card>
                    )}
                </div>

                {/* Categories List */}
                <Card className="gap-4">
                    <CardHeader>
                        <CardTitle>All Categories ({categories.length})</CardTitle>
                    </CardHeader>
                    <CardContent>
                        {categories.length === 0 ? (
                            <p className="text-sm text-muted-foreground">No categories found</p>
                        ) : (
                            <Table containerClassName="rounded-lg border">
                                <TableHeader>
                                    <TableRow className="hover:bg-transparent">
                                        <TableHead className="w-20">ID</TableHead>
                                        <TableHead>Name</TableHead>
                                        <TableHead className="text-right">Actions</TableHead>
                                    </TableRow>
                                </TableHeader>
                                <TableBody>
                                    {categories.map(category => (
                                        <TableRow key={category.id}>
                                            <TableCell className="text-muted-foreground">{category.id}</TableCell>
                                            <TableCell className="font-medium">{category.name}</TableCell>
                                            <TableCell>
                                                <div className="flex justify-end gap-1">
                                                    <Button
                                                        variant="ghost"
                                                        size="sm"
                                                        onClick={() => handleEditCategory(category)}
                                                        disabled={editingCategory !== null || deletingId === category.id}
                                                    >
                                                        <Pencil /> Edit
                                                    </Button>
                                                    <Button
                                                        variant="ghost"
                                                        size="sm"
                                                        className="text-destructive hover:text-destructive"
                                                        onClick={() => handleDeleteCategory(category.id)}
                                                        disabled={deletingId === category.id}
                                                    >
                                                        {deletingId === category.id ? <Spinner /> : <Trash2 />}
                                                        Delete
                                                    </Button>
                                                </div>
                                            </TableCell>
                                        </TableRow>
                                    ))}
                                </TableBody>
                            </Table>
                        )}
                    </CardContent>
                </Card>
            </div>
        </PageContainer>
    );
};

export default AdminCategories;
