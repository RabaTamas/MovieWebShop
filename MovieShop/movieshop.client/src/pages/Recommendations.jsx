import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { Bot, Clapperboard, Info, ShoppingBag, Sparkles, Users } from 'lucide-react';
import MovieCard from '../components/MovieCard';
import API_BASE_URL from '../config/api';
import { useAuth } from '../contexts/AuthContext';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { LoadingState } from '@/components/ui/spinner';
import { EmptyState, PageContainer, PageHeader } from '@/components/ui/page';

function HorizontalRow({ movies, badge, badgeClass }) {
    return (
        <div className="scrollbar-none -mx-4 flex snap-x snap-mandatory gap-4 overflow-x-auto px-4 pt-2 pb-4">
            {movies.map(movie => (
                <div key={movie.id} className="relative w-40 shrink-0 snap-start sm:w-48">
                    <span
                        className={cn(
                            "pointer-events-none absolute top-2 right-2 z-10 inline-flex items-center gap-1 rounded-md px-1.5 py-0.5 text-[10px] font-semibold shadow",
                            badgeClass
                        )}
                    >
                        {badge}
                    </span>
                    <MovieCard movie={movie} />
                    {movie.reason && (
                        <div className="mt-1 flex items-center gap-1 truncate px-0.5 text-xs text-muted-foreground" title={movie.reason}>
                            <Info className="size-3 shrink-0" />
                            <span className="truncate">{movie.reason}</span>
                        </div>
                    )}
                </div>
            ))}
        </div>
    );
}

HorizontalRow.propTypes = {
    movies: PropTypes.array.isRequired,
    badge: PropTypes.node.isRequired,
    badgeClass: PropTypes.string.isRequired,
};

export default function Recommendations() {
    const { token } = useAuth();
    const [data, setData] = useState(null);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        if (!token) { setLoading(false); return; }
        fetch(`${API_BASE_URL}/api/Recommendation`, {
            headers: { Authorization: `Bearer ${token}` }
        })
            .then(r => r.ok ? r.json() : null)
            .then(d => { setData(d); setLoading(false); })
            .catch(() => setLoading(false));
    }, [token]);

    if (!token) return (
        <PageContainer size="md">
            <EmptyState
                icon={Clapperboard}
                title="Log in to see your recommendations"
                description="We'll suggest movies based on your purchase history."
            >
                <Button asChild><Link to="/login">Log in</Link></Button>
            </EmptyState>
        </PageContainer>
    );

    if (loading) return <LoadingState label="Loading recommendations…" />;

    const hasCategory      = data?.categoryBased?.length > 0;
    const hasCollaborative = data?.collaborativeBased?.length > 0;

    if (!hasCategory && !hasCollaborative) return (
        <PageContainer size="md">
            <EmptyState
                icon={ShoppingBag}
                title="No recommendations yet"
                description="Purchase some movies and we'll start suggesting titles you'll love."
            >
                <Button variant="outline" asChild><Link to="/">Browse movies</Link></Button>
            </EmptyState>
        </PageContainer>
    );

    const openAiChat = () => {
        const shown = [
            ...(data?.categoryBased || []),
            ...(data?.collaborativeBased || [])
        ].map(m => m.title);

        const message = shown.length > 0
            ? `I already see these movies recommended on my recommendations page: ${shown.join(', ')}. What other movies would you suggest for me that are different from these?`
            : 'What movies would you recommend for me based on my purchase history?';

        globalThis.dispatchEvent(new CustomEvent('chatbot:open', { detail: { message } }));
    };

    return (
        <PageContainer size="xl">
            <PageHeader
                title="Your Recommendations"
                icon={Sparkles}
                description="Personalised picks based on your purchase history and customers with similar taste."
            >
                <Button variant="outline" onClick={openAiChat} title="Ask the AI chatbot for more personalised suggestions">
                    <Bot className="text-primary" />
                    Ask AI for more suggestions
                </Button>
            </PageHeader>

            {hasCategory && (
                <section className="mb-10">
                    <h2 className="mb-2 flex flex-wrap items-center gap-2 text-xl font-semibold">
                        <Sparkles className="size-5 text-primary" />
                        Recommended for You
                        <Badge variant="warning">Category match</Badge>
                    </h2>
                    <HorizontalRow
                        movies={data.categoryBased}
                        badge={<><Sparkles className="size-3" />Recommended</>}
                        badgeClass="bg-primary text-primary-foreground"
                    />
                </section>
            )}

            {hasCollaborative && (
                <section className="mb-10">
                    <h2 className="mb-2 flex flex-wrap items-center gap-2 text-xl font-semibold">
                        <Users className="size-5 text-sky-400" />
                        Customers Also Bought
                        <Badge className="border-transparent bg-sky-500/15 text-sky-400">Similar taste</Badge>
                    </h2>
                    <HorizontalRow
                        movies={data.collaborativeBased}
                        badge={<><Users className="size-3" />Popular pick</>}
                        badgeClass="bg-sky-500 text-white"
                    />
                </section>
            )}
        </PageContainer>
    );
}
