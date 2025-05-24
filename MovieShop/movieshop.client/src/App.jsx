import './App.css';
import Navbar from './components/Navbar';
import { BrowserRouter as Router, Routes, Route } from 'react-router-dom';
import { AuthProvider } from './contexts/AuthContext';
import Home from './pages/Home';
import Login from "./pages/Login";
import Register from "./pages/Register";
import MovieDetails from "./pages/MovieDetails";
import Cart from "./pages/Cart";
import PrivateRoute from './components/PrivateRoute';
import AdminRoute from './components/AdminRoute';
import Orders from './pages/Orders';
import Profile from './pages/Profile';
import AdminMovies from './pages/AdminMovies';
import MovieForm from './components/MovieForm';
import MovieCategories from './components/MovieCategories';
import AdminCategories from './pages/AdminCategories';
import AdminUsers from './pages/AdminUsers';
import AdminReviews from './pages/AdminReviews';
import AdminOrders from './pages/AdminOrders';
import AdminOrderDetails from './pages/AdminOrderDetails';
import AdminShoppingCarts from './pages/AdminShoppingCarts';
import NotFound from './pages/NotFound'; 
import Unauthorized from './pages/Unauthorized';
import AdminAddresses from './pages/AdminAddresses';

function App() {
    return (
        <Router>
            <AuthProvider>
                <div className="app-container">
                    <Navbar />
                    <div className="main-content">
                        <Routes>
                            {/* Public routes */}
                            <Route path="/" element={<Home />} />
                            <Route path="/login" element={<Login />} />
                            <Route path="/register" element={<Register />} />
                            <Route path="/movies/:id" element={<MovieDetails />} />
                            <Route path="/unauthorized" element={<Unauthorized />} />

                            {/* Protected user routes */}
                            <Route
                                path="/cart"
                                element={
                                    <PrivateRoute>
                                        <Cart />
                                    </PrivateRoute>
                                }
                            />
                            <Route
                                path="/orders"
                                element={
                                    <PrivateRoute>
                                        <Orders />
                                    </PrivateRoute>
                                }
                            />
                            <Route
                                path="/profile"
                                element={
                                    <PrivateRoute>
                                        <Profile />
                                    </PrivateRoute>
                                }
                            />

                            {/* Admin routes - protected with AdminRoute */}
                            <Route
                                path="/admin/movies"
                                element={
                                    <AdminRoute>
                                        <AdminMovies />
                                    </AdminRoute>
                                }
                            />
                            <Route
                                path="/admin/movies/add"
                                element={
                                    <AdminRoute>
                                        <MovieForm />
                                    </AdminRoute>
                                }
                            />
                            <Route
                                path="/admin/movies/edit/:id"
                                element={
                                    <AdminRoute>
                                        <MovieForm />
                                    </AdminRoute>
                                }
                            />
                            <Route
                                path="/admin/movies/categories/:id"
                                element={
                                    <AdminRoute>
                                        <MovieCategories />
                                    </AdminRoute>
                                }
                            />
                            <Route
                                path="/admin/categories"
                                element={
                                    <AdminRoute>
                                        <AdminCategories />
                                    </AdminRoute>
                                }
                            />
                            <Route
                                path="/admin/users"
                                element={
                                    <AdminRoute>
                                        <AdminUsers />
                                    </AdminRoute>
                                }
                            />
                            <Route
                                path="/admin/reviews"
                                element={
                                    <AdminRoute>
                                        <AdminReviews />
                                    </AdminRoute>
                                }
                            />
                            <Route
                                path="/admin/orders"
                                element={
                                    <AdminRoute>
                                        <AdminOrders />
                                    </AdminRoute>
                                }
                            />
                            <Route
                                path="/admin/orders/:id"
                                element={
                                    <AdminRoute>
                                        <AdminOrderDetails />
                                    </AdminRoute>
                                }
                            />
                            <Route
                                path="/admin/carts"
                                element={
                                    <AdminRoute>
                                        <AdminShoppingCarts />
                                    </AdminRoute>
                                }
                            />

                            <Route
                                path="/admin/addresses"
                                element={
                                    <AdminRoute>
                                        <AdminAddresses />
                                    </AdminRoute>
                                }
                            />

                            {/* Catch all route for 404 */}
                            <Route path="*" element={<NotFound />} />
                        </Routes>
                    </div>
                </div>
            </AuthProvider>
        </Router>
    );
}

export default App;
