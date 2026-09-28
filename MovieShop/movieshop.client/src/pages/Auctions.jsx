import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { CheckCircle2, Clock, CreditCard, Crown, Flag, Gavel, Timer, Trophy, Zap } from 'lucide-react';
import API_BASE_URL from '../config/api';
import { useAuth } from '../contexts/AuthContext';
import AuctionCountdown from '../components/AuctionCountdown';
import { cn, formatPrice } from '@/lib/utils';
import { Skeleton } from '@/components/ui/skeleton';
import { EmptyState, PageContainer, PageHeader } from '@/components/ui/page';

const statusLabel = { 0: 'Pending', 1: 'Active', 2: 'Ended' };
const statusClass = {
    0: 'bg-sky-500 text-white',
    1: 'bg-success text-white',
    2: 'bg-zinc-700 text-zinc-100',
};

function AuctionCard({ to, imageUrl, title, badges, children, footer, footerClass, className }) {
    return (
        <Link
            to={to}
            className={cn(
                "group relative flex flex-col overflow-hidden rounded-xl border bg-card transition-all duration-300 hover:-translate-y-1 hover:border-primary/50 hover:shadow-2xl hover:shadow-primary/10",
                className
            )}
        >
            <div className="relative aspect-[16/10] overflow-hidden bg-muted">
                {imageUrl && (
                    <img
                        src={imageUrl}
                        alt={title}
                        loading="lazy"
                        className="size-full object-cover object-top transition-transform duration-500 group-hover:scale-105"
                    />
                )}
                <div className="absolute inset-0 bg-gradient-to-t from-black/70 via-transparent to-transparent" />
                <div className="absolute inset-x-3 top-3 flex flex-wrap items-start justify-between gap-2">{badges}</div>
                <h3 className="absolute inset-x-3 bottom-3 line-clamp-2 text-lg leading-tight font-semibold text-white drop-shadow">
                    {title}
                </h3>
            </div>
            <div className="flex flex-1 flex-col gap-3 p-4">{children}</div>
            <div className={cn("flex items-center justify-center gap-2 border-t px-4 py-2.5 text-sm font-medium transition-colors", footerClass)}>
                {footer}
            </div>
        </Link>
    );
}

const StatusPill = ({ className, children }) => (
    <span className={cn("inline-flex items-center gap-1 rounded-md px-2 py-0.5 text-xs font-semibold shadow", className)}>
        {children}
    </span>
);

const Label = ({ children }) => (
    <div className="text-xs tracking-wider text-muted-foreground uppercase">{children}</div>
);

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

    const header = (
        <PageHeader
            title="Live Movie Auctions"
            icon={Gavel}
            description="Bid on exclusive movies and win at the lowest price!"
        />
    );

    if (loading) return (
        <PageContainer size="xl">
            {header}
            <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3" aria-label="Loading auctions…">
                {Array.from({ length: 6 }).map((_, i) => <Skeleton key={i} className="h-80 rounded-xl" />)}
            </div>
        </PageContainer>
    );

    if (auctions.length === 0) return (
        <PageContainer size="xl">
            {header}
            <EmptyState
                icon={Gavel}
                title="No auctions right now"
                description="Check back soon — new movie auctions are added regularly!"
            />
        </PageContainer>
    );

    return (
        <PageContainer size="xl">
            {header}

            {wonAuctions.length > 0 && (
                <section className="mb-12 rounded-2xl border border-primary/30 bg-gradient-to-br from-primary/10 to-transparent p-5 sm:p-6">
                    <h2 className="mb-5 flex items-center gap-2 text-xl font-semibold">
                        <Trophy className="size-5 text-primary" /> Your Won Auctions
                    </h2>
                    <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
                        {wonAuctions.map(a => (
                            <AuctionCard
                                key={a.id}
                                to={`/auctions/${a.id}`}
                                imageUrl={a.imageUrl}
                                title={a.title}
                                className="border-primary/30"
                                badges={
                                    <>
                                        <StatusPill className={statusClass[2]}>Ended</StatusPill>
                                        {a.isPaid
                                            ? <StatusPill className="bg-success text-white"><CheckCircle2 className="size-3" />Paid</StatusPill>
                                            : <StatusPill className="bg-primary text-primary-foreground"><CreditCard className="size-3" />Unpaid</StatusPill>}
                                    </>
                                }
                                footer={a.isPaid ? <><CheckCircle2 className="size-4" />Paid</> : <><CreditCard className="size-4" />Pay now</>}
                                footerClass={a.isPaid ? "bg-success/10 text-success" : "bg-primary text-primary-foreground group-hover:bg-primary/90"}
                            >
                                <div>
                                    <Label>Final bid</Label>
                                    <div className="text-2xl font-bold">{formatPrice(a.currentPrice)}</div>
                                </div>
                            </AuctionCard>
                        ))}
                    </div>
                </section>
            )}

            <div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
                {auctions.map(a => (
                    <AuctionCard
                        key={a.id}
                        to={`/auctions/${a.id}`}
                        imageUrl={a.imageUrl}
                        title={a.title}
                        className={a.status === 1 ? "glow-border shadow-lg shadow-primary/10" : undefined}
                        badges={
                            <StatusPill className={statusClass[a.status]}>
                                {a.status === 1 && <span className="size-1.5 animate-pulse rounded-full bg-white" />}
                                {statusLabel[a.status]}
                            </StatusPill>
                        }
                        footer={
                            a.status === 1 ? <><Zap className="size-4" />Place a bid</>
                                : a.status === 0 ? <><Clock className="size-4" />Coming soon</>
                                    : <><Flag className="size-4" />View result</>
                        }
                        footerClass={
                            a.status === 1
                                ? "text-primary group-hover:bg-primary group-hover:text-primary-foreground"
                                : "text-muted-foreground group-hover:bg-muted group-hover:text-foreground"
                        }
                    >
                        <div className="flex items-end justify-between gap-3">
                            <div>
                                <Label>Current bid</Label>
                                <div className="text-2xl font-bold">{formatPrice(a.currentPrice)}</div>
                            </div>
                            {a.status === 1 && (
                                <div className="text-right">
                                    <Label>Ends in</Label>
                                    <div className="flex items-center justify-end gap-1 font-semibold text-primary">
                                        <Timer className="size-4" />
                                        <AuctionCountdown endsAt={a.endsAt} highlightUrgent={false} />
                                    </div>
                                </div>
                            )}
                            {a.status === 0 && (
                                <div className="text-right">
                                    <Label>Starts</Label>
                                    <div className="text-sm font-medium">
                                        {new Date(a.startsAt).toLocaleString()}
                                    </div>
                                </div>
                            )}
                        </div>
                        {a.currentBidderName && (
                            <div className="flex items-center gap-1.5 text-sm text-muted-foreground">
                                <Crown className="size-4 text-primary" /> Leading: <strong className="text-foreground">{a.currentBidderName}</strong>
                            </div>
                        )}
                    </AuctionCard>
                ))}
            </div>
        </PageContainer>
    );
}
