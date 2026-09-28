import { useState, useEffect } from "react";
import { toast } from "sonner";
import { AlertCircle, Minus, Plus, RefreshCw, ShoppingCart, Trash2 } from "lucide-react";
import { useAuth } from "../contexts/AuthContext";
import { UserRoles } from "../constants/UserRoles";

import API_BASE_URL from "../config/api";
import { formatPrice } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Card, CardAction, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { NativeSelect } from "@/components/ui/native-select";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Table, TableBody, TableCell, TableFooter, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { LoadingState } from "@/components/ui/spinner";
import { EmptyState, PageContainer, PageHeader } from "@/components/ui/page";

const AdminShoppingCarts = () => {
    const { token, user } = useAuth();
    const [carts, setCarts] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
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
            toast.success("Cart cleared successfully!");
        } catch (err) {
            console.error("Failed to clear cart:", err);
            toast.error(`Failed to clear cart: ${err.message}`);
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
            toast.error(`Failed to remove item: ${err.message}`);
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
            toast.error(`Failed to update quantity: ${err.message}`);
        }
    };

    const calculateTotal = (items) => {
        return items.reduce((sum, item) => sum + (item.priceAtOrder * item.quantity), 0);
    };

    if (error) {
        return (
            <PageContainer size="md">
                <Alert variant="destructive">
                    <AlertCircle />
                    <AlertDescription>
                        <span>Error: {error}</span>
                        <Button size="sm" variant="outline" className="mt-2" onClick={() => window.location.reload()}>
                            <RefreshCw /> Refresh Page
                        </Button>
                    </AlertDescription>
                </Alert>
            </PageContainer>
        );
    }

    return (
        <PageContainer size="xl">
            <PageHeader title="Manage Shopping Carts" icon={ShoppingCart}>
                <div className="flex w-full items-center gap-2 sm:w-auto">
                    <Label htmlFor="cart-user" className="whitespace-nowrap text-muted-foreground">Select User</Label>
                    <NativeSelect
                        id="cart-user"
                        containerClassName="sm:w-80"
                        value={selectedUser?.id || ''}
                        onChange={(e) => handleSelectUser(parseInt(e.target.value))}
                    >
                        {users.map(user => (
                            <option key={user.id} value={user.id}>
                                {user.name} ({user.email})
                            </option>
                        ))}
                    </NativeSelect>
                </div>
            </PageHeader>

            {loading ? (
                <LoadingState label="Loading shopping carts..." className="min-h-[30vh]" />
            ) : selectedUser && carts.length > 0 && (
                <Card className="gap-4">
                    <CardHeader>
                        <CardTitle>Cart for {selectedUser.name}</CardTitle>
                        <CardDescription>{selectedUser.email}</CardDescription>
                        <CardAction>
                            <Button
                                variant="destructive"
                                size="sm"
                                onClick={() => handleClearCart(selectedUser.id)}
                                disabled={!carts[0]?.items?.length}
                            >
                                <Trash2 /> Clear Cart
                            </Button>
                        </CardAction>
                    </CardHeader>
                    <CardContent>
                        {carts[0]?.items?.length ? (
                            <Table containerClassName="rounded-lg border">
                                <TableHeader>
                                    <TableRow className="hover:bg-transparent">
                                        <TableHead>Movie ID</TableHead>
                                        <TableHead>Title</TableHead>
                                        <TableHead>Price</TableHead>
                                        <TableHead>Quantity</TableHead>
                                        <TableHead>Subtotal</TableHead>
                                        <TableHead className="text-right">Actions</TableHead>
                                    </TableRow>
                                </TableHeader>
                                <TableBody>
                                    {carts[0].items.map(item => (
                                        <TableRow key={item.movieId}>
                                            <TableCell className="text-muted-foreground">{item.movieId}</TableCell>
                                            <TableCell className="font-medium">{item.title}</TableCell>
                                            <TableCell className="whitespace-nowrap">{formatPrice(item.priceAtOrder)}</TableCell>
                                            <TableCell>
                                                <div className="inline-flex items-center rounded-md border">
                                                    <Button
                                                        variant="ghost"
                                                        size="icon-sm"
                                                        className="rounded-r-none"
                                                        aria-label="Decrease quantity"
                                                        onClick={() => handleUpdateQuantity(selectedUser.id, item.movieId, Math.max(1, item.quantity - 1))}
                                                        disabled={item.quantity <= 1}
                                                    >
                                                        <Minus />
                                                    </Button>
                                                    <input
                                                        type="number"
                                                        className="h-8 w-12 border-x bg-transparent text-center text-sm outline-none [appearance:textfield] [&::-webkit-inner-spin-button]:appearance-none"
                                                        aria-label="Quantity"
                                                        value={item.quantity}
                                                        min="1"
                                                        onChange={(e) => {
                                                            const value = parseInt(e.target.value);
                                                            if (value > 0) {
                                                                handleUpdateQuantity(selectedUser.id, item.movieId, value);
                                                            }
                                                        }}
                                                    />
                                                    <Button
                                                        variant="ghost"
                                                        size="icon-sm"
                                                        className="rounded-l-none"
                                                        aria-label="Increase quantity"
                                                        onClick={() => handleUpdateQuantity(selectedUser.id, item.movieId, item.quantity + 1)}
                                                    >
                                                        <Plus />
                                                    </Button>
                                                </div>
                                            </TableCell>
                                            <TableCell className="whitespace-nowrap">{formatPrice(item.priceAtOrder * item.quantity)}</TableCell>
                                            <TableCell className="text-right">
                                                <Button
                                                    variant="ghost"
                                                    size="sm"
                                                    className="text-destructive hover:text-destructive"
                                                    onClick={() => handleRemoveItem(selectedUser.id, item.movieId)}
                                                >
                                                    <Trash2 /> Remove
                                                </Button>
                                            </TableCell>
                                        </TableRow>
                                    ))}
                                </TableBody>
                                <TableFooter>
                                    <TableRow className="hover:bg-transparent">
                                        <TableCell colSpan={4} className="text-right font-bold">Total:</TableCell>
                                        <TableCell colSpan={2} className="font-bold">{formatPrice(calculateTotal(carts[0].items))}</TableCell>
                                    </TableRow>
                                </TableFooter>
                            </Table>
                        ) : (
                            <EmptyState icon={ShoppingCart} title="This user's cart is empty." className="py-10" />
                        )}
                    </CardContent>
                </Card>
            )}
        </PageContainer>
    );
};

export default AdminShoppingCarts;
