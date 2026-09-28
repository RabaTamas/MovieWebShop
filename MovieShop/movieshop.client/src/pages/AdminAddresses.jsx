import { useState, useEffect } from "react";
import { toast } from "sonner";
import { AlertCircle, Check, MapPin, Pencil, Trash2, TriangleAlert, X } from "lucide-react";
import { useAuth } from "../contexts/AuthContext";

import API_BASE_URL from "../config/api";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { LoadingState } from "@/components/ui/spinner";
import { EmptyState, PageContainer, PageHeader } from "@/components/ui/page";

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
                const response = await fetch(`${API_BASE_URL}/api/admin/addresses`, {
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
            toast.error("All fields must be filled!");
            return;
        }

        if (!/^\d{4}$/.test(editForm.zip)) {
            toast.error("Zip must be 4 numbers");
            return;
        }

        try {
            const response = await fetch(`${API_BASE_URL}/api/admin/addresses/${addressId}`, {
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
            const response = await fetch(`${API_BASE_URL}/api/admin/addresses/${addressId}`, {
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
            toast.error(`Failed to delete address: ${err.message}`);
        }
    };

    const handleInputChange = (e) => {
        const { name, value } = e.target;
        setEditForm(prev => ({ ...prev, [name]: value }));
    };

    if (loading) {
        return <LoadingState label="Loading addresses..." />;
    }

    if (error) {
        return (
            <PageContainer size="md">
                <Alert variant="destructive">
                    <AlertCircle />
                    <AlertDescription><span><strong>Error:</strong> {error}</span></AlertDescription>
                </Alert>
            </PageContainer>
        );
    }

    return (
        <PageContainer size="xl">
            <PageHeader title="Manage Addresses" icon={MapPin} description={`Total: ${addresses.length} addresses`} />

            {addresses.length === 0 ? (
                <EmptyState icon={MapPin} title="No addresses found in the system." />
            ) : (
                <Card className="py-0">
                    <Table>
                        <TableHeader>
                            <TableRow className="hover:bg-transparent">
                                <TableHead className="w-14">ID</TableHead>
                                <TableHead>User</TableHead>
                                <TableHead>Street</TableHead>
                                <TableHead>City</TableHead>
                                <TableHead>Zip</TableHead>
                                <TableHead>Orders</TableHead>
                                <TableHead className="text-right">Actions</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {addresses.map(address => {
                                const isEditing = editingAddress === address.id;
                                return (
                                    <TableRow key={address.id} className={isEditing ? "bg-primary/5" : undefined}>
                                        <TableCell className="text-muted-foreground">{address.id}</TableCell>
                                        <TableCell>
                                            <div className="font-medium">{address.userName}</div>
                                            <div className="text-xs text-muted-foreground">{address.userEmail}</div>
                                        </TableCell>
                                        <TableCell>
                                            {isEditing ? (
                                                <Input type="text" name="street" value={editForm.street} onChange={handleInputChange} className="h-8 min-w-40" maxLength="100" />
                                            ) : (
                                                address.street
                                            )}
                                        </TableCell>
                                        <TableCell>
                                            {isEditing ? (
                                                <Input type="text" name="city" value={editForm.city} onChange={handleInputChange} className="h-8 min-w-28" maxLength="50" />
                                            ) : (
                                                address.city
                                            )}
                                        </TableCell>
                                        <TableCell>
                                            {isEditing ? (
                                                <Input type="text" name="zip" value={editForm.zip} onChange={handleInputChange} className="h-8 w-20" maxLength="4" pattern="\d{4}" />
                                            ) : (
                                                address.zip
                                            )}
                                        </TableCell>
                                        <TableCell className="text-muted-foreground">
                                            {address.billingOrdersCount || 0}
                                        </TableCell>
                                        <TableCell>
                                            <div className="flex justify-end gap-1">
                                                {isEditing ? (
                                                    <>
                                                        <Button size="icon-sm" variant="success" onClick={() => handleSaveEdit(address.id)} title="Save changes" aria-label="Save changes">
                                                            <Check />
                                                        </Button>
                                                        <Button size="icon-sm" variant="outline" onClick={handleCancelEdit} title="Cancel" aria-label="Cancel">
                                                            <X />
                                                        </Button>
                                                    </>
                                                ) : (
                                                    <>
                                                        <Button size="icon-sm" variant="ghost" onClick={() => handleEdit(address)} title="Edit address" aria-label="Edit address">
                                                            <Pencil />
                                                        </Button>
                                                        <Button
                                                            size="icon-sm"
                                                            variant="ghost"
                                                            className="text-destructive hover:text-destructive"
                                                            onClick={() => handleDelete(address.id)}
                                                            title="Delete address"
                                                            aria-label="Delete address"
                                                            disabled={address.billingOrdersCount > 0}
                                                        >
                                                            <Trash2 />
                                                        </Button>
                                                    </>
                                                )}
                                            </div>
                                            {(address.billingOrdersCount > 0) && (
                                                <div className="mt-1 flex items-center justify-end gap-1 text-xs text-primary">
                                                    <TriangleAlert className="size-3" />
                                                    Used in orders
                                                </div>
                                            )}
                                        </TableCell>
                                    </TableRow>
                                );
                            })}
                        </TableBody>
                    </Table>
                </Card>
            )}
        </PageContainer>
    );
};

export default AdminAddresses;
