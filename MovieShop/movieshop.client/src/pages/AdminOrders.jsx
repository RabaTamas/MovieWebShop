import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { AlertCircle, ChevronRight, Package } from 'lucide-react';
import { useAuth } from '../contexts/AuthContext';
import { orderService } from '../services/orderService';
import OrderStatusBadge from '../components/OrderStatusBadge';
import { formatPrice } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { NativeSelect } from '@/components/ui/native-select';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { LoadingState } from '@/components/ui/spinner';
import { PageContainer, PageHeader } from '@/components/ui/page';

export const ORDER_STATUSES = ['Pending', 'Completed', 'Failed', 'Cancelled', 'Refunded'];

const AdminOrders = () => {
    const [orders, setOrders] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [statusFilter, setStatusFilter] = useState('');
    const { token } = useAuth();
    const navigate = useNavigate();

    useEffect(() => {
        const fetchOrders = async () => {
            try {
                setLoading(true);
                let fetchedOrders;

                if (statusFilter) {
                    fetchedOrders = await orderService.getOrdersByStatus(statusFilter, token);
                } else {
                    fetchedOrders = await orderService.getAllOrders(token);
                }

                setOrders(fetchedOrders);
                setError(null);
            } catch (err) {
                setError('Failed to load orders. Please try again.');
                console.error(err);
            } finally {
                setLoading(false);
            }
        };

        fetchOrders();
    }, [token, statusFilter]);

    const handleStatusChange = (e) => {
        setStatusFilter(e.target.value);
    };

    const formatDate = (dateString) => {
        const date = new Date(dateString);
        return date.toLocaleDateString();
    };

    return (
        <PageContainer size="xl">
            <PageHeader title="Manage Orders" icon={Package}>
                <div className="flex items-center gap-2">
                    <Label htmlFor="statusFilter" className="whitespace-nowrap text-muted-foreground">
                        Filter by Status:
                    </Label>
                    <NativeSelect
                        id="statusFilter"
                        value={statusFilter}
                        onChange={handleStatusChange}
                        containerClassName="w-44"
                    >
                        <option value="">All Orders</option>
                        {ORDER_STATUSES.map(s => <option key={s} value={s}>{s}</option>)}
                    </NativeSelect>
                </div>
            </PageHeader>

            {loading ? (
                <LoadingState label="Loading orders..." className="min-h-[30vh]" />
            ) : error ? (
                <Alert variant="destructive">
                    <AlertCircle />
                    <AlertDescription>{error}</AlertDescription>
                </Alert>
            ) : (
                <Card className="py-0">
                    <Table>
                        <TableHeader>
                            <TableRow className="hover:bg-transparent">
                                <TableHead>Order ID</TableHead>
                                <TableHead>Date</TableHead>
                                <TableHead>Customer</TableHead>
                                <TableHead>Total</TableHead>
                                <TableHead>Status</TableHead>
                                <TableHead className="text-right">Actions</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody>
                            {orders.length > 0 ? (
                                orders.map((order) => (
                                    <TableRow
                                        key={order.id}
                                        className="cursor-pointer"
                                        onClick={() => navigate(`/admin/orders/${order.id}`)}
                                    >
                                        <TableCell className="font-medium">#{order.id}</TableCell>
                                        <TableCell className="text-muted-foreground">{formatDate(order.orderDate)}</TableCell>
                                        <TableCell>
                                            <div className="font-medium">{order.userName}</div>
                                            <div className="text-xs text-muted-foreground">{order.userEmail}</div>
                                        </TableCell>
                                        <TableCell className="font-semibold whitespace-nowrap">{formatPrice(order.totalPrice)}</TableCell>
                                        <TableCell>
                                            <OrderStatusBadge status={order.status} />
                                        </TableCell>
                                        <TableCell className="text-right">
                                            <Button
                                                variant="ghost"
                                                size="sm"
                                                onClick={(e) => { e.stopPropagation(); navigate(`/admin/orders/${order.id}`); }}
                                            >
                                                View Details <ChevronRight />
                                            </Button>
                                        </TableCell>
                                    </TableRow>
                                ))
                            ) : (
                                <TableRow>
                                    <TableCell colSpan={6} className="py-10 text-center text-muted-foreground">
                                        No orders found.
                                    </TableCell>
                                </TableRow>
                            )}
                        </TableBody>
                    </Table>
                </Card>
            )}
        </PageContainer>
    );
};

export default AdminOrders;
