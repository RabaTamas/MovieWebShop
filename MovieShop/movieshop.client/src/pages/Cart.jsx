import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";
import AddressForm from "../components/AddressForm";
import { Elements } from '@stripe/react-stripe-js';
import { loadStripe } from '@stripe/stripe-js';
import StripeCheckout from '../components/StripeCheckout';
import API_BASE_URL from "../config/api";
import { toast } from "sonner";
import { CreditCard, Film, Lock, ShoppingCart, Trash2 } from "lucide-react";
import { formatPrice } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import { EmptyState, PageContainer, PageHeader } from "@/components/ui/page";

const Cart = () => {
    const { token } = useAuth();
    const [items, setItems] = useState([]);
    const [billingAddress, setBillingAddress] = useState(null);
    const [showPayment, setShowPayment] = useState(false);
    const [clientSecret, setClientSecret] = useState('');
    const [stripePromise, setStripePromise] = useState(null);

    const fetchCart = () => {
        fetch(`${API_BASE_URL}/api/ShoppingCart`, {
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
        fetch(`${API_BASE_URL}/api/Address`, {
            headers: { Authorization: `Bearer ${token}` }
        })
            .then(res => res.json())
            .then(data => {
                setBillingAddress(data[0] || null);
            })
            .catch(err => {
                console.error("Error loading addresses:", err);
            });
    };

    useEffect(() => {
        fetch(`${API_BASE_URL}/api/Payment/config`, {
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
    }, [token]);

    const saveBillingAddress = (address) => {
        const method = address.id ? 'PUT' : 'POST';
        const url = address.id
            ? `${API_BASE_URL}/api/Address/${address.id}`
            : `${API_BASE_URL}/api/Address`;

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
            .catch(err => toast.error(err.message));
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
                toast.error('Error removing item');
            });
    };

    const total = items.reduce((sum, item) => sum + item.priceAtOrder, 0);

    const initiatePayment = async () => {
        if (!billingAddress) {
            toast.error("Missing billing address.");
            return;
        }

        try {
            // Stripe minimum amount check (minimum 100 HUF)
            const testAmount = Math.max(total, 100);

            const response = await fetch(`${API_BASE_URL}/api/Payment/create-payment-intent`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    Authorization: `Bearer ${token}`
                },
                body: JSON.stringify({ amount: testAmount })
            });

            if (!response.ok) {
                const errorData = await response.json();
                console.error("Server error:", errorData);
                toast.error(`Server error: ${errorData.error || 'Unknown error'}`);
                return;
            }

            const data = await response.json();
            setClientSecret(data.clientSecret);
            setShowPayment(true);
        } catch (err) {
            console.error("Exception:", err);
            toast.error("Payment initiation error: " + err.message);
        }
    };

    const handlePaymentSuccess = async (paymentMethodId) => {
        try {
            const stripe = await stripePromise;
            const { error, paymentIntent } = await stripe.confirmCardPayment(clientSecret, {
                payment_method: paymentMethodId
            });

            if (error) {
                toast.error("Payment error: " + error.message);
                return;
            }

            if (paymentIntent.status === 'succeeded') {
                const orderData = {
                    billingAddress: {
                        city: billingAddress.city,
                        street: billingAddress.street,
                        zip: billingAddress.zip,
                    },
                    paymentIntentId: paymentIntent.id,
                    movies: items.map(item => ({
                        movieId: item.movieId,
                        title: item.title,
                        priceAtOrder: item.priceAtOrder
                    }))
                };

                const orderResponse = await fetch(`${API_BASE_URL}/api/Order`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        Authorization: `Bearer ${token}`
                    },
                    body: JSON.stringify(orderData)
                });

                if (!orderResponse.ok) throw new Error("Order failed.");

                const order = await orderResponse.json();
                toast.success("Sikeres rendelés! Rendelés azonosító: " + order.id, {
                    action: { label: "My Movies", onClick: () => window.location.assign("/my-movies") },
                });
                setShowPayment(false);
                fetchCart();
            }
        } catch (err) {
            toast.error("Error occurred: " + err.message);
        }
    };

    // Only one early return
    if (!items || items.length === 0) {
        return (
            <PageContainer size="md">
                <EmptyState
                    icon={ShoppingCart}
                    title="Your cart is empty."
                    description="Find something great to watch and add it to your cart."
                >
                    <Button asChild>
                        <Link to="/">Browse movies</Link>
                    </Button>
                </EmptyState>
            </PageContainer>
        );
    }

    return (
        <PageContainer>
            <PageHeader title="Your Cart" icon={ShoppingCart} description={`${items.length} item${items.length !== 1 ? "s" : ""} in your cart`} />

            <div className="grid items-start gap-6 lg:grid-cols-[1fr_380px]">
                <div className="space-y-6">
                    <Card className="gap-0 py-0">
                        <ul className="divide-y">
                            {items.map(item => (
                                <li key={item.movieId} className="flex items-center gap-4 p-4">
                                    <div className="flex size-11 shrink-0 items-center justify-center rounded-lg bg-muted">
                                        <Film className="size-5 text-muted-foreground" />
                                    </div>
                                    <div className="min-w-0 flex-1">
                                        <Link to={`/movies/${item.movieId}`} className="line-clamp-1 font-medium hover:text-primary">
                                            {item.title}
                                        </Link>
                                        <p className="text-sm text-muted-foreground">Digital copy · streaming included</p>
                                    </div>
                                    <span className="font-semibold whitespace-nowrap">{formatPrice(item.priceAtOrder)}</span>
                                    <Button
                                        variant="ghost"
                                        size="icon-sm"
                                        className="text-muted-foreground hover:text-destructive"
                                        aria-label="Remove"
                                        title="Remove"
                                        onClick={() => removeItem(item.movieId)}
                                    >
                                        <Trash2 />
                                    </Button>
                                </li>
                            ))}
                        </ul>
                    </Card>

                    {showPayment && (
                        <Card>
                            <CardHeader>
                                <CardTitle className="flex items-center gap-2">
                                    <CreditCard className="size-5 text-primary" /> Payment
                                </CardTitle>
                                <CardDescription className="flex items-center gap-1.5">
                                    <Lock className="size-3.5" /> Secure payment powered by Stripe
                                </CardDescription>
                            </CardHeader>
                            <CardContent className="space-y-3">
                                {stripePromise && clientSecret && (
                                    <Elements stripe={stripePromise} options={{ clientSecret }}>
                                        <StripeCheckout
                                            amount={total}
                                            onSuccess={handlePaymentSuccess}
                                            onError={(err) => toast.error("Payment error: " + err)}
                                        />
                                    </Elements>
                                )}
                                <Button variant="outline" className="w-full" onClick={() => setShowPayment(false)}>
                                    Cancel Payment
                                </Button>
                            </CardContent>
                        </Card>
                    )}
                </div>

                <div className="space-y-6 lg:sticky lg:top-24">
                    <Card>
                        <CardHeader>
                            <CardTitle>Order summary</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-3">
                            <div className="flex justify-between text-sm text-muted-foreground">
                                <span>Items ({items.length})</span>
                                <span>{formatPrice(total)}</span>
                            </div>
                            <Separator />
                            <div className="flex items-baseline justify-between">
                                <span className="font-medium">Total</span>
                                <span className="text-2xl font-bold">{formatPrice(total)}</span>
                            </div>
                            {!showPayment && (
                                <>
                                    <Button
                                        size="lg"
                                        className="mt-2 w-full"
                                        disabled={!billingAddress || items.length === 0}
                                        onClick={initiatePayment}
                                    >
                                        <Lock />
                                        Proceed to Payment
                                    </Button>
                                    {!billingAddress && (
                                        <p className="text-center text-xs text-muted-foreground">
                                            Save a billing address to continue.
                                        </p>
                                    )}
                                </>
                            )}
                        </CardContent>
                    </Card>

                    <AddressForm
                        title="Billing Address"
                        initialAddress={billingAddress}
                        onSave={saveBillingAddress}
                    />
                </div>
            </div>
        </PageContainer>
    );
};

export default Cart;
