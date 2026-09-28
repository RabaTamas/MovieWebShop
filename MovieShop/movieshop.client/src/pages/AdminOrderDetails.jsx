import React, { useState, useEffect } from 'react';
import { useParams, Link, useNavigate } from 'react-router-dom';
import { toast } from 'sonner';
import { AlertCircle, ArrowLeft, Ban, MapPin, ReceiptText, UserRound } from 'lucide-react';
import { useAuth } from '../contexts/AuthContext';
import { orderService } from '../services/orderService';
import OrderStatusBadge from '../components/OrderStatusBadge';
import { ORDER_STATUSES } from './AdminOrders';
import { formatPrice } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { NativeSelect } from '@/components/ui/native-select';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Table, TableBody, TableCell, TableFooter, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { LoadingState, Spinner } from '@/components/ui/spinner';
import { PageContainer, PageHeader } from '@/components/ui/page';

const InfoRow = ({ label, children }) => (
    <div className="flex justify-between gap-4 py-1.5 text-sm">
        <span className="text-muted-foreground">{label}</span>
        <span className="text-right font-medium">{children}</span>
    </div>
);

const AdminOrderDetails = () => {
    const { id } = useParams();
    const [order, setOrder] = useState(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [newStatus, setNewStatus] = useState('');
    const [updateLoading, setUpdateLoading] = useState(false);
    const { token } = useAuth();
    const navigate = useNavigate();

    useEffect(() => {
        const fetchOrderDetails = async () => {
            try {
                setLoading(true);
                const orderData = await orderService.getOrderById(id, token);
                setOrder(orderData);
                setNewStatus(orderData.status);
                setError(null);
            } catch (err) {
                setError('Failed to load order details. Please try again.');
                console.error(err);
            } finally {
                setLoading(false);
            }
        };

        fetchOrderDetails();
    }, [id, token]);

    // statusOverride: a "Cancel Order" gomb közvetlenül adja át az új állapotot,
    // mert a setNewStatus még nem frissül ugyanabban a kattintásban
    const handleStatusChange = async (statusOverride) => {
        const status = typeof statusOverride === 'string' ? statusOverride : newStatus;
        try {
            setUpdateLoading(true);
            await orderService.updateOrderStatus(id, status, token);
            // Update the order object
            setOrder({ ...order, status });
            setNewStatus(status);
            toast.success('Order status updated successfully!');
        } catch (err) {
            toast.error('Failed to update order status. Please try again.');
            console.error(err);
        } finally {
            setUpdateLoading(false);
        }
    };

    const formatDate = (dateString) => {
        const date = new Date(dateString);
        return date.toLocaleDateString() + ' ' + date.toLocaleTimeString();
    };

    if (loading) return <LoadingState label="Loading order details..." />;
    if (error || !order) {
        return (
            <PageContainer size="md">
                <Alert variant="destructive">
                    <AlertCircle />
                    <AlertDescription>{error || 'Order not found'}</AlertDescription>
                </Alert>
            </PageContainer>
        );
    }

    const address = order.billingAddress;

    return (
        <PageContainer size="lg" className="max-w-5xl">
            <PageHeader title={`Order #${order.id} Details`} icon={ReceiptText}>
                <Button variant="outline" asChild>
                    <Link to="/admin/orders"><ArrowLeft />Back to Orders</Link>
                </Button>
            </PageHeader>

            <div className="grid gap-6 md:grid-cols-3">
                <Card className="gap-3">
                    <CardHeader>
                        <CardTitle className="text-sm text-muted-foreground">Order Information</CardTitle>
                    </CardHeader>
                    <CardContent className="divide-y">
                        <InfoRow label="Order ID">#{order.id}</InfoRow>
                        <InfoRow label="Date">{formatDate(order.orderDate)}</InfoRow>
                        <InfoRow label="Total">{formatPrice(order.totalPrice)}</InfoRow>
                    </CardContent>
                </Card>

                <Card className="gap-3">
                    <CardHeader>
                        <CardTitle className="flex items-center gap-1.5 text-sm text-muted-foreground"><UserRound className="size-4" />Customer Information</CardTitle>
                    </CardHeader>
                    <CardContent className="divide-y">
                        <InfoRow label="Name">{order.userName}</InfoRow>
                        <InfoRow label="Email"><span className="break-all">{order.userEmail}</span></InfoRow>
                        <InfoRow label="User ID">{order.userId}</InfoRow>
                    </CardContent>
                </Card>

                <Card className="gap-3">
                    <CardHeader>
                        <CardTitle className="text-sm text-muted-foreground">Order Status</CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-3">
                        <OrderStatusBadge status={order.status} className="px-2.5 py-1 text-sm" />
                        <div className="flex gap-2">
                            <NativeSelect
                                value={newStatus}
                                onChange={(e) => setNewStatus(e.target.value)}
                                disabled={updateLoading}
                                aria-label="New status"
                            >
                                {ORDER_STATUSES.map(s => <option key={s} value={s}>{s}</option>)}
                            </NativeSelect>
                            <Button
                                onClick={() => handleStatusChange()}
                                disabled={updateLoading || newStatus === order.status}
                            >
                                {updateLoading ? <><Spinner />Updating...</> : 'Update'}
                            </Button>
                        </div>
                    </CardContent>
                </Card>
            </div>

            {/* Billing Address */}
            {address && (
                <Card className="mt-6 gap-3">
                    <CardHeader>
                        <CardTitle className="flex items-center gap-2"><MapPin className="size-4 text-primary" />Billing Address</CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-0.5 text-sm">
                        {address.fullName && <p className="font-medium">{address.fullName}</p>}
                        <p>{address.street}</p>
                        <p>{address.city}{address.state ? `, ${address.state}` : ''} {address.zip ?? address.zipCode}</p>
                        {address.country && <p>{address.country}</p>}
                        {address.phone && <p className="mt-2 text-muted-foreground">{address.phone}</p>}
                    </CardContent>
                </Card>
            )}

            {/* Order Items */}
            <Card className="mt-6 gap-3">
                <CardHeader>
                    <CardTitle>Order Items</CardTitle>
                </CardHeader>
                <CardContent>
                    <Table containerClassName="rounded-lg border">
                        <TableHeader>
                            <TableRow className="hover:bg-transparent">
                                <TableHead>Movie</TableHead>
                                <TableHead>Quantity</TableHead>
                                <TableHead>Price</TableHead>
                                <TableHead className="text-right">Total</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {order.movies.map((movie) => {
                                const quantity = movie.quantity ?? 1;
                                return (
                                    <TableRow key={movie.movieId}>
                                        <TableCell>
                                            <div className="font-medium">{movie.title}</div>
                                            <div className="text-xs text-muted-foreground">ID: {movie.movieId}</div>
                                        </TableCell>
                                        <TableCell className="text-muted-foreground">{quantity}</TableCell>
                                        <TableCell className="text-muted-foreground">{formatPrice(movie.priceAtOrder)}</TableCell>
                                        <TableCell className="text-right">{formatPrice(movie.priceAtOrder * quantity)}</TableCell>
                                    </TableRow>
                                );
                            })}
                        </TableBody>
                        <TableFooter>
                            <TableRow className="hover:bg-transparent">
                                <TableCell colSpan={3} className="text-right">Total</TableCell>
                                <TableCell className="text-right font-bold">{formatPrice(order.totalPrice)}</TableCell>
                            </TableRow>
                        </TableFooter>
                    </Table>
                </CardContent>
            </Card>

            {/* Action Buttons */}
            <div className="mt-6 flex justify-end gap-3">
                <Button variant="outline" onClick={() => navigate('/admin/orders')}>
                    Back to Orders
                </Button>
                {order.status !== 'Cancelled' && (
                    <Button
                        variant="destructive"
                        onClick={() => {
                            if (window.confirm('Are you sure you want to cancel this order?')) {
                                handleStatusChange('Cancelled');
                            }
                        }}
                        disabled={updateLoading}
                    >
                        <Ban /> Cancel Order
                    </Button>
                )}
            </div>
        </PageContainer>
    )
};

export default AdminOrderDetails;
