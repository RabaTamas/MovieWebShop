import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { CalendarDays, MapPin, ReceiptText } from "lucide-react";
import { useAuth } from "../contexts/AuthContext";
import API_BASE_URL from "../config/api";
import { formatPrice } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription, CardAction } from "@/components/ui/card";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { LoadingState } from "@/components/ui/spinner";
import { EmptyState, PageContainer, PageHeader } from "@/components/ui/page";
import OrderStatusBadge from "../components/OrderStatusBadge";

const Orders = () => {
    const { token } = useAuth();
    const [orders, setOrders] = useState([]);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        const fetchOrders = async () => {
            try {
                const response = await fetch(`${API_BASE_URL}/api/Order/user`, {
                    headers: {
                        Authorization: `Bearer ${token}`,
                    },
                });

                if (!response.ok) {
                    throw new Error("Failed to fetch orders");
                }

                const data = await response.json();
                setOrders(data);
            } catch (error) {
                console.error("Error fetching orders:", error);
            } finally {
                setLoading(false);
            }
        };

        fetchOrders();
    }, [token]);

    if (loading) return <LoadingState label="Loading orders..." />;

    if (orders.length === 0) {
        return (
            <PageContainer size="md">
                <EmptyState icon={ReceiptText} title="No orders yet" description="Your purchases will show up here.">
                    <Button asChild><Link to="/">Browse movies</Link></Button>
                </EmptyState>
            </PageContainer>
        );
    }

    return (
        <PageContainer size="lg" className="max-w-4xl">
            <PageHeader title="Your Orders" icon={ReceiptText} description={`${orders.length} order${orders.length !== 1 ? "s" : ""}`} />
            <div className="space-y-5">
                {orders.map((order) => (
                    <Card key={order.id} className="gap-4">
                        <CardHeader>
                            <CardTitle className="text-lg">Order #{order.id}</CardTitle>
                            <CardDescription className="flex flex-wrap items-center gap-x-4 gap-y-1">
                                <span className="flex items-center gap-1.5">
                                    <CalendarDays className="size-3.5" />
                                    {new Date(order.orderDate).toLocaleString()}
                                </span>
                                {order.billingAddress && (
                                    <span className="flex items-center gap-1.5">
                                        <MapPin className="size-3.5" />
                                        {order.billingAddress.street}, {order.billingAddress.city} {order.billingAddress.zip}
                                    </span>
                                )}
                            </CardDescription>
                            <CardAction>
                                <OrderStatusBadge status={order.status} />
                            </CardAction>
                        </CardHeader>
                        <CardContent>
                            <Table containerClassName="rounded-lg border">
                                <TableHeader>
                                    <TableRow className="hover:bg-transparent">
                                        <TableHead>Title</TableHead>
                                        <TableHead className="text-right">Price</TableHead>
                                    </TableRow>
                                </TableHeader>
                                <TableBody>
                                    {order.movies.map((movie) => (
                                        <TableRow key={movie.movieId}>
                                            <TableCell className="font-medium">{movie.title}</TableCell>
                                            <TableCell className="text-right">{formatPrice(movie.priceAtOrder)}</TableCell>
                                        </TableRow>
                                    ))}
                                </TableBody>
                            </Table>
                            <div className="mt-4 flex items-baseline justify-end gap-3">
                                <span className="text-sm text-muted-foreground">Total</span>
                                <span className="text-xl font-bold">{formatPrice(order.totalPrice)}</span>
                            </div>
                        </CardContent>
                    </Card>
                ))}
            </div>
        </PageContainer>
    );
};

export default Orders;
