import { useEffect, useState } from "react";
import { useAuth } from "../contexts/AuthContext";
import AddressForm from "../components/AddressForm";
import { Elements } from '@stripe/react-stripe-js';
import { loadStripe } from '@stripe/stripe-js';
import StripeCheckout from '../components/StripeCheckout';
import API_BASE_URL from '../config/api';

const Cart = () => {
    const { token } = useAuth();
    const [items, setItems] = useState([]);
    const [shippingAddress, setShippingAddress] = useState(null);
    const [billingAddress, setBillingAddress] = useState(null);
    const [showPayment, setShowPayment] = useState(false);
    const [clientSecret, setClientSecret] = useState('');
    const [stripePromise, setStripePromise] = useState(null);

    const fetchCart = () => {
        fetch('${API_BASE_URL}/api/ShoppingCart', {
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
        fetch('${API_BASE_URL}/api/Address', {
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
        fetch('${API_BASE_URL}/api/Payment/config', {
            headers: { Authorization: `Bearer ${token}` }
        })
            .then(res => res.json())
            .then(data => {
                setStripePromise(loadStripe(data.publishableKey));
            })
            .catch(err => console.error("Error loading Stripe config:", err));
    }, [token]);

    useEffect(() => {
        fetchCart();
        fetchAddresses();
    }, []);

    const saveShippingAddress = (address) => {
        const method = address.id ? 'PUT' : 'POST';
        const url = address.id
            ? `${API_BASE_URL}/api/Address/${address.id}`
            : '${API_BASE_URL}/api/Address';

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
            ? `${API_BASE_URL}/api/Address/${address.id}`
            : '${API_BASE_URL}/api/Address';

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

    const updateQuantity = (movieId, newQuantity) => {
        if (newQuantity < 1) return;

        fetch('${API_BASE_URL}/api/ShoppingCart/update', {
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
        fetch(`${API_BASE_URL}/api/ShoppingCart/remove/${movieId}`, {
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

    //const initiatePayment = async () => {
    //    if (!billingAddress || !shippingAddress) {
    //        alert("Hi�nyz� sz�ml�z�si vagy sz�ll�t�si c�m.");
    //        return;
    //    }

    //    try {
    //        const response = await fetch('${API_BASE_URL}/api/Payment/create-payment-intent', {
    //            method: 'POST',
    //            headers: {
    //                'Content-Type': 'application/json',
    //                Authorization: `Bearer ${token}`
    //            },
    //            body: JSON.stringify({ amount: total })
    //        });

    //        const data = await response.json();
    //        setClientSecret(data.clientSecret);
    //        setShowPayment(true);
    //    } catch (err) {
    //        alert("Hiba a fizet�s ind�t�sakor: " + err.message);
    //    }
    //};

    const initiatePayment = async () => {
        if (!billingAddress || !shippingAddress) {
            alert("Hi�nyz� sz�ml�z�si vagy sz�ll�t�si c�m.");
            return;
        }

        try {
            console.log("?? Total amount:", total);

            // Teszt: minimum �sszeg ellen�rz�se
            const testAmount = Math.max(total, 100); // Minimum 100 HUF
            console.log("?? Sending amount:", testAmount);

            const response = await fetch('${API_BASE_URL}/api/Payment/create-payment-intent', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    Authorization: `Bearer ${token}`
                },
                body: JSON.stringify({ amount: testAmount })
            });

            if (!response.ok) {
                const errorData = await response.json();
                console.error("? Server error:", errorData);
                alert(`Server error: ${errorData.error || 'Unknown error'}`);
                return;
            }

            const data = await response.json();
            console.log("? Payment Intent created:", data);
            setClientSecret(data.clientSecret);
            setShowPayment(true);
        } catch (err) {
            console.error("? Exception:", err);
            alert("Hiba a fizet�s ind�t�sakor: " + err.message);
        }
    };

    const handlePaymentSuccess = async (paymentMethodId) => {
        try {
            const stripe = await stripePromise;
            const { error, paymentIntent } = await stripe.confirmCardPayment(clientSecret, {
                payment_method: paymentMethodId
            });

            if (error) {
                alert("Fizet�si hiba: " + error.message);
                return;
            }

            if (paymentIntent.status === 'succeeded') {
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
                    paymentIntentId: paymentIntent.id,
                    movies: items.map(item => ({
                        movieId: item.movieId,
                        title: item.title,
                        quantity: item.quantity,
                        priceAtOrder: item.priceAtOrder
                    }))
                };

                const orderResponse = await fetch('/api/Order', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        Authorization: `Bearer ${token}`
                    },
                    body: JSON.stringify(orderData)
                });

                if (!orderResponse.ok) throw new Error("Order failed.");

                const order = await orderResponse.json();
                alert("Sikeres rendel�s! Rendel�s azonos�t�: " + order.id);
                setShowPayment(false);
                fetchCart();
            }
        } catch (err) {
            alert("Hiba t�rt�nt: " + err.message);
        }
    };

    // CSAK EGY early return legyen!
    if (!items || items.length === 0) {
        return (
            <div className="container mt-4">
                <h2>Your cart is empty.</h2>
            </div>
        );
    }

    return (
        <div className="container mt-4">
            <div className="row">
                <div className="col-md-8">
                    <h2>Your Cart</h2>
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

                    {!showPayment ? (
                        <button
                            className="btn btn-primary mt-3"
                            disabled={!shippingAddress || !billingAddress || items.length === 0}
                            onClick={initiatePayment}
                        >
                            Proceed to Payment
                        </button>
                    ) : (
                        <div className="mt-4">
                            <h4>Payment</h4>
                            {stripePromise && clientSecret && (
                                <Elements stripe={stripePromise} options={{ clientSecret }}>
                                    <StripeCheckout
                                        amount={total}
                                        onSuccess={handlePaymentSuccess}
                                        onError={(err) => alert("Payment error: " + err)}
                                    />
                                </Elements>
                            )}
                            <button
                                className="btn btn-secondary mt-2"
                                onClick={() => setShowPayment(false)}
                            >
                                Cancel Payment
                            </button>
                        </div>
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