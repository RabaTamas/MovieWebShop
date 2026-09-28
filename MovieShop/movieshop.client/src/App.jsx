import Navbar from './components/Navbar';
import Footer from './components/Footer';
import { Toaster } from './components/ui/sonner';
import PwaUpdater from './components/pwa/PwaUpdater';
import OfflineBanner from './components/pwa/OfflineBanner';
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
import About from './pages/About';
import Chatbot from './components/Chatbot';
import MyMovies from './pages/MyMovies';
import WatchMovie from './pages/WatchMovie';
import AdminVideoUpload from './pages/Admin/AdminVideoUpload';
import WatchParty from './pages/WatchParty';
import TwoFactorSetup from './pages/TwoFactorSetup';
import Auctions from './pages/Auctions';
import Recommendations from './pages/Recommendations';
import AuctionDetail from './pages/AuctionDetail';
import AdminAuctions from './pages/AdminAuctions';
import MyWonAuctions from './pages/MyWonAuctions';

function App() {
    return (
        <Router>
            <AuthProvider>
                <div className="flex min-h-screen flex-col">
                    <Navbar />
                    <OfflineBanner />
                    <main className="flex flex-1 flex-col">
                        <Routes>
                            {/* Public routes */}
                            <Route path="/" element={<Home />} />
                            <Route path="/about" element={<About />} />
                            <Route path="/auctions" element={<Auctions />} />
                            <Route path="/auctions/:id" element={<AuctionDetail />} />
                            <Route
                                path="/recommendations"
                                element={
                                    <PrivateRoute>
                                        <Recommendations />
                                    </PrivateRoute>
                                }
                            />
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
                            <Route
                                path="/my-movies"
                                element={
                                    <PrivateRoute>
                                        <MyMovies />
                                    </PrivateRoute>
                                }
                            />
                            <Route
                                path="/my-movies/:movieId/watch"
                                element={
                                    <PrivateRoute>
                                        <WatchMovie />
                                    </PrivateRoute>
                                }
                            />
                            <Route
                                path="/my-movies/:movieId/watch-party"
                                element={
                                    <PrivateRoute>
                                        <WatchParty />
                                    </PrivateRoute>
                                }
                            />
                            <Route
                                path="/my-wins"
                                element={
                                    <PrivateRoute>
                                        <MyWonAuctions />
                                    </PrivateRoute>
                                }
                            />
                            <Route
                                path="/profile/2fa"
                                element={
                                    <PrivateRoute>
                                        <TwoFactorSetup />
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
                                path="/admin/movies/:movieId/video"
                                element={
                                    <AdminRoute>
                                        <AdminVideoUpload />
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
                            <Route
                                path="/admin/auctions"
                                element={
                                    <AdminRoute>
                                        <AdminAuctions />
                                    </AdminRoute>
                                }
                            />

                            {/* Catch all route for 404 */}
                            <Route path="*" element={<NotFound />} />
                        </Routes>
                    </main>
                    <Footer />
                    <Chatbot />
                    <Toaster position="top-center" richColors closeButton />
                    <PwaUpdater />
                </div>
            </AuthProvider>
        </Router>
    );
}

export default App;
