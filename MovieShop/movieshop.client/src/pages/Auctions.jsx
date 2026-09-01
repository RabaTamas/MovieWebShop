import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import API_BASE_URL from '../config/api';
import { useAuth } from '../contexts/AuthContext';
import './Auctions.css';

const statusLabel = { 0: 'Pending', 1: 'Active', 2: 'Ended' };
const statusClass = { 0: 'pending', 1: 'active', 2: 'ended' };

function Countdown({ endsAt }) {
    const [remaining, setRemaining] = useState('');

    useEffect(() => {
        const tick = () => {
            const diff = new Date(endsAt) - Date.now();
            if (diff <= 0) { setRemaining('Ended'); return; }
            const h = Math.floor(diff / 3600000);
            const m = Math.floor((diff % 3600000) / 60000);
            const s = Math.floor((diff % 60000) / 1000);
            setRemaining(h > 0 ? `${h}h ${m}m ${s}s` : `${m}m ${s}s`);
        };
        tick();
        const id = setInterval(tick, 1000);
        return () => clearInterval(id);
    }, [endsAt]);

    return <span>{remaining}</span>;
}

export default function Auctions() {
    const { token } = useAuth();
    const [auctions, setAuctions] = useState([]);
    const [wonAuctions, setWonAuctions] = useState([]);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        fetch(`${API_BASE_URL}/api/Auction`)
            .then(r => r.json())
            .then(data => { setAuctions(data); setLoading(false); })
            .catch(() => setLoading(false));

        const id = setInterval(() => {
            fetch(`${API_BASE_URL}/api/Auction`)
                .then(r => r.json())
                .then(setAuctions);
        }, 30000);
        return () => clearInterval(id);
    }, []);

    useEffect(() => {
        if (!token) { setWonAuctions([]); return; }
        fetch(`${API_BASE_URL}/api/Auction/my-wins`, {
            headers: { Authorization: `Bearer ${token}` }
        })
            .then(r => r.ok ? r.json() : [])
            .then(setWonAuctions)
            .catch(() => {});
    }, [token]);

    if (loading) return (
        <div className="auction-loading">
            <div className="spinner-border text-primary" role="status" />
            <p>Loading auctions…</p>
        </div>
    );

    if (auctions.length === 0) return (
        <div className="auction-empty">
            <div className="auction-empty-icon">🎬</div>
            <h3>No auctions right now</h3>
            <p>Check back soon — new movie auctions are added regularly!</p>
        </div>
    );

    return (
        <div className="auctions-page">
            <div className="auctions-header">
                <h1>🎬 Live Movie Auctions</h1>
                <p>Bid on exclusive movies and win at the lowest price!</p>
            </div>

            {wonAuctions.length > 0 && (
                <div className="won-auctions-section">
                    <h4 className="won-auctions-title">🏆 Your Won Auctions</h4>
                    <div className="auctions-grid">
                        {wonAuctions.map(a => (
                            <Link to={`/auctions/${a.id}`} key={a.id} className="auction-card won">
                                <div className="auction-card-img">
                                    <img src={a.imageUrl} alt={a.title} />
                                    <span className="auction-status-badge ended">Ended</span>
                                    {!a.isPaid && <span className="unpaid-badge">💳 Unpaid</span>}
                                    {a.isPaid  && <span className="paid-badge">✅ Paid</span>}
                                </div>
                                <div className="auction-card-body">
                                    <h5 className="auction-title">{a.title}</h5>
                                    <div className="auction-price-row">
                                        <div>
                                            <div className="auction-label">Final bid</div>
                                            <div className="auction-price">{a.currentPrice.toLocaleString()} Ft</div>
                                        </div>
                                    </div>
                                </div>
                                <div className={`auction-card-footer ${a.isPaid ? 'paid' : 'unpaid'}`}>
                                    {a.isPaid ? '✅ Paid' : '💳 Pay now'}
                                </div>
                            </Link>
                        ))}
                    </div>
                </div>
            )}

            <div className="auctions-grid">
                {auctions.map(a => (
                    <Link to={`/auctions/${a.id}`} key={a.id} className="auction-card">
                        <div className="auction-card-img">
                            <img src={a.imageUrl} alt={a.title} />
                            <span className={`auction-status-badge ${statusClass[a.status]}`}>
                                {statusLabel[a.status]}
                            </span>
                        </div>
                        <div className="auction-card-body">
                            <h5 className="auction-title">{a.title}</h5>
                            <div className="auction-price-row">
                                <div>
                                    <div className="auction-label">Current bid</div>
                                    <div className="auction-price">{a.currentPrice.toLocaleString()} Ft</div>
                                </div>
                                {a.status === 1 && (
                                    <div className="auction-timer">
                                        <div className="auction-label">Ends in</div>
                                        <div className="auction-countdown">
                                            <Countdown endsAt={a.endsAt} />
                                        </div>
                                    </div>
                                )}
                                {a.status === 0 && (
                                    <div className="auction-timer">
                                        <div className="auction-label">Starts</div>
                                        <div className="auction-countdown">
                                            {new Date(a.startsAt).toLocaleString()}
                                        </div>
                                    </div>
                                )}
                            </div>
                            {a.currentBidderName && (
                                <div className="auction-leader">
                                    👑 Leading: <strong>{a.currentBidderName}</strong>
                                </div>
                            )}
                        </div>
                        <div className="auction-card-footer">
                            {a.status === 1 ? '⚡ Place a bid' : a.status === 0 ? '🕐 Coming soon' : '🏁 View result'}
                        </div>
                    </Link>
                ))}
            </div>
        </div>
    );
}
