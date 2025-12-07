import { Link } from "react-router-dom";
import { useAuth } from "../contexts/AuthContext";
import { useState, useRef, useEffect } from "react";

const Navbar = () => {
    const { user, logout } = useAuth();
    const [settingsOpen, setSettingsOpen] = useState(false);
    const [adminMenuOpen, setAdminMenuOpen] = useState(false);
    const dropdownRef = useRef(null);
    const adminDropdownRef = useRef(null);

    // Check if user is admin
    const isAdmin = user && user.role === "Admin";

    // Close the dropdowns when clicking outside
    useEffect(() => {
        function handleClickOutside(event) {
            if (dropdownRef.current && !dropdownRef.current.contains(event.target)) {
                setSettingsOpen(false);
            }
            if (adminDropdownRef.current && !adminDropdownRef.current.contains(event.target)) {
                setAdminMenuOpen(false);
            }
        }

        document.addEventListener("mousedown", handleClickOutside);
        return () => {
            document.removeEventListener("mousedown", handleClickOutside);
        };
    }, []);

    const toggleSettings = () => {
        setSettingsOpen(!settingsOpen);
    };

    const toggleAdminMenu = () => {
        setAdminMenuOpen(!adminMenuOpen);
    };

    return (
        <nav className="navbar navbar-expand-lg navbar-dark bg-dark px-4">
            <Link className="navbar-brand" to="/">MovieWebShop</Link>
            <button
                className="navbar-toggler"
                type="button"
                data-bs-toggle="collapse"
                data-bs-target="#navbarContent"
                aria-controls="navbarContent"
                aria-expanded="false"
                aria-label="Toggle navigation"
            >
                <span className="navbar-toggler-icon"></span>
            </button>

            <div className="collapse navbar-collapse" id="navbarContent">
                <ul className="navbar-nav me-auto">
                    <li className="nav-item">
                        <Link className="nav-link" to="/">Home</Link>
                    </li>
                    <li className="nav-item">
                        <Link className="nav-link" to="/about">About</Link>
                    </li>

                    {/* Admin Menu - Only visible for admin users */}
                    {isAdmin && (
                        <li className="nav-item dropdown" ref={adminDropdownRef}>
                            <a
                                className={`nav-link dropdown-toggle ${adminMenuOpen ? 'show' : ''}`}
                                href="#"
                                role="button"
                                onClick={toggleAdminMenu}
                                aria-expanded={adminMenuOpen}
                            >
                                Admin Panel
                            </a>
                            <ul className={`dropdown-menu ${adminMenuOpen ? 'show' : ''}`}
                                style={{ display: adminMenuOpen ? 'block' : 'none' }}
                            >
                                <li>
                                    <Link className="dropdown-item" to="/admin/movies" onClick={() => setAdminMenuOpen(false)}>
                                        Manage Movies
                                    </Link>
                                </li>
                                <li>
                                    <Link className="dropdown-item" to="/admin/categories" onClick={() => setAdminMenuOpen(false)}>
                                        Manage Categories
                                    </Link>
                                </li>
                                <li>
                                    <Link className="dropdown-item" to="/admin/users" onClick={() => setAdminMenuOpen(false)}>
                                        Manage Users
                                    </Link>
                                </li>
                                <li>
                                    <Link className="dropdown-item" to="/admin/reviews" onClick={() => setAdminMenuOpen(false)}>
                                        Manage Reviews
                                    </Link>
                                </li>

                                <li>
                                    <Link className="dropdown-item" to="/admin/orders" onClick={() => setAdminMenuOpen(false)}>
                                        Manage Orders
                                    </Link>
                                </li>
                                <li>
                                    <Link className="dropdown-item" to="/admin/carts" onClick={() => setAdminMenuOpen(false)}>
                                        Manage ShoppingCarts
                                    </Link>
                                </li>
                                <li>
                                    <Link className="dropdown-item" to="/admin/addresses" onClick={() => setAdminMenuOpen(false)}>
                                        Manage Addresses
                                    </Link>
                                </li>
                                

                                
                            </ul>
                        </li>
                    )}
                </ul>

                <ul className="navbar-nav ms-auto">
                    {!user ? (
                        <>
                            <li className="nav-item">
                                <Link className="nav-link" to="/login">Login</Link>
                            </li>
                            <li className="nav-item">
                                <Link className="nav-link" to="/register">Register</Link>
                            </li>
                        </>
                    ) : (
                        <>
                            {/* Settings Dropdown */}
                            <li className="nav-item dropdown" ref={dropdownRef}>
                                <a
                                    className={`nav-link dropdown-toggle ${settingsOpen ? 'show' : ''}`}
                                    href="#"
                                    role="button"
                                    onClick={toggleSettings}
                                    aria-expanded={settingsOpen}
                                >
                                    Settings
                                </a>
                                <ul className={`dropdown-menu dropdown-menu-end ${settingsOpen ? 'show' : ''}`}
                                    style={{ display: settingsOpen ? 'block' : 'none' }}
                                >
                                    <li>
                                        <Link className="dropdown-item" to="/profile" onClick={() => setSettingsOpen(false)}>
                                            Profile
                                        </Link>
                                    </li>
                                    <li>
                                        <Link className="dropdown-item" to="/orders" onClick={() => setSettingsOpen(false)}>
                                            Orders
                                        </Link>
                                    </li>
                                    <li>
                                        <Link className="dropdown-item" to="/my-movies" onClick={() => setSettingsOpen(false)}>
                                            My Movies
                                        </Link>
                                    </li>
                                </ul>
                            </li>

                            <li className="nav-item">
                                <Link className="nav-link" to="/cart">My Cart</Link>
                            </li>
                            <li className="nav-item">
                                <button className="btn btn-outline-light ms-2" onClick={logout}>Logout</button>
                            </li>
                        </>
                    )}
                </ul>
            </div>
        </nav>
    );
};

export default Navbar;