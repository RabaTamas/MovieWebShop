import { useEffect, useState } from "react";
import MovieCard from "../components/MovieCard";
import Pagination from "../components/Pagination";
import API_BASE_URL from "../config/api";

const Home = () => {
    const [movies, setMovies] = useState([]);
    const [filteredMovies, setFilteredMovies] = useState([]);
    const [categories, setCategories] = useState([]);
    const [categoriesLoading, setCategoriesLoading] = useState(true);
    const [moviesLoading, setMoviesLoading] = useState(true);
    const [search, setSearch] = useState("");
    const [sort, setSort] = useState("");
    const [selectedCategories, setSelectedCategories] = useState([]);
    const [minPrice, setMinPrice] = useState("");
    const [maxPrice, setMaxPrice] = useState("");
    const [currentPage, setCurrentPage] = useState(1);
    const itemsPerPage = 8;
    const totalPages = Math.ceil(filteredMovies.length / itemsPerPage);
    const paginatedMovies = filteredMovies.slice(
        (currentPage - 1) * itemsPerPage,
        currentPage * itemsPerPage
    );

    // Fetch categories with better error handling and loading state
    useEffect(() => {
        const fetchCategories = async () => {
            setCategoriesLoading(true);
            try {
                const res = await fetch(`${API_BASE_URL}/api/Category`);
                if (!res.ok) {
                    throw new Error(`HTTP error! status: ${res.status}`);
                }
                const data = await res.json();
                setCategories(data || []);
            } catch (error) {
                console.error("Failed to fetch categories:", error);
                setCategories([]);
                // Retry after 2 seconds if failed
                setTimeout(() => {
                    fetchCategories();
                }, 2000);
            } finally {
                setCategoriesLoading(false);
            }
        };

        fetchCategories();
    }, []);

    // Fetch movies based on selected categories with better error handling
    useEffect(() => {
        const fetchMovies = async () => {
            setMoviesLoading(true);
            try {
                let url = "";

                if (selectedCategories.length === 0) {
                    url = `${API_BASE_URL}/api/Movie`;
                } else {
                    const params = new URLSearchParams();
                    selectedCategories.forEach(id => params.append("categoryIds", id));
                    url = `${API_BASE_URL}/api/Movie/categories?${params.toString()}`;
                }

                const res = await fetch(url);
                if (!res.ok) {
                    throw new Error(`HTTP error! status: ${res.status}`);
                }
                const data = await res.json();
                setMovies(data || []);
            } catch (error) {
                console.error("Failed to fetch movies:", error);
                setMovies([]);
                // Retry after 2 seconds if failed
                setTimeout(() => {
                    fetchMovies();
                }, 2000);
            } finally {
                setMoviesLoading(false);
            }
        };

        fetchMovies();
    }, [selectedCategories]);

    // Filter on client side (search, price, sort)
    useEffect(() => {
        let result = [...movies];

        if (search.trim() !== "") {
            result = result.filter(m =>
                m.title.toLowerCase().includes(search.toLowerCase())
            );
        }

        if (minPrice !== "") {
            result = result.filter(movie =>
                (movie.discountedPrice ?? movie.price) >= parseFloat(minPrice)
            );
        }

        if (maxPrice !== "") {
            result = result.filter(movie =>
                (movie.discountedPrice ?? movie.price) <= parseFloat(maxPrice)
            );
        }

        if (sort === "name-asc") {
            result.sort((a, b) => a.title.localeCompare(b.title));
        } else if (sort === "name-desc") {
            result.sort((a, b) => b.title.localeCompare(a.title));
        } else if (sort === "price-asc") {
            result.sort((a, b) => (a.discountedPrice ?? a.price) - (b.discountedPrice ?? b.price));
        } else if (sort === "price-desc") {
            result.sort((a, b) => (b.discountedPrice ?? b.price) - (a.discountedPrice ?? a.price));
        }

        setFilteredMovies(result);
    }, [movies, search, sort, minPrice, maxPrice]);

    useEffect(() => {
        setCurrentPage(1);
    }, [movies, search, sort, minPrice, maxPrice]);

    const toggleCategory = (categoryId) => {
        setSelectedCategories(prev =>
            prev.includes(categoryId)
                ? prev.filter(id => id !== categoryId)
                : [...prev, categoryId]
        );
    };

    return (
        <div className="container mt-4">
            <div className="row">
                {/* Sidebar Filters */}
                <div className="col-md-3">
                    <h5>Search</h5>
                    <input
                        type="text"
                        className="form-control mb-3"
                        placeholder="Search movies..."
                        value={search}
                        onChange={e => setSearch(e.target.value)}
                    />

                    <h5>Sort</h5>
                    <select
                        className="form-control mb-3"
                        value={sort}
                        onChange={e => setSort(e.target.value)}
                    >
                        <option value="">Choose...</option>
                        <option value="name-asc">Name (A-Z)</option>
                        <option value="name-desc">Name (Z-A)</option>
                        <option value="price-asc">Price (Low → High)</option>
                        <option value="price-desc">Price (High → Low)</option>
                    </select>

                    <h5>Filter by Category</h5>
                    {categoriesLoading ? (
                        <div className="d-flex justify-content-center py-3">
                            <div className="spinner-border spinner-border-sm text-primary" role="status">
                                <span className="visually-hidden">Loading categories...</span>
                            </div>
                        </div>
                    ) : categories.length > 0 ? (
                        categories.map(cat => (
                            <div key={cat.id} className="form-check">
                                <input
                                    className="form-check-input"
                                    type="checkbox"
                                    checked={selectedCategories.includes(cat.id)}
                                    onChange={() => toggleCategory(cat.id)}
                                    id={`category-${cat.id}`}
                                />
                                <label className="form-check-label" htmlFor={`category-${cat.id}`}>
                                    {cat.name}
                                </label>
                            </div>
                        ))
                    ) : (
                        <div className="alert alert-warning">
                            <small>Failed to load categories. Retrying...</small>
                        </div>
                    )}

                    <h5 className="mt-3">Filter by Price</h5>
                    <div className="input-group mb-2">
                        <input
                            type="number"
                            className="form-control"
                            placeholder="Min"
                            value={minPrice}
                            onChange={e => setMinPrice(e.target.value)}
                        />
                        <span className="input-group-text">-</span>
                        <input
                            type="number"
                            className="form-control"
                            placeholder="Max"
                            value={maxPrice}
                            onChange={e => setMaxPrice(e.target.value)}
                        />
                    </div>
                </div>

                {/* Main Movie Grid */}
                <div className="col-md-9">
                    {moviesLoading ? (
                        <div className="d-flex justify-content-center align-items-center" style={{ height: '300px' }}>
                            <div className="text-center">
                                <div className="spinner-border text-primary mb-3" role="status">
                                    <span className="visually-hidden">Loading movies...</span>
                                </div>
                                <div>Loading movies...</div>
                            </div>
                        </div>
                    ) : filteredMovies.length > 0 ? (
                        <div className="row row-cols-1 row-cols-sm-2 row-cols-md-3 row-cols-lg-4 g-4">
                            {paginatedMovies.map(movie => (
                                <div className="col" key={movie.id}>
                                    <MovieCard movie={movie} />
                                </div>
                            ))}
                        </div>
                    ) : (
                        <div className="alert alert-warning text-center">
                            There are no movies matching your filters!
                        </div>
                    )}

                    {!moviesLoading && filteredMovies.length > 0 && (
                        <Pagination
                            currentPage={currentPage}
                            totalPages={totalPages}
                            onPageChange={setCurrentPage}
                        />
                    )}
                </div>
            </div>
        </div>
    );
};

export default Home;