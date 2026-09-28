import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { Elements } from '@stripe/react-stripe-js';
import { loadStripe } from '@stripe/stripe-js';
import { toast } from 'sonner';
import { CheckCircle2, CreditCard, ExternalLink, Trophy } from 'lucide-react';
import StripeCheckout from '../components/StripeCheckout';
import API_BASE_URL from '../config/api';
import { useAuth } from '../contexts/AuthContext';
import { cn, formatPrice } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { LoadingState } from '@/components/ui/spinner';
import { EmptyState, PageContainer, PageHeader } from '@/components/ui/page';

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
            if (!r.ok) { toast.error('Payment not available for this auction.'); return; }
            const data = await r.json();
            setPaymentState(prev => ({
                ...prev,
                [auctionId]: {
                    clientSecret: data.clientSecret,
                    stripePromise: loadStripe(data.publishableKey),
                    paid: false
                }
            }));
        } catch { toast.error('Network error.'); }
    };

    const handlePaymentSuccess = async (auctionId, paymentMethodId) => {
        const ps = paymentState[auctionId];
        if (!ps) return;
        const stripe = await ps.stripePromise;
        const { error, paymentIntent } = await stripe.confirmCardPayment(ps.clientSecret, {
            payment_method: paymentMethodId
        });
        if (error) { toast.error('Payment failed: ' + error.message); return; }
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
                toast.error('Payment confirmed by Stripe but server verification failed.');
            }
        }
    };

    if (loading) return <LoadingState label="Loading your won auctions…" />;

    if (wonAuctions.length === 0) return (
        <PageContainer size="md">
            <EmptyState
                icon={Trophy}
                title="No won auctions yet"
                description="Win an auction and your items will appear here for payment."
            >
                <Button asChild><Link to="/auctions">Browse Auctions</Link></Button>
            </EmptyState>
        </PageContainer>
    );

    const unpaidCount = wonAuctions.filter(a => !a.isPaid && !paymentState[a.id]?.paid).length;

    return (
        <PageContainer size="lg" className="max-w-4xl">
            <PageHeader
                title="My Won Auctions"
                icon={Trophy}
                description={unpaidCount > 0
                    ? `You have ${unpaidCount} unpaid auction${unpaidCount > 1 ? 's' : ''}. Pay now to receive your items.`
                    : 'All your won auctions are paid. Enjoy your movies!'}
            />

            <div className="space-y-4">
                {wonAuctions.map(a => {
                    const ps = paymentState[a.id];
                    const isPaid = a.isPaid || ps?.paid;

                    return (
                        <div
                            key={a.id}
                            className={cn(
                                "flex flex-col overflow-hidden rounded-xl border bg-card sm:flex-row",
                                isPaid ? "border-success/30" : "border-primary/40"
                            )}
                        >
                            <div className="relative aspect-video shrink-0 bg-muted sm:aspect-auto sm:w-44">
                                <img src={a.imageUrl} alt={a.title} className="size-full object-cover" />
                                <div className="absolute top-2 left-2">
                                    {isPaid
                                        ? <Badge className="bg-success text-white"><CheckCircle2 />Paid</Badge>
                                        : <Badge><CreditCard />Unpaid</Badge>}
                                </div>
                            </div>

                            <div className="flex flex-1 flex-col gap-4 p-5">
                                <div className="flex flex-wrap items-start justify-between gap-3">
                                    <div>
                                        <h2 className="text-xl font-semibold">{a.title}</h2>
                                        <div className="mt-1 text-xs tracking-wider text-muted-foreground uppercase">Final bid</div>
                                        <div className="text-2xl font-bold text-primary">{formatPrice(a.currentPrice)}</div>
                                    </div>
                                    <Button variant="outline" size="sm" asChild>
                                        <Link to={`/auctions/${a.id}`}>View auction <ExternalLink /></Link>
                                    </Button>
                                </div>

                                {isPaid ? (
                                    <div className="flex items-center gap-2 rounded-lg border border-success/30 bg-success/10 px-4 py-3 text-sm font-medium text-success">
                                        <CheckCircle2 className="size-4" />
                                        Payment complete — your item is on its way!
                                    </div>
                                ) : ps?.clientSecret ? (
                                    <div className="space-y-3 rounded-lg border bg-muted/30 p-4">
                                        <h3 className="font-medium">Complete your payment</h3>
                                        {ps.stripePromise && (
                                            <Elements stripe={ps.stripePromise} options={{ clientSecret: ps.clientSecret }}>
                                                <StripeCheckout
                                                    amount={a.currentPrice}
                                                    onSuccess={(pmId) => handlePaymentSuccess(a.id, pmId)}
                                                    onError={(msg) => toast.error('Payment error: ' + msg)}
                                                />
                                            </Elements>
                                        )}
                                        <Button
                                            variant="ghost"
                                            size="sm"
                                            className="w-full"
                                            onClick={() => setPaymentState(prev => {
                                                const next = { ...prev };
                                                delete next[a.id];
                                                return next;
                                            })}
                                        >
                                            Cancel
                                        </Button>
                                    </div>
                                ) : (
                                    <Button size="lg" className="self-start" onClick={() => handleStartPayment(a.id)}>
                                        <CreditCard /> Pay {formatPrice(a.currentPrice)}
                                    </Button>
                                )}
                            </div>
                        </div>
                    );
                })}
            </div>
        </PageContainer>
    );
}
