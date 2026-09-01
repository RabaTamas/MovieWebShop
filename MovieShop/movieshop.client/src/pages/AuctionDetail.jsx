import { useState, useEffect, useRef, useCallback } from 'react';
import { useParams, Link } from 'react-router-dom';
import * as signalR from '@microsoft/signalr';
import { Elements } from '@stripe/react-stripe-js';
import { loadStripe } from '@stripe/stripe-js';
import StripeCheckout from '../components/StripeCheckout';
import API_BASE_URL from '../config/api';
import { useAuth } from '../contexts/AuthContext';
import './AuctionDetail.css';

const HUB_URL = API_BASE_URL.replace('/api', '') + '/hubs/auction';

function Countdown({ endsAt, onExpire }) {
    const [remaining, setRemaining] = useState('');
    const [urgent, setUrgent] = useState(false);

    useEffect(() => {
        const tick = () => {
            const diff = new Date(endsAt) - Date.now();
            if (diff <= 0) {
                setRemaining('Ended');
                onExpire?.();
                return;
            }
            setUrgent(diff < 60000);
            const h = Math.floor(diff / 3600000);
            const m = Math.floor((diff % 3600000) / 60000);
            const s = Math.floor((diff % 60000) / 1000);
            setRemaining(h > 0 ? `${h}h ${m}m ${s}s` : `${m}m ${s}s`);
        };
        tick();
        const id = setInterval(tick, 1000);
        return () => clearInterval(id);
    }, [endsAt, onExpire]);

    return <span className={urgent ? 'countdown urgent' : 'countdown'}>{remaining}</span>;
}

export default function AuctionDetail() {
    const { id } = useParams();
    const { token, user } = useAuth();
    const [auction, setAuction] = useState(null);
    const [loading, setLoading] = useState(true);
    const [bidAmount, setBidAmount] = useState('');
    const [bidding, setBidding] = useState(false);
    const [feedback, setFeedback] = useState(null); // { type: 'success'|'error', msg }
    const [events, setEvents] = useState([]);        // live event feed
    const [paymentClientSecret, setPaymentClientSecret] = useState(null);
    const [stripePromise, setStripePromise] = useState(null);
    const [paid, setPaid] = useState(false);
    const connectionRef = useRef(null);
    const eventsEndRef = useRef(null);

    const loadAuction = useCallback(async () => {
        try {
            const r = await fetch(`${API_BASE_URL}/api/Auction/${id}`);
            if (r.ok) {
                const data = await r.json();
                setAuction(data);
                setBidAmount(String(data.minBidIncrement ?? 1000));
            }
        } catch { /* ignore */ }
        setLoading(false);
    }, [id]);

    // SignalR connection
    useEffect(() => {
        loadAuction();

        const connection = new signalR.HubConnectionBuilder()
            .withUrl(HUB_URL, token
                ? { accessTokenFactory: () => token }
                : { skipNegotiation: false })
            .withAutomaticReconnect()
            .build();

        connection.on('BidPlaced', ({ bidderName, amount, newEndsAt, antiSniping, currentPrice }) => {
            setAuction(prev => prev ? {
                ...prev,
                currentPrice,
                endsAt: newEndsAt,
                currentBidderName: bidderName
            } : prev);

            setBidAmount(prev => prev); // keep user's increment input unchanged

            const msg = antiSniping
                ? `🏹 ${bidderName} bid ${amount.toLocaleString()} Ft — ⏱️ Auction extended!`
                : `🏹 ${bidderName} bid ${amount.toLocaleString()} Ft`;
            setEvents(prev => [...prev, { id: Date.now(), msg, time: new Date() }]);
        });

        connection.on('AuctionEnded', ({ winnerName, finalPrice, movieTitle }) => {
            setAuction(prev => prev ? { ...prev, status: 2 } : prev);
            const msg = winnerName
                ? `🏆 Auction ended! ${winnerName} won "${movieTitle}" for ${finalPrice.toLocaleString()} Ft`
                : `🏁 Auction ended with no bids.`;
            setEvents(prev => [...prev, { id: Date.now(), msg, time: new Date(), highlight: true }]);
        });

        connection.start()
            .then(() => connection.invoke('JoinAuction', parseInt(id)))
            .catch(console.error);

        connectionRef.current = connection;
        return () => {
            connection.invoke('LeaveAuction', parseInt(id)).catch(() => { });
            connection.stop();
        };
    }, [id, token, loadAuction]);

    // Auto-scroll event feed
    useEffect(() => {
        eventsEndRef.current?.scrollIntoView({ behavior: 'smooth' });
    }, [events]);

    const handleBid = async () => {
        const increment = parseFloat(bidAmount);
        const amount = auction.currentPrice + increment;
        if (isNaN(increment) || increment < minIncrement) {
            setFeedback({ type: 'error', msg: `Minimum raise is ${minIncrement.toLocaleString()} Ft.` });
            return;
        }

        setBidding(true);
        setFeedback(null);

        try {
            const r = await fetch(`${API_BASE_URL}/api/Auction/${id}/bid`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                    'Authorization': `Bearer ${token}`
                },
                body: JSON.stringify({ amount })
            });
            const data = await r.json();

            if (r.ok) {
                setFeedback({ type: 'success', msg: `✅ Bid of ${amount.toLocaleString()} Ft placed successfully!` + (data.antiSnipingTriggered ? ' ⏱️ Auction extended (anti-sniping).' : '') });
            } else {
                setFeedback({ type: 'error', msg: data.error || 'Bid failed.' });
            }
        } catch {
            setFeedback({ type: 'error', msg: 'Network error. Please try again.' });
        } finally {
            setBidding(false);
        }
    };

    if (loading) return (
        <div className="auction-detail-loading">
            <div className="spinner-border text-primary" role="status" />
        </div>
    );

    if (!auction) return (
        <div className="auction-not-found">
            <h3>Auction not found</h3>
            <Link to="/auctions">← Back to auctions</Link>
        </div>
    );

    const isActive       = auction.status === 1;
    const isEnded        = auction.status === 2;
    const isPending      = auction.status === 0;
    const isWinner       = isEnded && auction.currentBidderId === user?.id;
    const minIncrement   = auction.minBidIncrement ?? 1000;
    const totalBid       = auction.currentPrice + (parseFloat(bidAmount) || 0);

    const handleStartPayment = async () => {
        try {
            const r = await fetch(`${API_BASE_URL}/api/Auction/${id}/create-payment`, {
                method: 'POST',
                headers: { Authorization: `Bearer ${token}` }
            });
            if (!r.ok) { alert('Payment not available.'); return; }
            const data = await r.json();
            setStripePromise(loadStripe(data.publishableKey));
            setPaymentClientSecret(data.clientSecret);
        } catch { alert('Network error.'); }
    };

    const handlePaymentSuccess = async (paymentMethodId) => {
        const stripe = await stripePromise;
        const { error, paymentIntent } = await stripe.confirmCardPayment(paymentClientSecret, {
            payment_method: paymentMethodId
        });
        if (error) { alert('Payment failed: ' + error.message); return; }
        if (paymentIntent.status === 'succeeded') {
            const r = await fetch(`${API_BASE_URL}/api/Auction/${id}/confirm-payment`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
                body: JSON.stringify({ paymentIntentId: paymentIntent.id })
            });
            if (r.ok) {
                setPaid(true);
                setPaymentClientSecret(null);
            } else {
                alert('Payment confirmed by Stripe but server verification failed.');
            }
        }
    };

    return (
        <div className="auction-detail-page">
            <Link to="/auctions" className="back-link">← All auctions</Link>

            <div className="auction-detail-grid">
                {/* Left: movie info */}
                <div className="auction-movie-card">
                    <img src={auction.imageUrl} alt={auction.title} className="auction-movie-img" />
                    <div className="auction-movie-info">
                        <h2>{auction.title}</h2>
                        <div className="auction-meta">
                            <span>Starting price: <strong>{auction.startingPrice.toLocaleString()} Ft</strong></span>
                        </div>
                        {isEnded && (
                            <div className={`auction-ended-banner ${isWinner ? 'winner' : ''}`}>
                                {isWinner
                                    ? '🎉 You won this auction!'
                                    : auction.currentBidderName
                                        ? `🏆 Winner: ${auction.currentBidderName}`
                                        : '🏁 Ended with no bids'}
                            </div>
                        )}
                    </div>
                </div>

                {/* Right: bidding panel */}
                <div className="auction-bid-panel">
                    <div className="auction-status-row">
                        <span className={`auction-badge ${isActive ? 'badge-active' : isPending ? 'badge-pending' : 'badge-ended'}`}>
                            {isActive ? '🟢 Live' : isPending ? '🕐 Starting soon' : '🏁 Ended'}
                        </span>
                        {isActive && (
                            <div className="auction-time-block">
                                <span className="auction-time-label">Time remaining</span>
                                <Countdown endsAt={auction.endsAt} onExpire={loadAuction} />
                            </div>
                        )}
                        {isPending && (
                            <div className="auction-time-block">
                                <span className="auction-time-label">Starts at</span>
                                <span>{new Date(auction.startsAt).toLocaleString()}</span>
                            </div>
                        )}
                    </div>

                    <div className="current-price-block">
                        <div className="current-price-label">Current highest bid</div>
                        <div className="current-price-value">{auction.currentPrice.toLocaleString()} Ft</div>
                        {auction.currentBidderName && (
                            <div className="current-bidder">👑 {auction.currentBidderName}</div>
                        )}
                    </div>

                    {/* Bid form — only for active auctions, logged-in users */}
                    {isActive && token && (
                        <div className="bid-form">
                            <label className="bid-label">
                                Raise by (min. {minIncrement.toLocaleString()} Ft)
                            </label>
                            <div className="bid-input-row">
                                <input
                                    type="number"
                                    value={bidAmount}
                                    onChange={e => setBidAmount(e.target.value)}
                                    min={minIncrement}
                                    step={minIncrement}
                                    className="bid-input"
                                    disabled={bidding}
                                    placeholder={minIncrement.toLocaleString()}
                                />
                                <span className="bid-currency">Ft</span>
                            </div>
                            <div className="bid-total-preview">
                                Your total bid: <strong>{totalBid.toLocaleString()} Ft</strong>
                            </div>
                            <button
                                className="btn-place-bid"
                                onClick={handleBid}
                                disabled={bidding || parseFloat(bidAmount) < minIncrement}
                            >
                                {bidding ? 'Placing bid…' : '⚡ Place Bid'}
                            </button>

                            {feedback && (
                                <div className={`bid-feedback ${feedback.type}`}>
                                    {feedback.msg}
                                </div>
                            )}

                            <p className="anti-sniping-note">
                                ⏱️ Bids placed in the last 60 seconds extend the auction by 1 minute (anti-sniping).
                            </p>
                        </div>
                    )}

                    {isActive && !token && (
                        <div className="bid-login-prompt">
                            <Link to="/login">Log in</Link> to place a bid.
                        </div>
                    )}

                    {/* Winner payment section */}
                    {isEnded && isWinner && (
                        <div className="winner-payment-section">
                            {paid || auction.isPaid ? (
                                <div className="payment-success-banner">
                                    ✅ Payment complete — the item will be delivered to you.
                                </div>
                            ) : !paymentClientSecret ? (
                                <button className="btn-pay-now" onClick={handleStartPayment}>
                                    💳 Pay {auction.currentPrice.toLocaleString()} Ft
                                </button>
                            ) : (
                                stripePromise && (
                                    <Elements stripe={stripePromise} options={{ clientSecret: paymentClientSecret }}>
                                        <StripeCheckout
                                            amount={auction.currentPrice}
                                            onSuccess={handlePaymentSuccess}
                                            onError={msg => alert('Payment error: ' + msg)}
                                        />
                                    </Elements>
                                )
                            )}
                        </div>
                    )}
                </div>
            </div>

            {/* Live event feed + bid history */}
            <div className="auction-bottom-grid">
                {/* Live events */}
                <div className="auction-events">
                    <h5>⚡ Live feed</h5>
                    <div className="events-list">
                        {events.length === 0 && <p className="no-events">Waiting for bids…</p>}
                        {events.map(e => (
                            <div key={e.id} className={`event-item ${e.highlight ? 'highlight' : ''}`}>
                                <span className="event-time">{e.time.toLocaleTimeString()}</span>
                                <span className="event-msg">{e.msg}</span>
                            </div>
                        ))}
                        <div ref={eventsEndRef} />
                    </div>
                </div>

                {/* Bid history */}
                <div className="auction-bids">
                    <h5>📋 Bid history</h5>
                    {auction.recentBids.length === 0
                        ? <p className="no-bids">No bids yet. Be the first!</p>
                        : (
                            <table className="bids-table">
                                <thead>
                                    <tr>
                                        <th>Bidder</th>
                                        <th>Amount</th>
                                        <th>Time</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {auction.recentBids.map(b => (
                                        <tr key={b.id} className={b.id === auction.recentBids[0]?.id ? 'top-bid' : ''}>
                                            <td>{b.bidderName}</td>
                                            <td><strong>{b.amount.toLocaleString()} Ft</strong></td>
                                            <td>{new Date(b.placedAt).toLocaleTimeString()}</td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        )
                    }
                </div>
            </div>
        </div>
    );
}
