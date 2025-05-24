import React, { useState, useEffect } from 'react';
import { useParams, Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { orderService } from '../services/orderService';

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

    const handleStatusChange = async () => {
        try {
            setUpdateLoading(true);
            await orderService.updateOrderStatus(id, newStatus, token);
            // Update the order object
            setOrder({ ...order, status: newStatus });
            alert('Order status updated successfully!');
        } catch (err) {
            alert('Failed to update order status. Please try again.');
            console.error(err);
        } finally {
            setUpdateLoading(false);
        }
    };

    const formatDate = (dateString) => {
        const date = new Date(dateString);
        return date.toLocaleDateString() + ' ' + date.toLocaleTimeString();
    };

    if (loading) return <div className="p-4">Loading order details...</div>;
    if (error) return <div className="p-4 text-red-500">{error}</div>;
    if (!order) return <div className="p-4 text-red-500">Order not found</div>;

    return (
        <div className="p-4">
            <div className="flex justify-between items-center mb-6">
                <h1 className="text-2xl font-bold">Order #{order.id} Details</h1>
                <Link to="/admin/orders" className="bg-gray-500 hover:bg-gray-600 text-white py-2 px-4 rounded">
                    Back to Orders
                </Link>
            </div>

            {/* Order Summary Card */}
            <div className="bg-white p-6 rounded-lg shadow mb-6">
                <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
                    <div>
                        <h2 className="text-gray-500 font-medium text-sm mb-2">Order Information</h2>
                        <div className="text-sm">
                            <p><span className="font-medium">Order ID:</span> #{order.id}</p>
                            <p><span className="font-medium">Date:</span> {formatDate(order.orderDate)}</p>
                            <p><span className="font-medium">Total:</span> ${order.totalPrice}</p>
                        </div>
                    </div>

                    <div>
                        <h2 className="text-gray-500 font-medium text-sm mb-2">Customer Information</h2>
                        <div className="text-sm">
                            <p><span className="font-medium">Name:</span> {order.userName}</p>
                            <p><span className="font-medium">Email:</span> {order.userEmail}</p>
                            <p><span className="font-medium">User ID:</span> {order.userId}</p>
                        </div>
                    </div>

                    <div>
                        <h2 className="text-gray-500 font-medium text-sm mb-2">Order Status</h2>
                        <div className="flex items-center space-x-4">
                            <span className={`px-2 py-1 inline-flex text-xs leading-5 font-semibold rounded-full 
                                ${order.status === 'Pending' ? 'bg-yellow-100 text-yellow-800' :
                                    order.status === 'Processing' ? 'bg-blue-100 text-blue-800' :
                                        order.status === 'Shipped' ? 'bg-indigo-100 text-indigo-800' :
                                            order.status === 'Delivered' ? 'bg-green-100 text-green-800' :
                                                order.status === 'Cancelled' ? 'bg-red-100 text-red-800' :
                                                    'bg-gray-100 text-gray-800'}`}>
                                {order.status}
                            </span>
                        </div>

                        <div className="mt-4">
                            <div className="flex items-center space-x-2">
                                <select
                                    value={newStatus}
                                    onChange={(e) => setNewStatus(e.target.value)}
                                    className="block w-full rounded-md border-gray-300 shadow-sm focus:border-indigo-300 focus:ring focus:ring-indigo-200 focus:ring-opacity-50"
                                    disabled={updateLoading}
                                >
                                    <option value="Pending">Pending</option>
                                    <option value="Processing">Processing</option>
                                    <option value="Shipped">Shipped</option>
                                    <option value="Delivered">Delivered</option>
                                    <option value="Cancelled">Cancelled</option>
                                    <option value="Refunded">Refunded</option>
                                </select>
                                <button
                                    onClick={handleStatusChange}
                                    className="bg-blue-500 hover:bg-blue-600 text-white py-2 px-4 rounded"
                                    disabled={updateLoading || newStatus === order.status}
                                >
                                    {updateLoading ? 'Updating...' : 'Update'}
                                </button>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            {/* Addresses */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mb-6">
                {/* Shipping Address */}
                <div className="bg-white p-6 rounded-lg shadow">
                    <h2 className="text-lg font-semibold mb-4">Shipping Address</h2>
                    <div className="text-sm">
                        <p>{order.shippingAddress.fullName}</p>
                        <p>{order.shippingAddress.street}</p>
                        <p>{order.shippingAddress.city}, {order.shippingAddress.state} {order.shippingAddress.zipCode}</p>
                        <p>{order.shippingAddress.country}</p>
                        <p className="mt-2">{order.shippingAddress.phone}</p>
                    </div>
                </div>

                {/* Billing Address */}
                <div className="bg-white p-6 rounded-lg shadow">
                    <h2 className="text-lg font-semibold mb-4">Billing Address</h2>
                    <div className="text-sm">
                        <p>{order.billingAddress.fullName}</p>
                        <p>{order.billingAddress.street}</p>
                        <p>{order.billingAddress.city}, {order.billingAddress.state} {order.billingAddress.zipCode}</p>
                        <p>{order.billingAddress.country}</p>
                        <p className="mt-2">{order.billingAddress.phone}</p>
                    </div>
                </div>
            </div>

            {/* Order Items */}
            <div className="bg-white p-6 rounded-lg shadow mb-6">
                <h2 className="text-lg font-semibold mb-4">Order Items</h2>
                <div className="overflow-x-auto">
                    <table className="min-w-full divide-y divide-gray-200">
                        <thead>
                            <tr>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                                    Movie
                                </th>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                                    Quantity
                                </th>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                                    Price
                                </th>
                                <th className="px-6 py-3 text-left text-xs font-medium text-gray-500 uppercase tracking-wider">
                                    Total
                                </th>
                            </tr>
                        </thead>
                        <tbody className="bg-white divide-y divide-gray-200">
                            {order.movies.map((movie) => (
                                <tr key={movie.movieId}>
                                    <td className="px-6 py-4 whitespace-nowrap">
                                        <div className="text-sm font-medium text-gray-900">{movie.title}</div>
                                        <div className="text-sm text-gray-500">ID: {movie.movieId}</div>
                                    </td>
                                    <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                                        {movie.quantity}
                                    </td>
                                    <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                                        ${movie.priceAtOrder}
                                    </td>
                                    <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">
                                        ${movie.priceAtOrder * movie.quantity}
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                        <tfoot>
                            <tr className="bg-gray-50">
                                <td colSpan="3" className="px-6 py-4 text-right text-sm font-medium">
                                    Total
                                </td>
                                <td className="px-6 py-4 whitespace-nowrap text-sm font-medium">
                                    ${order.totalPrice}
                                </td>
                            </tr>
                        </tfoot>
                    </table>
                </div>
            </div>

            {/* Action Buttons */}
            <div className="flex justify-end space-x-4">
                <button
                    onClick={() => navigate('/admin/orders')}
                    className="bg-gray-500 hover:bg-gray-600 text-white py-2 px-4 rounded"
                >
                    Back to Orders
                </button>
                {order.status !== 'Cancelled' && (
                    <button
                        onClick={() => {
                            if (window.confirm('Are you sure you want to cancel this order?')) {
                                setNewStatus('Cancelled');
                                handleStatusChange();
                            }
                        }}
                        className="bg-red-500 hover:bg-red-600 text-white py-2 px-4 rounded"
                        disabled={updateLoading}
                    >
                        Cancel Order
                    </button>
                )}
            </div>
        </div>
    )
};

export default AdminOrderDetails;