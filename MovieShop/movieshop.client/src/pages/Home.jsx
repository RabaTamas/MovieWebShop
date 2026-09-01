import { useEffect, useState, useRef, useCallback } from "react";
import { Link } from "react-router-dom";
import MovieCard from "../components/MovieCard";
import Pagination from "../components/Pagination";
import API_BASE_URL from "../config/api";
import { useAuth } from "../contexts/AuthContext";

const Home = () => {
    const { token } = useAuth();

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

    const isSearchMode = !!search.trim();
    const isLoading = isSearchMode ? searchLoading : moviesLoading;

    const displayMovies = isSearchMode
        ? (searchResults?.movies ?? [])
        : paginatedMovies;

    const searchTotalPages = searchResults
        ? Math.ceil(searchResults.total / ITEMS_PER_PAGE)
        : 0;

    return (
        <div className="container mt-4">
            <div className="row">
                {/* ── Sidebar ─────────────────────────────── */}
                <div className="col-md-3">
                    {/* Search with autocomplete */}
                    <h5>Search</h5>
                    <div className="position-relative mb-3">
                        <input
                            ref={searchInputRef}
                            type="text"
                            className="form-control"
                            placeholder="Search movies..."
                            value={search}
                            onChange={handleSearchChange}
                            onFocus={() => setShowSuggestions(true)}
                            onBlur={() => setTimeout(() => setShowSuggestions(false), 150)}
                        />
                        {search && (
                            <button
                                className="btn btn-sm btn-link position-absolute top-50 end-0 translate-middle-y pe-2"
                                onClick={() => { setSearch(""); setSearchResults(null); setSuggestions([]); }}
                                style={{ zIndex: 10 }}
                            >
                                <i className="bi bi-x-lg text-secondary" />
                            </button>
                        )}
                        {showSuggestions && suggestions.length > 0 && (
                            <ul className="list-group position-absolute w-100 shadow-sm" style={{ zIndex: 1000, top: "100%" }}>
                                {suggestions.map((s, i) => (
                                    <li
                                        key={i}
                                        className="list-group-item list-group-item-action py-1 px-3"
                                        style={{ cursor: "pointer" }}
                                        onMouseDown={() => applySuggestion(s)}
                                    >
                                        <i className="bi bi-search me-2 text-muted small" />
                                        {s}
                                    </li>
                                ))}
                            </ul>
                        )}
                    </div>

                    <h5>Sort</h5>
                    <select className="form-control mb-3" value={sort} onChange={e => { setSort(e.target.value); setCurrentPage(1); setSearchPage(1); }}>
                        <option value="">Choose...</option>
                        <option value="name-asc">Name (A-Z)</option>
                        <option value="name-desc">Name (Z-A)</option>
                        <option value="price-asc">Price (Low → High)</option>
                        <option value="price-desc">Price (High → Low)</option>
                        {isSearchMode && <option value="rating-desc">Rating (Best first)</option>}
                    </select>

                    <h5>Filter by Category</h5>
                    {categoriesLoading ? (
                        <div className="d-flex justify-content-center py-2">
                            <div className="spinner-border spinner-border-sm text-primary" role="status" />
                        </div>
                    ) : categories.map(cat => (
                        <div key={cat.id} className="form-check">
                            <input
                                className="form-check-input"
                                type="checkbox"
                                checked={selectedCategories.includes(cat.id)}
                                onChange={() => toggleCategory(cat.id)}
                                id={`cat-${cat.id}`}
                            />
                            <label className="form-check-label" htmlFor={`cat-${cat.id}`}>{cat.name}</label>
                        </div>
                    ))}

                    <h5 className="mt-3">Filter by Price</h5>
                    <div className="input-group mb-2">
                        <input type="number" className="form-control" placeholder="Min" value={minPrice}
                            onChange={e => { setMinPrice(e.target.value); setCurrentPage(1); setSearchPage(1); }} />
                        <span className="input-group-text">-</span>
                        <input type="number" className="form-control" placeholder="Max" value={maxPrice}
                            onChange={e => { setMaxPrice(e.target.value); setCurrentPage(1); setSearchPage(1); }} />
                    </div>

                    {isSearchMode && searchResults && (
                        <div className="alert alert-info py-2 small mt-2">
                            <i className="bi bi-lightning-charge-fill me-1" />
                            <strong>{searchResults.total}</strong> result{searchResults.total !== 1 ? "s" : ""} found
                        </div>
                    )}
                </div>

                {/* ── Main grid ───────────────────────────── */}
                <div className="col-md-9">

                    {/* ── Recommendations teaser ──────────── */}
                    {!isSearchMode && recommendations &&
                        (recommendations.categoryBased?.length > 0 || recommendations.collaborativeBased?.length > 0) && (
                        <div className="alert alert-warning d-flex align-items-center justify-content-between mb-4 py-2">
                            <span>
                                <i className="bi bi-stars me-2" />
                                We have <strong>personalised recommendations</strong> for you!
                            </span>
                            <Link to="/recommendations" className="btn btn-sm btn-warning ms-3">
                                View all →
                            </Link>
                        </div>
                    )}
                    {isLoading ? (
                        <div className="d-flex justify-content-center align-items-center" style={{ height: "300px" }}>
                            <div className="text-center">
                                <div className="spinner-border text-primary mb-3" role="status" />
                                <div>{isSearchMode ? "Searching..." : "Loading movies..."}</div>
                            </div>
                        </div>
                    ) : displayMovies.length > 0 ? (
                        <div className="row row-cols-1 row-cols-sm-2 row-cols-md-3 row-cols-lg-4 g-4">
                            {displayMovies.map(movie => (
                                <div className="col" key={movie.id}>
                                    <MovieCard movie={movie} />
                                </div>
                            ))}
                        </div>
                    ) : (
                        <div className="alert alert-warning text-center">
                            {isSearchMode
                                ? `No results for "${search}"`
                                : "There are no movies matching your filters!"}
                        </div>
                    )}

                    {!isLoading && !isSearchMode && filteredMovies.length > 0 && (
                        <Pagination currentPage={currentPage} totalPages={browseTotalPages} onPageChange={setCurrentPage} />
                    )}

                    {!isLoading && isSearchMode && searchTotalPages > 1 && (
                        <Pagination currentPage={searchPage} totalPages={searchTotalPages} onPageChange={setSearchPage} />
                    )}
                </div>
            </div>
        </div>
    );
};

export default Home;
