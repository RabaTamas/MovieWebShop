
import { useEffect, useState } from "react";
import { useAuth } from "../contexts/AuthContext";
import API_BASE_URL from '../config/api';

const Orders = () => {
    const { token } = useAuth();
    const [orders, setOrders] = useState([]);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        const fetchOrders = async () => {
            try {
                const response = await fetch("${API_BASE_URL}/api/Order/user", {
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

    if (loading) return <p>Loading orders...</p>;
    if (orders.length === 0) return <p>No orders yet</p>;

    return (
        <div className="orders-page">
            <h2>Your Orders</h2>
            {orders.map((order) => (
                <div key={order.id} className="order-card" style={{ border: "1px solid #ccc", padding: "1rem", marginBottom: "2rem" }}>
                    <h3>Order #{order.id} - {new Date(order.orderDate).toLocaleString()}</h3>
                    <p><strong>Total:</strong> {order.totalPrice} Ft</p>
                    <p><strong>Status:</strong> {order.status}</p>
                    <div style={{ display: "flex", gap: "2rem" }}>
                        <div>
                            <h4>Billing Address</h4>
                            <p>{order.billingAddress.street}, {order.billingAddress.city} {order.billingAddress.zip}</p>
                        </div>
                        <div>
                            <h4>Shipping Address</h4>
                            <p>{order.shippingAddress.street}, {order.shippingAddress.city} {order.shippingAddress.zip}</p>
                        </div>
                    </div>

                    <table style={{ width: "100%", marginTop: "1rem", borderCollapse: "collapse" }}>
                        <thead>
                            <tr>
                                <th style={{ borderBottom: "1px solid #ccc" }}>Title</th>
                                <th style={{ borderBottom: "1px solid #ccc" }}>Quantity</th>
                                <th style={{ borderBottom: "1px solid #ccc" }}>Price (Ft)</th>
                            </tr>
                        </thead>
                        <tbody>
                            {order.movies.map((movie) => (
                                <tr key={movie.movieId}>
                                    <td>{movie.title}</td>
                                    <td>{movie.quantity}</td>
                                    <td>{movie.priceAtOrder}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            ))}
        </div>
    );
};

export default Orders;
