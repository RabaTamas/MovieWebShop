import { useEffect, useState } from "react";
import { useAuth } from "../contexts/AuthContext";
import AddressForm from "../components/AddressForm";

const Cart = () => {
    const { token } = useAuth();
    const [items, setItems] = useState([]);
    const [shippingAddress, setShippingAddress] = useState(null);
    const [billingAddress, setBillingAddress] = useState(null);

    const fetchCart = () => {
        fetch('https://localhost:7289/api/ShoppingCart', {
            headers: { Authorization: `Bearer ${token}` }
})
            .then(res => res.json())
    .then(data => setItems(data.items || []))
    .catch(err => {
        console.error("Error loading cart:", err);
        setItems([]);
    });
    };

const fetchAddresses = () => {
    fetch('https://localhost:7289/api/Address', {
        headers: { Authorization: `Bearer ${token}` }
        })
            .then(res => res.json())
    .then(data => {
        setShippingAddress(data[0] || null);
        setBillingAddress(data[1] || null);
    })
    .catch(err => {
        console.error("Error loading addresses:", err);
    });
    };

useEffect(() => {
    fetchCart();
    fetchAddresses();
}, []);

const saveShippingAddress = (address) => {
    const method = address.id ? 'PUT' : 'POST';
    const url = address.id
        ? `https://localhost:7289/api/Address/${address.id}`
        : 'https://localhost:7289/api/Address';


fetch(url, {
    method,
    headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${token}`
            },
    body: JSON.stringify(address)
        })
    .then(res => {
        if (!res.ok) throw new Error("Failed to save shipping address");
        return res.json();
    })
    .then(saved => setShippingAddress(saved))
    .catch(err => alert(err.message));
    };

const saveBillingAddress = (address) => {
    const method = address.id ? 'PUT' : 'POST';
    const url = address.id
        ? `https://localhost:7289/api/Address/${address.id}`
        : 'https://localhost:7289/api/Address';

fetch(url, {
    method,
    headers: {
        'Content-Type': 'application/json',
        Authorization: `Bearer ${token}`
            },
    body: JSON.stringify(address)
        })
    .then(res => {
        if (!res.ok) throw new Error("Failed to save billing address");
        return res.json();
    })
    .then(saved => setBillingAddress(saved))
    .catch(err => alert(err.message));
    };

    if (!items || items.length === 0) {
        return (
            <div className="container mt-4">
                <h2>Your cart is empty.</h2>
            </div>
        );
    }

    const updateQuantity = (movieId, newQuantity) => {
        if (newQuantity < 1) return;

        fetch('https://localhost:7289/api/ShoppingCart/update', {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json',
                Authorization: `Bearer ${token}`
            },
            body: JSON.stringify({ movieId, quantity: newQuantity })
        })
            .then(res => {
                if (!res.ok) throw new Error('Failed to update quantity');
                fetchCart();
            })
            .catch(err => {
                console.error(err);
                alert('Error updating quantity');
            });
    };

    const removeItem = (movieId) => {
        fetch(`https://localhost:7289/api/ShoppingCart/remove/${movieId}`, {
            method: 'DELETE',
            headers: {
                Authorization: `Bearer ${token}`
            }
        })
            .then(res => {
                if (!res.ok) throw new Error('Failed to remove item');
                fetchCart();
            })
            .catch(err => {
                console.error(err);
                alert('Error removing item');
            });
    };

    const total = items.reduce((sum, item) => sum + item.priceAtOrder * item.quantity, 0);

    const placeOrder = () => {
        if (!billingAddress || !shippingAddress) {
            alert("Hiányzó számlázási vagy szállítási cím.");
            return;
        }

        console.log("POST /api/Order payload", {
            billingAddress,
            shippingAddress
        });

        const orderData = {
            billingAddress: {
                city: billingAddress.city,
                street: billingAddress.street,
                zip: billingAddress.zip,
            },
            shippingAddress: {
                city: shippingAddress.city,
                street: shippingAddress.street,
                zip: shippingAddress.zip,
            },
            movies: items.map(item => ({
                movieId: item.movieId,
                title: item.title,
                quantity: item.quantity,
                priceAtOrder: item.priceAtOrder
            }))
        };


        fetch('https://localhost:7289/api/Order', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                Authorization: `Bearer ${token}`
            },
            body: JSON.stringify(
                orderData
            )
        })
            .then(res => {
                if (!res.ok) throw new Error("Order failed.");
                return res.json();
            })
            .then(order => {
                alert("Sikeres rendelés! Rendelés azonosító: " + order.id);
                fetchCart();
            })
            .catch(err => alert("Hiba történt: " + err.message));
    };


return (
    <div className="container mt-4">
        <div className="row">
            <div className="col-md-8">
                <h2>Your Cart</h2>
                {items.length === 0 ? (
                    <p>Your cart is empty.</p>
                ) : (
                    <>
                        <table className="table table-striped">
                            <thead>
                                <tr>
                                    <th>Title</th>
                                    <th>Quantity</th>
                                    <th>Price (Ft)</th>
                                    <th>Subtotal (Ft)</th>
                                    <th>Actions</th>
                                </tr>
                            </thead>
                            <tbody>
                                {items.map(item => (
                                    <tr key={item.movieId}>
                                        <td>{item.title}</td>
                                        <td>
                                            <button className="btn btn-sm btn-secondary me-1"
                                                onClick={() => updateQuantity(item.movieId, item.quantity - 1)}
                                                disabled={item.quantity <= 1}
                                            >-</button>
                                            {item.quantity}
                                            <button className="btn btn-sm btn-secondary ms-1"
                                                onClick={() => updateQuantity(item.movieId, item.quantity + 1)}
                                            >+</button>
                                        </td>
                                        <td>{item.priceAtOrder}</td>
                                        <td>{item.priceAtOrder * item.quantity}</td>
                                        <td>
                                            <button className="btn btn-sm btn-danger"
                                                onClick={() => removeItem(item.movieId)}
                                            >Remove</button>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                            <h4>Total: {total} Ft</h4>

                            <button
                                className="btn btn-primary mt-3"
                                disabled={!shippingAddress || !billingAddress || items.length === 0}
                                onClick={() => placeOrder()}
                            >
                                Order
                            </button>
                    </>
                )}
            </div>

            <div className="col-md-4">
                <AddressForm
                    title="Shipping Address"
                    initialAddress={shippingAddress}
                    onSave={saveShippingAddress}
                />
                <AddressForm
                    title="Billing Address"
                    initialAddress={billingAddress}
                    onSave={saveBillingAddress}
                />
            </div>
        </div>
    </div>
);
};

export default Cart;