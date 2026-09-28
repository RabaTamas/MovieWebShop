import { useEffect, useState, useRef, useCallback, useMemo } from "react";
import { Link } from "react-router-dom";
import { AnimatePresence, LayoutGroup, animate, motion, useInView } from "motion/react";
import {
    ArrowRight, ArrowUpDown, Check, Clapperboard, Grid3x3, LayoutGrid, RotateCcw, Search, SlidersHorizontal,
    Sparkles, Tags, Wallet, X, Zap,
} from "lucide-react";
import MovieCard from "../components/MovieCard";
import Pagination from "../components/Pagination";
import API_BASE_URL from "../config/api";
import { useAuth } from "../contexts/AuthContext";
import { cn, formatPrice } from "@/lib/utils";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { Slider } from "@/components/ui/slider";
import { Alert, AlertDescription } from "@/components/ui/alert";
import {
    DropdownMenu, DropdownMenuCheckboxItem, DropdownMenuContent, DropdownMenuLabel, DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
    Sheet, SheetContent, SheetDescription, SheetFooter, SheetHeader, SheetTitle,
} from "@/components/ui/sheet";
import PosterWall from "../components/home/PosterWall";
import OfflineState from "../components/pwa/OfflineState";
import PushPrompt from "../components/pwa/PushPrompt";
import useOnlineStatus from "@/hooks/useOnlineStatus";

const HEADLINE = [
    [{ word: "Your" }, { word: "next" }, { word: "favourite" }, { word: "movie", accent: true }],
    [{ word: "is" }, { word: "one" }, { word: "click" }, { word: "away" }],
];

const SORT_OPTIONS = [
    { value: "", label: "Default order" },
    { value: "name-asc", label: "Name (A-Z)" },
    { value: "name-desc", label: "Name (Z-A)" },
    { value: "price-asc", label: "Price (Low → High)" },
    { value: "price-desc", label: "Price (High → Low)" },
    { value: "rating-desc", label: "Rating (Best first)", searchOnly: true },
];

const DENSITIES = [
    { value: "comfortable", label: "Large posters", icon: LayoutGrid },
    { value: "compact", label: "Compact grid", icon: Grid3x3 },
];

const PRICE_BINS = 16;
const EASE = [0.22, 1, 0.36, 1];

/** Felpörgő szám (találatszám a fejlécben). */
function AnimatedNumber({ value }) {
    const ref = useRef(null);
    const inView = useInView(ref, { once: true });
    const previous = useRef(0);
    const [display, setDisplay] = useState(0);

    useEffect(() => {
        if (!inView) return;
        const controls = animate(previous.current, value, {
            duration: 0.6,
            ease: EASE,
            onUpdate: (v) => setDisplay(Math.round(v)),
        });
        previous.current = value;
        return () => controls.stop();
    }, [value, inView]);

    return <span ref={ref} className="tabular-nums">{display}</span>;
}

/** Animált „nincs találat" grafika: forgó filmtekercs és pásztázó nagyító. */
function NoResultsGraphic() {
    return (
        <div className="relative mx-auto mb-2 size-28" aria-hidden="true">
            <motion.svg
                viewBox="0 0 100 100"
                className="size-full text-muted-foreground/40"
                animate={{ rotate: 360 }}
                transition={{ duration: 18, repeat: Infinity, ease: "linear" }}
            >
                <circle cx="50" cy="50" r="46" fill="none" stroke="currentColor" strokeWidth="4" />
                <circle cx="50" cy="50" r="9" fill="currentColor" />
                {[0, 60, 120, 180, 240, 300].map((deg) => (
                    <circle
                        key={deg}
                        cx={50 + 26 * Math.cos((deg * Math.PI) / 180)}
                        cy={50 + 26 * Math.sin((deg * Math.PI) / 180)}
                        r="10"
                        fill="currentColor"
                        opacity="0.75"
                    />
                ))}
            </motion.svg>
            <motion.div
                className="absolute -right-3 -bottom-2 flex size-12 items-center justify-center rounded-full border bg-background text-primary shadow-lg"
                animate={{ x: [0, -18, 6, 0], y: [0, -10, -16, 0], rotate: [0, -12, 8, 0] }}
                transition={{ duration: 4, repeat: Infinity, ease: "easeInOut" }}
            >
                <Search className="size-5" />
            </motion.div>
        </div>
    );
}

const Home = () => {
    const { token } = useAuth();
    const online = useOnlineStatus();

    // ── recommendations ─────────────────────────────────────────────
    const [recommendations, setRecommendations] = useState(null);

    // ── browse (no search) ──────────────────────────────────────────
    const [movies, setMovies] = useState([]);
    const [moviesLoading, setMoviesLoading] = useState(true);
    const [categories, setCategories] = useState([]);
    const [categoriesLoading, setCategoriesLoading] = useState(true);
    const [selectedCategories, setSelectedCategories] = useState([]);
    const [sort, setSort] = useState("");
    const [minPrice, setMinPrice] = useState("");
    const [maxPrice, setMaxPrice] = useState("");

    // ── elasticsearch search ────────────────────────────────────────
    const [search, setSearch] = useState("");
    const [searchResults, setSearchResults] = useState(null);   // null = show browse
    const [searchLoading, setSearchLoading] = useState(false);
    const [suggestions, setSuggestions] = useState([]);
    const [showSuggestions, setShowSuggestions] = useState(false);
    const [searchPage, setSearchPage] = useState(1);
    const searchInputRef = useRef(null);
    const debounceRef = useRef(null);
    const ITEMS_PER_PAGE = 8;

    // ── pagination (browse mode) ────────────────────────────────────
    const [currentPage, setCurrentPage] = useState(1);

    // ── presentation-only state ─────────────────────────────────────
    const [filtersOpen, setFiltersOpen] = useState(false);
    const [density, setDensity] = useState(() => {
        try {
            return localStorage.getItem("movieshop-grid-density") === "compact" ? "compact" : "comfortable";
        } catch {
            return "comfortable";
        }
    });
    const resultsRef = useRef(null);

    useEffect(() => {
        try {
            localStorage.setItem("movieshop-grid-density", density);
        } catch {
            /* privát mód */
        }
    }, [density]);

    const filteredMovies = movies.filter(m => {
        const price = m.discountedPrice ?? m.price;
        if (minPrice !== "" && price < parseFloat(minPrice)) return false;
        if (maxPrice !== "" && price > parseFloat(maxPrice)) return false;
        return true;
    }).sort((a, b) => {
        if (sort === "name-asc") return a.title.localeCompare(b.title);
        if (sort === "name-desc") return b.title.localeCompare(a.title);
        const pa = a.discountedPrice ?? a.price, pb = b.discountedPrice ?? b.price;
        if (sort === "price-asc") return pa - pb;
        if (sort === "price-desc") return pb - pa;
        return 0;
    });

    const browseTotalPages = Math.ceil(filteredMovies.length / ITEMS_PER_PAGE);
    const paginatedMovies = filteredMovies.slice(
        (currentPage - 1) * ITEMS_PER_PAGE,
        currentPage * ITEMS_PER_PAGE
    );

    // ── fetch recommendations (logged-in users only) ────────────────
    useEffect(() => {
        if (!token) { setRecommendations(null); return; }
        const load = async () => {
            try {
                const res = await fetch(`${API_BASE_URL}/api/Recommendation`, {
                    headers: { Authorization: `Bearer ${token}` }
                });
                if (res.ok) setRecommendations(await res.json());
            } catch { /* silent fail */ }
        };
        load();
    }, [token]);

    // ── fetch categories ────────────────────────────────────────────
    useEffect(() => {
        const load = async () => {
            setCategoriesLoading(true);
            try {
                const res = await fetch(`${API_BASE_URL}/api/Category`);
                setCategories(res.ok ? await res.json() : []);
            } catch { setCategories([]); }
            finally { setCategoriesLoading(false); }
        };
        load();
    }, []);

    // ── fetch movies (browse mode) ──────────────────────────────────
    useEffect(() => {
        if (search.trim()) return;          // ES mode active – skip DB fetch
        const load = async () => {
            setMoviesLoading(true);
            try {
                let url = selectedCategories.length === 0
                    ? `${API_BASE_URL}/api/Movie`
                    : `${API_BASE_URL}/api/Movie/categories?${selectedCategories.map(id => `categoryIds=${id}`).join("&")}`;
                const res = await fetch(url);
                setMovies(res.ok ? await res.json() : []);
            } catch { setMovies([]); }
            finally { setMoviesLoading(false); }
        };
        load();
    }, [selectedCategories, search]);

    // ── elasticsearch search (debounced) ───────────────────────────
    const runSearch = useCallback(async (q, page = 1) => {
        if (!q.trim()) { setSearchResults(null); return; }
        setSearchLoading(true);
        try {
            const params = new URLSearchParams({ q, page, size: ITEMS_PER_PAGE, ...(sort && { sort }) });
            selectedCategories.forEach(id => {
                const name = categories.find(c => c.id === id)?.name;
                if (name) params.append("categories", name);
            });
            if (minPrice) params.set("minPrice", minPrice);
            if (maxPrice) params.set("maxPrice", maxPrice);

            const res = await fetch(`${API_BASE_URL}/api/Search?${params}`);
            if (res.ok) setSearchResults(await res.json());
        } catch { setSearchResults(null); }
        finally { setSearchLoading(false); }
    }, [sort, selectedCategories, categories, minPrice, maxPrice]);

    useEffect(() => {
        clearTimeout(debounceRef.current);
        if (!search.trim()) { setSearchResults(null); setSuggestions([]); return; }
        debounceRef.current = setTimeout(() => {
            runSearch(search, searchPage);
            fetchSuggestions(search);
        }, 350);
    }, [search, searchPage, runSearch]);

    // Re-run search when filters/sort change (ES mode)
    useEffect(() => {
        if (search.trim()) runSearch(search, searchPage);
    }, [sort, minPrice, maxPrice, selectedCategories]);

    const fetchSuggestions = async (q) => {
        try {
            const res = await fetch(`${API_BASE_URL}/api/Search/autocomplete?q=${encodeURIComponent(q)}`);
            setSuggestions(res.ok ? await res.json() : []);
        } catch { setSuggestions([]); }
    };

    const handleSearchChange = (e) => {
        setSearch(e.target.value);
        setSearchPage(1);
        setShowSuggestions(true);
    };

    const applySuggestion = (s) => {
        setSearch(s);
        setShowSuggestions(false);
        searchInputRef.current?.blur();
    };

    const toggleCategory = (id) => {
        setSelectedCategories(prev =>
            prev.includes(id) ? prev.filter(c => c !== id) : [...prev, id]
        );
        setCurrentPage(1);
        setSearchPage(1);
    };

    const resetPages = () => { setCurrentPage(1); setSearchPage(1); };

    const isSearchMode = !!search.trim();
    const isLoading = isSearchMode ? searchLoading : moviesLoading;

    const displayMovies = isSearchMode
        ? (searchResults?.movies ?? [])
        : paginatedMovies;

    const searchTotalPages = searchResults
        ? Math.ceil(searchResults.total / ITEMS_PER_PAGE)
        : 0;

    // ── presentation helpers (derived, no new data fetching) ────────
    const resultCount = isSearchMode ? (searchResults?.total ?? 0) : filteredMovies.length;

    // Ár-eloszlás a hisztogramhoz és a csúszka határaihoz (a betöltött filmek effektív ára alapján)
    const priceStats = useMemo(() => {
        const prices = movies.map(m => m.discountedPrice ?? m.price).filter(p => typeof p === "number");
        if (prices.length === 0) return null;
        const min = Math.floor(Math.min(...prices) / 100) * 100;
        let max = Math.ceil(Math.max(...prices) / 100) * 100;
        if (max === min) max = min + 100;
        const bins = Array(PRICE_BINS).fill(0);
        prices.forEach(p => {
            bins[Math.min(PRICE_BINS - 1, Math.floor(((p - min) / (max - min)) * PRICE_BINS))]++;
        });
        return { min, max, bins, peak: Math.max(...bins) };
    }, [movies]);

    const priceRange = priceStats
        ? [
            minPrice === "" ? priceStats.min : Math.max(priceStats.min, Math.min(Number(minPrice), priceStats.max)),
            maxPrice === "" ? priceStats.max : Math.min(priceStats.max, Math.max(Number(maxPrice), priceStats.min)),
        ]
        : null;

    const handlePriceRange = ([low, high]) => {
        setMinPrice(low <= priceStats.min ? "" : String(low));
        setMaxPrice(high >= priceStats.max ? "" : String(high));
        resetPages();
    };

    const activeFilters = [
        ...selectedCategories.map(id => ({
            key: `category-${id}`,
            label: categories.find(c => c.id === id)?.name ?? `#${id}`,
            onRemove: () => toggleCategory(id),
        })),
        ...(minPrice !== "" || maxPrice !== ""
            ? [{
                key: "price",
                label: `${minPrice !== "" ? formatPrice(Number(minPrice)) : "Any"} – ${maxPrice !== "" ? formatPrice(Number(maxPrice)) : "Any"}`,
                onRemove: () => { setMinPrice(""); setMaxPrice(""); resetPages(); },
            }]
            : []),
    ];

    const clearFilters = () => {
        setSelectedCategories([]);
        setMinPrice("");
        setMaxPrice("");
        setSort("");
        resetPages();
    };

    const sortOptions = SORT_OPTIONS.filter(o => !o.searchOnly || isSearchMode);
    const currentSortLabel = SORT_OPTIONS.find(o => o.value === sort)?.label ?? "Default order";

    // Lapváltáskor a találatok elejére görgetünk
    const changePage = (setter) => (page) => {
        setter(page);
        resultsRef.current?.scrollIntoView({ behavior: "smooth", block: "start" });
    };

    const gridClass = density === "compact"
        ? "grid grid-cols-3 gap-x-3 gap-y-6 sm:grid-cols-4 lg:grid-cols-5 2xl:grid-cols-6"
        : "grid grid-cols-2 gap-x-4 gap-y-8 sm:grid-cols-3 xl:grid-cols-4";

    const renderFilterSections = () => (
        <>
            <div className="space-y-3">
                <h3 className="flex items-center gap-2 text-xs font-semibold tracking-wider text-muted-foreground uppercase">
                    <Tags className="size-4" /> Category
                </h3>
                <LayoutGroup id="category-chips">
                    <div className="flex flex-wrap gap-2">
                        {categoriesLoading
                            ? Array.from({ length: 6 }).map((_, i) => <Skeleton key={i} className="h-8 w-20 rounded-full" />)
                            : categories.map(cat => {
                                const active = selectedCategories.includes(cat.id);
                                return (
                                    <motion.button
                                        key={cat.id}
                                        layout
                                        type="button"
                                        role="checkbox"
                                        aria-checked={active}
                                        onClick={() => toggleCategory(cat.id)}
                                        whileTap={{ scale: 0.94 }}
                                        className={cn(
                                            "relative inline-flex cursor-pointer items-center gap-1.5 overflow-hidden rounded-full border px-3 py-1.5 text-sm transition-colors",
                                            active
                                                ? "border-primary text-primary-foreground"
                                                : "bg-background/40 text-muted-foreground hover:border-primary/50 hover:text-foreground"
                                        )}
                                    >
                                        <AnimatePresence>
                                            {active && (
                                                <motion.span
                                                    className="absolute inset-0 bg-primary"
                                                    initial={{ scale: 0, opacity: 0 }}
                                                    animate={{ scale: 1, opacity: 1 }}
                                                    exit={{ scale: 0, opacity: 0 }}
                                                    transition={{ duration: 0.25, ease: EASE }}
                                                    style={{ borderRadius: 9999 }}
                                                />
                                            )}
                                        </AnimatePresence>
                                        <AnimatePresence initial={false}>
                                            {active && (
                                                <motion.span
                                                    className="relative"
                                                    initial={{ width: 0, opacity: 0 }}
                                                    animate={{ width: "auto", opacity: 1 }}
                                                    exit={{ width: 0, opacity: 0 }}
                                                >
                                                    <Check className="size-3.5" />
                                                </motion.span>
                                            )}
                                        </AnimatePresence>
                                        <span className="relative">{cat.name}</span>
                                    </motion.button>
                                );
                            })}
                    </div>
                </LayoutGroup>
            </div>

            <div className="space-y-3">
                <div className="flex items-baseline justify-between gap-2">
                    <h3 className="flex items-center gap-2 text-xs font-semibold tracking-wider text-muted-foreground uppercase">
                        <Wallet className="size-4" /> Price
                    </h3>
                    {priceRange && (
                        <span className="text-xs font-medium tabular-nums">
                            {formatPrice(priceRange[0])} – {formatPrice(priceRange[1])}
                        </span>
                    )}
                </div>

                {priceStats && priceRange && (
                    <div>
                        {/* Ár-eloszlás hisztogram: a kijelölt tartományba eső sávok kiemelve */}
                        <div className="flex h-14 items-end gap-[3px] px-2" aria-hidden="true">
                            {priceStats.bins.map((count, i) => {
                                const width = (priceStats.max - priceStats.min) / PRICE_BINS;
                                const binStart = priceStats.min + i * width;
                                const binEnd = binStart + width;
                                const inRange = binEnd > priceRange[0] && binStart <= priceRange[1];
                                const height = count === 0 ? 6 : Math.max(18, (count / priceStats.peak) * 100);
                                return (
                                    <motion.div
                                        key={i}
                                        className={cn(
                                            "flex-1 rounded-t-[3px] transition-colors duration-300",
                                            count === 0 ? "bg-muted/60" : inRange ? "bg-primary/85" : "bg-muted"
                                        )}
                                        initial={{ height: 0 }}
                                        animate={{ height: `${height}%` }}
                                        transition={{ duration: 0.6, delay: i * 0.025, ease: EASE }}
                                    />
                                );
                            })}
                        </div>
                        <Slider
                            min={priceStats.min}
                            max={priceStats.max}
                            step={50}
                            minStepsBetweenThumbs={1}
                            value={priceRange}
                            onValueChange={handlePriceRange}
                            thumbLabels={["Minimum price", "Maximum price"]}
                        />
                    </div>
                )}

                <div className="flex items-center gap-2">
                    <Input type="number" placeholder="Min" aria-label="Min price" value={minPrice} className="h-8"
                        onChange={e => { setMinPrice(e.target.value); resetPages(); }} />
                    <span className="text-muted-foreground">–</span>
                    <Input type="number" placeholder="Max" aria-label="Max price" value={maxPrice} className="h-8"
                        onChange={e => { setMaxPrice(e.target.value); resetPages(); }} />
                </div>
            </div>

            {isSearchMode && searchResults && (
                <Alert variant="info">
                    <Zap />
                    <AlertDescription>
                        <span>
                            <strong className="text-foreground">{searchResults.total}</strong> result{searchResults.total !== 1 ? "s" : ""} found
                        </span>
                    </AlertDescription>
                </Alert>
            )}
        </>
    );

    return (
        <div>
            {/* ── Hero ─────────────────────────────────── */}
            <section className="relative border-b">
                <div className="film-grain pointer-events-none absolute inset-0 overflow-hidden" aria-hidden="true">
                    <div className="absolute -top-40 -left-20 size-[36rem] animate-aurora rounded-full bg-primary/20 blur-3xl" />
                    <div className="absolute -right-32 -bottom-48 size-[30rem] animate-aurora rounded-full bg-destructive/10 blur-3xl [animation-delay:-8s]" />
                    {/* Pásztázó reflektorfény-kúp */}
                    <div className="absolute -top-24 left-[18%] h-[160%] w-[34rem] origin-top animate-spotlight bg-[conic-gradient(from_180deg_at_50%_0%,transparent_42%,color-mix(in_oklch,var(--primary)_16%,transparent)_50%,transparent_58%)] blur-2xl" />
                </div>
                <PosterWall movies={movies} />
                <div className="relative mx-auto max-w-7xl px-4 py-12 sm:px-6 sm:py-20 lg:px-8">
                    <motion.p
                        initial={{ opacity: 0, y: -10 }}
                        animate={{ opacity: 1, y: 0 }}
                        transition={{ duration: 0.6 }}
                        className="mb-4 inline-flex items-center gap-2 rounded-full border border-primary/30 bg-primary/10 px-3 py-1 text-xs font-medium text-primary"
                    >
                        <Clapperboard className="size-3.5" /> Buy once, stream anytime
                    </motion.p>
                    {/* A címsor szavai egyenként, elmosódásból élesedve úsznak be */}
                    <motion.h1
                        className="font-display text-5xl leading-[0.95] tracking-wide sm:text-7xl"
                        initial="hidden"
                        animate="show"
                        variants={{ hidden: {}, show: { transition: { staggerChildren: 0.08, delayChildren: 0.1 } } }}
                    >
                        {HEADLINE.map((line, li) => (
                            <span key={li} className="block">
                                {line.map(({ word, accent }) => (
                                    <motion.span
                                        key={word}
                                        className={cn("mr-[0.22em] inline-block", accent && "text-primary drop-shadow-[0_0_28px_color-mix(in_oklch,var(--primary)_55%,transparent)]")}
                                        variants={{
                                            hidden: { opacity: 0, y: 40, rotateX: -70, filter: "blur(8px)" },
                                            show: { opacity: 1, y: 0, rotateX: 0, filter: "blur(0px)", transition: { duration: 0.8, ease: EASE } },
                                        }}
                                    >
                                        {word}
                                    </motion.span>
                                ))}
                            </span>
                        ))}
                    </motion.h1>
                    <motion.p
                        initial={{ opacity: 0, y: 16 }}
                        animate={{ opacity: 1, y: 0 }}
                        transition={{ duration: 0.7, delay: 0.7 }}
                        className="mt-4 max-w-xl text-muted-foreground sm:text-lg"
                    >
                        Browse the catalogue, bid in live auctions and watch together with friends in Watch Party.
                    </motion.p>

                    {/* Search with autocomplete */}
                    <div className="relative mt-8 max-w-2xl">
                        <Search className="pointer-events-none absolute top-1/2 left-4 z-10 size-5 -translate-y-1/2 text-muted-foreground" />
                        <Input
                            ref={searchInputRef}
                            type="text"
                            className="h-14 rounded-xl bg-card/80 pr-12 pl-12 text-base shadow-lg backdrop-blur md:text-base dark:bg-card/80"
                            placeholder="Search movies..."
                            aria-label="Search"
                            value={search}
                            onChange={handleSearchChange}
                            onFocus={() => setShowSuggestions(true)}
                            onBlur={() => setTimeout(() => setShowSuggestions(false), 150)}
                        />
                        {search && (
                            <Button
                                variant="ghost"
                                size="icon-sm"
                                className="absolute top-1/2 right-3 z-10 -translate-y-1/2 text-muted-foreground"
                                aria-label="Clear search"
                                onClick={() => { setSearch(""); setSearchResults(null); setSuggestions([]); }}
                            >
                                <X />
                            </Button>
                        )}
                        {showSuggestions && suggestions.length > 0 && (
                            <ul className="absolute top-full z-30 mt-2 w-full overflow-hidden rounded-xl border bg-popover py-1 shadow-2xl">
                                {suggestions.map((s, i) => (
                                    <li
                                        key={i}
                                        className="flex cursor-pointer items-center gap-3 px-4 py-2 text-sm transition-colors hover:bg-accent hover:text-accent-foreground"
                                        onMouseDown={() => applySuggestion(s)}
                                    >
                                        <Search className="size-3.5 text-muted-foreground" />
                                        {s}
                                    </li>
                                ))}
                            </ul>
                        )}
                    </div>
                </div>
            </section>

            <div className="mx-auto grid max-w-7xl gap-8 px-4 py-8 sm:px-6 lg:grid-cols-[280px_1fr] lg:px-8">
                {/* ── Szűrőpanel (asztali) ─────────────────── */}
                <aside className="hidden lg:block">
                    <div className="sticky top-24 space-y-7 rounded-2xl border bg-card/50 p-5 shadow-xl backdrop-blur-xl">
                        <div className="flex items-center justify-between">
                            <h2 className="flex items-center gap-2 font-semibold">
                                <SlidersHorizontal className="size-4 text-primary" /> Filters
                            </h2>
                            <AnimatePresence>
                                {(activeFilters.length > 0 || sort) && (
                                    <motion.div initial={{ opacity: 0, x: 8 }} animate={{ opacity: 1, x: 0 }} exit={{ opacity: 0, x: 8 }}>
                                        <Button variant="ghost" size="sm" className="h-7 text-muted-foreground" onClick={clearFilters}>
                                            <RotateCcw /> Reset
                                        </Button>
                                    </motion.div>
                                )}
                            </AnimatePresence>
                        </div>
                        {renderFilterSections()}
                    </div>
                </aside>

                {/* ── Találatok ───────────────────────────── */}
                <section ref={resultsRef} className="min-w-0 scroll-mt-20">
                    <PushPrompt className="mb-6" />

                    {/* ── Recommendations teaser ──────────── */}
                    {!isSearchMode && recommendations &&
                        (recommendations.categoryBased?.length > 0 || recommendations.collaborativeBased?.length > 0) && (
                        <div className="mb-6 flex flex-col gap-3 rounded-xl border border-primary/30 bg-gradient-to-r from-primary/15 via-primary/5 to-transparent p-4 sm:flex-row sm:items-center sm:justify-between">
                            <span className="flex items-center gap-3">
                                <span className="flex size-9 shrink-0 items-center justify-center rounded-full bg-primary/20 text-primary">
                                    <Sparkles className="size-4" />
                                </span>
                                <span>We have <strong>personalised recommendations</strong> for you!</span>
                            </span>
                            <Button size="sm" asChild>
                                <Link to="/recommendations">View all <ArrowRight /></Link>
                            </Button>
                        </div>
                    )}

                    {/* ── Ragadós eszköztár ───────────────── */}
                    <div className="sticky top-16 z-20 -mx-4 mb-6 border-b bg-background/80 px-4 py-3 backdrop-blur-xl sm:mx-0 sm:rounded-2xl sm:border sm:px-4 sm:shadow-lg">
                        <div className="flex flex-wrap items-center gap-2 sm:gap-3">
                            <div className="flex min-w-0 flex-1 items-baseline gap-3">
                                <h2 className="truncate text-lg font-semibold sm:text-xl">
                                    {isSearchMode ? <>Results for “<span className="text-primary">{search}</span>”</> : "All movies"}
                                </h2>
                                {!isLoading && (
                                    <span className="shrink-0 text-sm text-muted-foreground">
                                        <AnimatedNumber value={resultCount} /> {resultCount === 1 ? "movie" : "movies"}
                                    </span>
                                )}
                            </div>

                            <Button variant="outline" size="sm" className="lg:hidden" onClick={() => setFiltersOpen(true)}>
                                <SlidersHorizontal />
                                Filters
                                {activeFilters.length > 0 && (
                                    <span className="flex size-5 items-center justify-center rounded-full bg-primary text-[10px] font-bold text-primary-foreground">
                                        {activeFilters.length}
                                    </span>
                                )}
                            </Button>

                            <DropdownMenu modal={false}>
                                <DropdownMenuTrigger asChild>
                                    <Button variant="outline" size="sm" aria-label="Sort">
                                        <ArrowUpDown />
                                        <span className="hidden sm:inline">{currentSortLabel}</span>
                                    </Button>
                                </DropdownMenuTrigger>
                                <DropdownMenuContent align="end" className="w-52">
                                    <DropdownMenuLabel>Sort by</DropdownMenuLabel>
                                    {sortOptions.map(option => (
                                        <DropdownMenuCheckboxItem
                                            key={option.value || "default"}
                                            checked={sort === option.value}
                                            onCheckedChange={() => { setSort(option.value); resetPages(); }}
                                        >
                                            {option.label}
                                        </DropdownMenuCheckboxItem>
                                    ))}
                                </DropdownMenuContent>
                            </DropdownMenu>

                            <div className="hidden items-center rounded-lg border p-0.5 sm:inline-flex" role="group" aria-label="Grid density">
                                {DENSITIES.map(({ value, label, icon: Icon }) => (
                                    <button
                                        key={value}
                                        type="button"
                                        title={label}
                                        aria-label={label}
                                        aria-pressed={density === value}
                                        onClick={() => setDensity(value)}
                                        className="relative flex size-7 cursor-pointer items-center justify-center rounded-md text-muted-foreground transition-colors hover:text-foreground"
                                    >
                                        {density === value && (
                                            <motion.span
                                                layoutId="density-pill"
                                                className="absolute inset-0 rounded-md bg-primary/15 ring-1 ring-primary/40"
                                                transition={{ type: "spring", stiffness: 420, damping: 32 }}
                                            />
                                        )}
                                        <Icon className={cn("relative size-4", density === value && "text-primary")} />
                                    </button>
                                ))}
                            </div>
                        </div>

                        {/* Aktív szűrők chipként, egyenként eltávolíthatók */}
                        <AnimatePresence initial={false}>
                            {activeFilters.length > 0 && (
                                <motion.div
                                    initial={{ height: 0, opacity: 0 }}
                                    animate={{ height: "auto", opacity: 1 }}
                                    exit={{ height: 0, opacity: 0 }}
                                    transition={{ duration: 0.25, ease: EASE }}
                                    className="overflow-hidden"
                                >
                                    <div className="flex flex-wrap items-center gap-2 pt-3">
                                        <AnimatePresence mode="popLayout">
                                            {activeFilters.map(filter => (
                                                <motion.button
                                                    key={filter.key}
                                                    layout
                                                    type="button"
                                                    onClick={filter.onRemove}
                                                    initial={{ opacity: 0, scale: 0.8 }}
                                                    animate={{ opacity: 1, scale: 1 }}
                                                    exit={{ opacity: 0, scale: 0.8 }}
                                                    className="group inline-flex cursor-pointer items-center gap-1.5 rounded-full border border-primary/40 bg-primary/10 py-1 pr-1.5 pl-3 text-xs font-medium text-foreground transition-colors hover:border-primary"
                                                    aria-label={`Remove filter ${filter.label}`}
                                                >
                                                    {filter.label}
                                                    <span className="flex size-4 items-center justify-center rounded-full bg-primary/20 text-primary transition-colors group-hover:bg-primary group-hover:text-primary-foreground">
                                                        <X className="size-3" />
                                                    </span>
                                                </motion.button>
                                            ))}
                                        </AnimatePresence>
                                        <button
                                            type="button"
                                            onClick={clearFilters}
                                            className="cursor-pointer text-xs font-medium text-muted-foreground underline-offset-4 hover:text-foreground hover:underline"
                                        >
                                            Clear all
                                        </button>
                                    </div>
                                </motion.div>
                            )}
                        </AnimatePresence>
                    </div>

                    {isLoading ? (
                        <div className={gridClass} aria-label={isSearchMode ? "Searching..." : "Loading movies..."}>
                            {Array.from({ length: ITEMS_PER_PAGE }).map((_, i) => (
                                <div key={i} className="space-y-3">
                                    <Skeleton className="aspect-[2/3] w-full rounded-xl" />
                                    <Skeleton className="h-4 w-3/4" />
                                    <Skeleton className="h-4 w-1/3" />
                                </div>
                            ))}
                        </div>
                    ) : displayMovies.length > 0 ? (
                        <LayoutGroup id="movie-grid">
                            <motion.div layout className={gridClass}>
                                <AnimatePresence mode="popLayout">
                                    {displayMovies.map((movie, i) => (
                                        <motion.div
                                            key={movie.id}
                                            layout
                                            initial={{ opacity: 0, y: 24, scale: 0.95 }}
                                            animate={{
                                                opacity: 1, y: 0, scale: 1,
                                                transition: { duration: 0.45, delay: Math.min(i, 12) * 0.04, ease: EASE },
                                            }}
                                            exit={{ opacity: 0, scale: 0.9, transition: { duration: 0.2 } }}
                                        >
                                            <MovieCard movie={movie} />
                                        </motion.div>
                                    ))}
                                </AnimatePresence>
                            </motion.div>
                        </LayoutGroup>
                    ) : !online ? (
                        <OfflineState />
                    ) : (
                        <motion.div
                            initial={{ opacity: 0, y: 12 }}
                            animate={{ opacity: 1, y: 0 }}
                            className="flex flex-col items-center gap-3 rounded-2xl border border-dashed px-6 py-14 text-center"
                        >
                            <NoResultsGraphic />
                            <h3 className="text-lg font-semibold">
                                {isSearchMode ? `No results for "${search}"` : "There are no movies matching your filters!"}
                            </h3>
                            <p className="max-w-sm text-sm text-muted-foreground">Try a different keyword or loosen the filters.</p>
                            {(activeFilters.length > 0 || sort) && (
                                <Button variant="outline" size="sm" className="mt-1" onClick={clearFilters}>
                                    <RotateCcw /> Reset filters
                                </Button>
                            )}
                        </motion.div>
                    )}

                    {!isLoading && !isSearchMode && filteredMovies.length > 0 && (
                        <Pagination
                            currentPage={currentPage}
                            totalPages={browseTotalPages}
                            onPageChange={changePage(setCurrentPage)}
                            totalItems={filteredMovies.length}
                            pageSize={ITEMS_PER_PAGE}
                        />
                    )}

                    {!isLoading && isSearchMode && searchTotalPages > 1 && (
                        <Pagination
                            currentPage={searchPage}
                            totalPages={searchTotalPages}
                            onPageChange={changePage(setSearchPage)}
                            totalItems={searchResults?.total}
                            pageSize={ITEMS_PER_PAGE}
                        />
                    )}
                </section>
            </div>

            {/* ── Szűrőpanel (mobil): alulról felcsúszó lap ── */}
            <Sheet open={filtersOpen} onOpenChange={setFiltersOpen}>
                <SheetContent side="bottom" className="max-h-[85vh] gap-0 rounded-t-3xl">
                    <div className="mx-auto mt-3 h-1.5 w-12 rounded-full bg-muted" aria-hidden="true" />
                    <SheetHeader className="border-b">
                        <SheetTitle className="flex items-center gap-2">
                            <SlidersHorizontal className="size-4 text-primary" /> Filters
                        </SheetTitle>
                        <SheetDescription>Narrow down the movie list.</SheetDescription>
                    </SheetHeader>
                    <div className="space-y-7 overflow-y-auto p-4">{renderFilterSections()}</div>
                    <SheetFooter className="flex-row gap-2 border-t">
                        <Button variant="outline" className="flex-1" onClick={clearFilters}>
                            <RotateCcw /> Reset
                        </Button>
                        <Button className="flex-1" onClick={() => setFiltersOpen(false)}>
                            Show {resultCount} {resultCount === 1 ? "movie" : "movies"}
                        </Button>
                    </SheetFooter>
                </SheetContent>
            </Sheet>
        </div>
    );
};

export default Home;
