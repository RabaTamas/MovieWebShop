import { useState, useEffect, useRef, useCallback } from 'react';
import { useParams, Link } from 'react-router-dom';
import * as signalR from '@microsoft/signalr';
import { Elements } from '@stripe/react-stripe-js';
import { loadStripe } from '@stripe/stripe-js';
import { toast } from 'sonner';
import { motion } from 'motion/react';
import {
    AlertCircle, ArrowLeft, CheckCircle2, Clock, CreditCard, Crown, Flag, History, PartyPopper, Radio, Timer, Trophy, Zap,
} from 'lucide-react';
import StripeCheckout from '../components/StripeCheckout';
import AuctionCountdown from '../components/AuctionCountdown';
import API_BASE_URL from '../config/api';
import { useAuth } from '../contexts/AuthContext';
import { cn, formatPrice } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { LoadingState, Spinner } from '@/components/ui/spinner';
import { EmptyState, PageContainer } from '@/components/ui/page';

const HUB_URL = API_BASE_URL.replace('/api', '') + '/hubs/auction';

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
                ? `${bidderName} bid ${amount.toLocaleString()} Ft — Auction extended!`
                : `${bidderName} bid ${amount.toLocaleString()} Ft`;
            setEvents(prev => [...prev, { id: Date.now(), msg, time: new Date(), antiSniping }]);
        });

        connection.on('AuctionEnded', ({ winnerName, finalPrice, movieTitle }) => {
            setAuction(prev => prev ? { ...prev, status: 2 } : prev);
            const msg = winnerName
                ? `Auction ended! ${winnerName} won "${movieTitle}" for ${finalPrice.toLocaleString()} Ft`
                : `Auction ended with no bids.`;
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
        eventsEndRef.current?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
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
                setFeedback({ type: 'success', msg: `Bid of ${amount.toLocaleString()} Ft placed successfully!` + (data.antiSnipingTriggered ? ' Auction extended (anti-sniping).' : '') });
            } else {
                setFeedback({ type: 'error', msg: data.error || 'Bid failed.' });
            }
        } catch {
            setFeedback({ type: 'error', msg: 'Network error. Please try again.' });
        } finally {
            setBidding(false);
        }
    };

    if (loading) return <LoadingState label="Loading auction…" />;

    if (!auction) return (
        <PageContainer size="md">
            <EmptyState icon={AlertCircle} title="Auction not found">
                <Button variant="outline" asChild>
                    <Link to="/auctions"><ArrowLeft />Back to auctions</Link>
                </Button>
            </EmptyState>
        </PageContainer>
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
            if (!r.ok) { toast.error('Payment not available.'); return; }
            const data = await r.json();
            setStripePromise(loadStripe(data.publishableKey));
            setPaymentClientSecret(data.clientSecret);
        } catch { toast.error('Network error.'); }
    };

    const handlePaymentSuccess = async (paymentMethodId) => {
        const stripe = await stripePromise;
        const { error, paymentIntent } = await stripe.confirmCardPayment(paymentClientSecret, {
            payment_method: paymentMethodId
        });
        if (error) { toast.error('Payment failed: ' + error.message); return; }
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
                toast.error('Payment confirmed by Stripe but server verification failed.');
            }
        }
    };

    return (
        <PageContainer size="xl">
            <Button variant="ghost" size="sm" className="mb-6" asChild>
                <Link to="/auctions"><ArrowLeft />All auctions</Link>
            </Button>

            <div className="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_420px]">
                {/* Left: movie info */}
                <div className="relative isolate overflow-hidden rounded-2xl border bg-card">
                    {auction.imageUrl && (
                        <div
                            className="absolute inset-0 -z-10 scale-110 bg-cover bg-center opacity-30 blur-2xl"
                            style={{ backgroundImage: `url(${auction.imageUrl})` }}
                            aria-hidden="true"
                        />
                    )}
                    <div className="grid gap-6 bg-gradient-to-br from-card/70 to-card p-6 sm:grid-cols-[200px_1fr]">
                        <img
                            src={auction.imageUrl}
                            alt={auction.title}
                            className="mx-auto aspect-[2/3] w-full max-w-[200px] rounded-xl object-cover shadow-2xl ring-1 ring-white/10"
                        />
                        <div className="flex flex-col gap-4">
                            <h1 className="font-display text-5xl leading-none tracking-wide text-balance">{auction.title}</h1>
                            <p className="text-muted-foreground">
                                Starting price: <strong className="text-foreground">{formatPrice(auction.startingPrice)}</strong>
                            </p>
                            {isEnded && (
                                <div className={cn(
                                    "flex items-center gap-3 rounded-xl border p-4 font-semibold",
                                    isWinner ? "border-primary/40 bg-primary/15 text-primary" : "bg-muted/60"
                                )}>
                                    {isWinner
                                        ? <><PartyPopper className="size-5" />You won this auction!</>
                                        : auction.currentBidderName
                                            ? <><Trophy className="size-5 text-primary" />Winner: {auction.currentBidderName}</>
                                            : <><Flag className="size-5" />Ended with no bids</>}
                                </div>
                            )}
                        </div>
                    </div>
                </div>

                {/* Right: bidding panel */}
                <Card className="gap-5 lg:sticky lg:top-24">
                    <CardContent className="space-y-5">
                        <div className="flex flex-wrap items-center justify-between gap-3">
                            <span className={cn(
                                "inline-flex items-center gap-1.5 rounded-full px-3 py-1 text-sm font-semibold",
                                isActive ? "bg-success/15 text-success" : isPending ? "bg-sky-500/15 text-sky-400" : "bg-muted text-muted-foreground"
                            )}>
                                {isActive ? <><Radio className="size-4 animate-pulse" />Live</> : isPending ? <><Clock className="size-4" />Starting soon</> : <><Flag className="size-4" />Ended</>}
                            </span>
                            {isActive && (
                                <div className="text-right">
                                    <div className="text-xs tracking-wider text-muted-foreground uppercase">Time remaining</div>
                                    <div className="flex items-center justify-end gap-1.5 text-xl font-bold">
                                        <Timer className="size-5 text-primary" />
                                        <AuctionCountdown endsAt={auction.endsAt} onExpire={loadAuction} />
                                    </div>
                                </div>
                            )}
                            {isPending && (
                                <div className="text-right">
                                    <div className="text-xs tracking-wider text-muted-foreground uppercase">Starts at</div>
                                    <div className="font-medium">{new Date(auction.startsAt).toLocaleString()}</div>
                                </div>
                            )}
                        </div>

                        <div className="rounded-xl bg-muted/50 p-5 text-center">
                            <div className="text-xs tracking-wider text-muted-foreground uppercase">Current highest bid</div>
                            {/* Új licitnél a ár „ráugrik": a key váltás újraindítja az animációt */}
                            <motion.div
                                key={auction.currentPrice}
                                initial={{ scale: 1.35, opacity: 0.3, textShadow: "0 0 40px var(--primary)" }}
                                animate={{ scale: 1, opacity: 1, textShadow: "0 0 0px transparent" }}
                                transition={{ type: "spring", stiffness: 260, damping: 16 }}
                                className="mt-1 text-4xl font-bold text-primary"
                            >
                                {formatPrice(auction.currentPrice)}
                            </motion.div>
                            {auction.currentBidderName && (
                                <div className="mt-2 flex items-center justify-center gap-1.5 text-sm text-muted-foreground">
                                    <Crown className="size-4 text-primary" /> {auction.currentBidderName}
                                </div>
                            )}
                        </div>

                        {/* Bid form — only for active auctions, logged-in users */}
                        {isActive && token && (
                            <div className="space-y-3">
                                <Label htmlFor="bid-amount">
                                    Raise by (min. {formatPrice(minIncrement)})
                                </Label>
                                <div className="relative">
                                    <Input
                                        id="bid-amount"
                                        type="number"
                                        value={bidAmount}
                                        onChange={e => setBidAmount(e.target.value)}
                                        min={minIncrement}
                                        step={minIncrement}
                                        className="h-11 pr-10 text-lg"
                                        disabled={bidding}
                                        placeholder={minIncrement.toLocaleString()}
                                    />
                                    <span className="pointer-events-none absolute top-1/2 right-3 -translate-y-1/2 text-sm text-muted-foreground">Ft</span>
                                </div>
                                <div className="text-sm text-muted-foreground">
                                    Your total bid: <strong className="text-foreground">{formatPrice(totalBid)}</strong>
                                </div>
                                <Button
                                    size="lg"
                                    className="h-12 w-full text-base"
                                    onClick={handleBid}
                                    disabled={bidding || parseFloat(bidAmount) < minIncrement}
                                >
                                    {bidding ? <><Spinner />Placing bid…</> : <><Zap />Place Bid</>}
                                </Button>

                                {feedback && (
                                    <div className={cn(
                                        "flex items-start gap-2 rounded-lg border px-3 py-2 text-sm",
                                        feedback.type === 'success' ? "border-success/30 bg-success/10 text-success" : "border-destructive/30 bg-destructive/10 text-destructive"
                                    )}>
                                        {feedback.type === 'success' ? <CheckCircle2 className="mt-0.5 size-4 shrink-0" /> : <AlertCircle className="mt-0.5 size-4 shrink-0" />}
                                        {feedback.msg}
                                    </div>
                                )}

                                <p className="flex items-start gap-1.5 text-xs text-muted-foreground">
                                    <Timer className="mt-0.5 size-3.5 shrink-0" />
                                    Bids placed in the last 60 seconds extend the auction by 1 minute (anti-sniping).
                                </p>
                            </div>
                        )}

                        {isActive && !token && (
                            <div className="rounded-lg bg-muted/60 p-4 text-center text-sm">
                                <Link to="/login" className="font-semibold text-primary hover:underline">Log in</Link> to place a bid.
                            </div>
                        )}

                        {/* Winner payment section */}
                        {isEnded && isWinner && (
                            <div className="space-y-3">
                                {paid || auction.isPaid ? (
                                    <div className="flex items-center gap-2 rounded-lg border border-success/30 bg-success/10 p-4 text-sm font-medium text-success">
                                        <CheckCircle2 className="size-5 shrink-0" />
                                        Payment complete — the item will be delivered to you.
                                    </div>
                                ) : !paymentClientSecret ? (
                                    <Button size="lg" className="h-12 w-full text-base" onClick={handleStartPayment}>
                                        <CreditCard /> Pay {formatPrice(auction.currentPrice)}
                                    </Button>
                                ) : (
                                    stripePromise && (
                                        <Elements stripe={stripePromise} options={{ clientSecret: paymentClientSecret }}>
                                            <StripeCheckout
                                                amount={auction.currentPrice}
                                                onSuccess={handlePaymentSuccess}
                                                onError={msg => toast.error('Payment error: ' + msg)}
                                            />
                                        </Elements>
                                    )
                                )}
                            </div>
                        )}
                    </CardContent>
                </Card>
            </div>

            {/* Live event feed + bid history */}
            <div className="mt-6 grid gap-6 lg:grid-cols-2">
                {/* Live events */}
                <Card className="gap-3">
                    <CardHeader>
                        <CardTitle className="flex items-center gap-2"><Zap className="size-4 text-primary" />Live feed</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="h-64 space-y-2 overflow-y-auto pr-1">
                            {events.length === 0 && <p className="py-10 text-center text-sm text-muted-foreground">Waiting for bids…</p>}
                            {events.map(e => (
                                <motion.div
                                    key={e.id}
                                    initial={{ opacity: 0, x: -24, scale: 0.96 }}
                                    animate={{ opacity: 1, x: 0, scale: 1 }}
                                    transition={{ type: "spring", stiffness: 300, damping: 24 }}
                                    className={cn(
                                        "flex gap-3 rounded-lg px-3 py-2 text-sm",
                                        e.highlight ? "border border-primary/30 bg-primary/10 font-medium" : "bg-muted/50"
                                    )}
                                >
                                    <span className="shrink-0 font-mono text-xs text-muted-foreground tabular-nums">{e.time.toLocaleTimeString()}</span>
                                    <span className="flex items-center gap-1.5">
                                        {e.highlight ? <Trophy className="size-3.5 shrink-0 text-primary" /> : <Zap className="size-3.5 shrink-0 text-primary" />}
                                        {e.msg}
                                        {e.antiSniping && <Timer className="size-3.5 shrink-0 text-destructive" />}
                                    </span>
                                </motion.div>
                            ))}
                            <div ref={eventsEndRef} />
                        </div>
                    </CardContent>
                </Card>

                {/* Bid history */}
                <Card className="gap-3">
                    <CardHeader>
                        <CardTitle className="flex items-center gap-2"><History className="size-4 text-primary" />Bid history</CardTitle>
                    </CardHeader>
                    <CardContent>
                        {auction.recentBids.length === 0
                            ? <p className="py-10 text-center text-sm text-muted-foreground">No bids yet. Be the first!</p>
                            : (
                                <Table containerClassName="max-h-64 overflow-y-auto">
                                    <TableHeader>
                                        <TableRow className="hover:bg-transparent">
                                            <TableHead>Bidder</TableHead>
                                            <TableHead>Amount</TableHead>
                                            <TableHead className="text-right">Time</TableHead>
                                        </TableRow>
                                    </TableHeader>
                                    <TableBody>
                                        {auction.recentBids.map(b => {
                                            const isTop = b.id === auction.recentBids[0]?.id;
                                            return (
                                                <TableRow key={b.id} className={cn(isTop && "bg-primary/10 hover:bg-primary/15")}>
                                                    <TableCell className="font-medium">
                                                        <span className="flex items-center gap-1.5">
                                                            {isTop && <Crown className="size-3.5 text-primary" />}
                                                            {b.bidderName}
                                                        </span>
                                                    </TableCell>
                                                    <TableCell className="font-semibold">{formatPrice(b.amount)}</TableCell>
                                                    <TableCell className="text-right text-muted-foreground tabular-nums">{new Date(b.placedAt).toLocaleTimeString()}</TableCell>
                                                </TableRow>
                                            );
                                        })}
                                    </TableBody>
                                </Table>
                            )
                        }
                    </CardContent>
                </Card>
            </div>
        </PageContainer>
    );
}
