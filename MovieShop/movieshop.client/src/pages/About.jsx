import { Link } from 'react-router-dom';
import {
    Bot, Clapperboard, Code2, Database, Gavel, Home, Info, Library, Mail, MessageSquareText, Paintbrush, Server,
    ShieldCheck, Target, Zap,
} from 'lucide-react';
import { Button } from '@/components/ui/button';

const features = [
    {
        icon: Library,
        title: 'Vast Collection',
        text: 'Browse through thousands of movies across all genres, from classics to the latest releases.',
    },
    {
        icon: ShieldCheck,
        title: 'Secure Shopping',
        text: 'Shop with confidence using our secure payment system and encrypted transactions.',
    },
    {
        icon: MessageSquareText,
        title: 'Community Reviews',
        text: 'Read authentic reviews from fellow movie lovers and share your own experiences.',
    },
    {
        icon: Zap,
        title: 'Instant Streaming',
        text: 'Start watching right after purchase with adaptive HLS streaming — or together in a Watch Party.',
    },
];

const techStack = [
    { icon: Code2, label: 'React' },
    { icon: Server, label: 'ASP.NET Core' },
    { icon: Database, label: 'SQL Server' },
    { icon: Paintbrush, label: 'Tailwind CSS + shadcn/ui' },
];

const About = () => {
    return (
        <div>
            {/* Hero Section */}
            <section className="relative overflow-hidden border-b">
                <div className="pointer-events-none absolute inset-0" aria-hidden="true">
                    <div className="absolute -top-32 left-1/2 size-[40rem] -translate-x-1/2 rounded-full bg-primary/15 blur-3xl" />
                </div>
                <div className="relative mx-auto max-w-3xl px-4 py-20 text-center sm:px-6">
                    <div className="mx-auto mb-6 flex size-16 items-center justify-center rounded-2xl bg-primary text-primary-foreground shadow-xl shadow-primary/30">
                        <Clapperboard className="size-8" />
                    </div>
                    <h1 className="font-display text-6xl leading-none tracking-wide sm:text-7xl">
                        About Movie<span className="text-primary">WebShop</span>
                    </h1>
                    <p className="mt-5 text-lg text-muted-foreground">
                        Your ultimate destination for discovering, purchasing, and enjoying the best movies from around the world.
                    </p>
                </div>
            </section>

            <div className="mx-auto max-w-6xl space-y-20 px-4 py-16 sm:px-6 lg:px-8">
                {/* Main Content */}
                <div className="grid gap-6 lg:grid-cols-2">
                    <div className="rounded-2xl border bg-card p-8">
                        <div className="mb-4 flex items-center gap-3">
                            <div className="flex size-11 items-center justify-center rounded-xl bg-primary/15 text-primary">
                                <Info className="size-5" />
                            </div>
                            <h2 className="text-2xl font-bold">What We Are</h2>
                        </div>
                        <div className="space-y-3 leading-relaxed text-muted-foreground">
                            <p>
                                MovieWebshop is a modern e-commerce platform dedicated to movie enthusiasts.
                                We provide a seamless shopping experience where you can browse, discover, and
                                purchase your favorite movies from various genres and eras.
                            </p>
                            <p>
                                Built with cutting-edge technology using React and ASP.NET Core, we ensure
                                a fast, secure, and user-friendly experience for all our customers.
                            </p>
                        </div>
                    </div>

                    <div className="rounded-2xl border bg-card p-8">
                        <div className="mb-4 flex items-center gap-3">
                            <div className="flex size-11 items-center justify-center rounded-xl bg-success/15 text-success">
                                <Target className="size-5" />
                            </div>
                            <h2 className="text-2xl font-bold">Our Mission</h2>
                        </div>
                        <div className="space-y-3 leading-relaxed text-muted-foreground">
                            <p>
                                Our mission is to make great movies accessible to everyone. We believe that
                                cinema has the power to inspire, entertain, and bring people together.
                            </p>
                            <p>
                                We strive to curate the finest collection of movies and provide exceptional
                                customer service to ensure every purchase enhances your movie-watching experience.
                            </p>
                        </div>
                    </div>
                </div>

                {/* Features Grid */}
                <section>
                    <div className="mb-10 text-center">
                        <h2 className="text-3xl font-bold tracking-tight">Why Choose MovieWebshop?</h2>
                        <p className="mt-2 text-muted-foreground">Discover what makes us the perfect choice for movie lovers</p>
                    </div>

                    <div className="grid gap-5 sm:grid-cols-2 lg:grid-cols-4">
                        {features.map(({ icon: Icon, title, text }) => (
                            <div
                                key={title}
                                className="group rounded-2xl border bg-card p-6 transition-all hover:-translate-y-1 hover:border-primary/40"
                            >
                                <div className="mb-4 flex size-12 items-center justify-center rounded-xl bg-primary/10 text-primary transition-colors group-hover:bg-primary group-hover:text-primary-foreground">
                                    <Icon className="size-6" />
                                </div>
                                <h3 className="mb-2 font-semibold">{title}</h3>
                                <p className="text-sm leading-relaxed text-muted-foreground">{text}</p>
                            </div>
                        ))}
                    </div>

                    <div className="mt-5 grid gap-5 sm:grid-cols-2">
                        <div className="flex items-center gap-4 rounded-2xl border bg-card p-5">
                            <Gavel className="size-6 shrink-0 text-primary" />
                            <p className="text-sm text-muted-foreground"><strong className="text-foreground">Live auctions</strong> with real-time bidding and anti-sniping protection.</p>
                        </div>
                        <div className="flex items-center gap-4 rounded-2xl border bg-card p-5">
                            <Bot className="size-6 shrink-0 text-primary" />
                            <p className="text-sm text-muted-foreground"><strong className="text-foreground">AI assistant</strong> that answers questions and performs actions for you.</p>
                        </div>
                    </div>
                </section>

                {/* Technology Stack */}
                <section className="relative overflow-hidden rounded-3xl border bg-gradient-to-br from-primary/20 via-card to-card p-10 text-center">
                    <h2 className="text-3xl font-bold tracking-tight">Built with Modern Technology</h2>
                    <p className="mx-auto mt-3 max-w-xl text-muted-foreground">
                        MovieWebshop is powered by cutting-edge web technologies to ensure the best user experience.
                    </p>

                    <div className="mt-8 flex flex-wrap justify-center gap-3">
                        {techStack.map(({ icon: Icon, label }) => (
                            <div key={label} className="flex items-center gap-2 rounded-full border bg-background/60 px-5 py-2 font-medium backdrop-blur">
                                <Icon className="size-4 text-primary" />
                                {label}
                            </div>
                        ))}
                    </div>
                </section>

                {/* Call to Action */}
                <section className="text-center">
                    <h2 className="text-3xl font-bold tracking-tight">Ready to Start Your Movie Journey?</h2>
                    <p className="mx-auto mt-3 max-w-xl text-muted-foreground">
                        Join thousands of movie enthusiasts who have made MovieWebshop their go-to destination for cinema.
                    </p>
                    <div className="mt-8 flex flex-col justify-center gap-3 sm:flex-row">
                        <Button size="lg" className="h-12 px-8" asChild>
                            <Link to="/">
                                <Home />
                                Browse Movies
                            </Link>
                        </Button>
                        <Button size="lg" variant="outline" className="h-12 px-8" asChild>
                            <a href="mailto:support@movieshop.com">
                                <Mail />
                                Contact Us
                            </a>
                        </Button>
                    </div>
                </section>
            </div>
        </div>
    );
};

export default About;
