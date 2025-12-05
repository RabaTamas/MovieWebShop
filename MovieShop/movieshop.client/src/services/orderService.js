import API_BASE_URL from '../config/api';

export const orderService = {
    // Admin functions
    getAllOrders: async (token) => {
        const response = await fetch('${API_BASE_URL}/api/admin/orders', {
            headers: {
                'Authorization': `Bearer ${token}`
            }
        });

        if (!response.ok) {
            throw new Error('Failed to fetch orders');
        }

        return await response.json();
    },

    getOrderById: async (id, token) => {
        const response = await fetch(`${API_BASE_URL}/api/admin/orders/${id}`, {
            headers: {
                'Authorization': `Bearer ${token}`
            }
        });

        if (!response.ok) {
            throw new Error('Failed to fetch order');
        }

        return await response.json();
    },

    getOrdersByStatus: async (status, token) => {
        const response = await fetch(`${API_BASE_URL}/api/admin/orders/status/${status}`, {
            headers: {
                'Authorization': `Bearer ${token}`
            }
        });

        if (!response.ok) {
            throw new Error('Failed to fetch orders by status');
        }

        return await response.json();
    },

    updateOrderStatus: async (id, status, token) => {
        const response = await fetch(`${API_BASE_URL}/api/admin/orders/${id}/status`, {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}`
            },
            body: JSON.stringify({ status })
        });

        if (!response.ok) {
            throw new Error('Failed to update order status');
        }

        return true;
    },

    getOrderStatistics: async (token) => {
        const response = await fetch('${API_BASE_URL}/api/admin/orders/statistics', {
            headers: {
                'Authorization': `Bearer ${token}`
            }
        });

        if (!response.ok) {
            throw new Error('Failed to fetch order statistics');
        }

        return await response.json();
    }
};