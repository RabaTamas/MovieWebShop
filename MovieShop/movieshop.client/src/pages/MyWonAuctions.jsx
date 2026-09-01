import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { Elements } from '@stripe/react-stripe-js';
import { loadStripe } from '@stripe/stripe-js';
import StripeCheckout from '../components/StripeCheckout';
import API_BASE_URL from '../config/api';
import { useAuth } from '../contexts/AuthContext';
import './MyWonAuctions.css';

export default function MyWonAuctions() {
    const { token } = useAuth();
    const [wonAuctions, setWonAuctions] = useState([]);
    const [loading, setLoading] = useState(true);
    // per-auction payment state: { [auctionId]: { clientSecret, stripePromise, paid } }
    const [paymentState, setPaymentState] = useState({});

    const fetchWins = () => {
        fetch(`${API_BASE_URL}/api/Auction/my-wins`, {
            headers: { Authorization: `Bearer ${token}` }
        })
            .then(r => r.ok ? r.json() : [])
            .then(data => { setWonAuctions(data); setLoading(false); })
            .catch(() => setLoading(false));
    };

    useEffect(() => { fetchWins(); }, [token]);

    const handleStartPayment = async (auctionId) => {
        try {
            const r = await fetch(`${API_BASE_URL}/api/Auction/${auctionId}/create-payment`, {
                method: 'POST',
                headers: { Authorization: `Bearer ${token}` }
            });
            if (!r.ok) { alert('Payment not available for this auction.'); return; }
            const data = await r.json();
            setPaymentState(prev => ({
                ...prev,
                [auctionId]: {
                    clientSecret: data.clientSecret,
                    stripePromise: loadStripe(data.publishableKey),
                    paid: false
                }
            }));
        } catch { alert('Network error.'); }
    };

    const handlePaymentSuccess = async (auctionId, paymentMethodId) => {
        const ps = paymentState[auctionId];
        if (!ps) return;
        const stripe = await ps.stripePromise;
        const { error, paymentIntent } = await stripe.confirmCardPayment(ps.clientSecret, {
            payment_method: paymentMethodId
        });
        if (error) { alert('Payment failed: ' + error.message); return; }
        if (paymentIntent.status === 'succeeded') {
            const r = await fetch(`${API_BASE_URL}/api/Auction/${auctionId}/confirm-payment`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
                body: JSON.stringify({ paymentIntentId: paymentIntent.id })
            });
            if (r.ok) {
                setPaymentState(prev => ({
                    ...prev,
                    [auctionId]: { ...prev[auctionId], paid: true, clientSecret: null }
                }));
                setWonAuctions(prev =>
                    prev.map(a => a.id === auctionId ? { ...a, isPaid: true } : a)
                );
            } else {
                alert('Payment confirmed by Stripe but server verification failed.');
            }
        }
    };

    if (loading) return (
        <div className="mwa-loading">
            <div className="spinner-border text-primary" role="status" />
            <p>Loading your won auctions…</p>
        </div>
    );

    if (wonAuctions.length === 0) return (
        <div className="mwa-empty">
            <div className="mwa-empty-icon">🏆</div>
            <h3>No won auctions yet</h3>
            <p className="text-muted">Win an auction and your items will appear here for payment.</p>
            <Link to="/auctions" className="btn btn-primary mt-2">Browse Auctions</Link>
        </div>
    );

    const unpaidCount = wonAuctions.filter(a => !a.isPaid && !paymentState[a.id]?.paid).length;

    return (
        <div className="mwa-page">
            <div className="mwa-header">
                <h1>🏆 My Won Auctions</h1>
                <p className="text-muted">
                    {unpaidCount > 0
                        ? `You have ${unpaidCount} unpaid auction${unpaidCount > 1 ? 's' : ''}. Pay now to receive your items.`
                        : 'All your won auctions are paid. Enjoy your movies!'}
                </p>
            </div>

            <div className="mwa-list">
                {wonAuctions.map(a => {
                    const ps = paymentState[a.id];
                    const isPaid = a.isPaid || ps?.paid;

                    return (
                        <div key={a.id} className={`mwa-card ${isPaid ? 'paid' : 'unpaid'}`}>
                            <div className="mwa-card-img">
                                <img src={a.imageUrl} alt={a.title} />
                                {isPaid
                                    ? <span className="mwa-badge paid-badge">✅ Paid</span>
                                    : <span className="mwa-badge unpaid-badge">💳 Unpaid</span>}
                            </div>

                            <div className="mwa-card-body">
                                <h4 className="mwa-title">{a.title}</h4>
                                <div className="mwa-price-row">
                                    <div>
                                        <div className="mwa-label">Final bid</div>
                                        <div className="mwa-price">{a.currentPrice.toLocaleString()} Ft</div>
                                    </div>
                                    <Link to={`/auctions/${a.id}`} className="btn btn-sm btn-outline-secondary">
                                        View auction
                                    </Link>
                                </div>

                                {isPaid ? (
                                    <div className="mwa-paid-banner">
                                        ✅ Payment complete — your item is on its way!
                                    </div>
                                ) : ps?.clientSecret ? (
                                    <div className="mwa-payment-form">
                                        <h6 className="mb-3">Complete your payment</h6>
                                        {ps.stripePromise && (
                                            <Elements stripe={ps.stripePromise} options={{ clientSecret: ps.clientSecret }}>
                                                <StripeCheckout
                                                    amount={a.currentPrice}
                                                    onSuccess={(pmId) => handlePaymentSuccess(a.id, pmId)}
                                                    onError={(msg) => alert('Payment error: ' + msg)}
                                                />
                                            </Elements>
                                        )}
                                        <button
                                            className="btn btn-sm btn-outline-secondary mt-2"
                                            onClick={() => setPaymentState(prev => {
                                                const next = { ...prev };
                                                delete next[a.id];
                                                return next;
                                            })}
                                        >
                                            Cancel
                                        </button>
                                    </div>
                                ) : (
                                    <button
                                        className="btn-pay-auction"
                                        onClick={() => handleStartPayment(a.id)}
                                    >
                                        💳 Pay {a.currentPrice.toLocaleString()} Ft
                                    </button>
                                )}
                            </div>
                        </div>
                    );
                })}
            </div>
        </div>
    );
}
