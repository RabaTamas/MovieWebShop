import React from 'react';

const About = () => {
    return (
        <div className="container py-5">
            {/* Hero Section */}
            <div className="row justify-content-center mb-5">
                <div className="col-lg-8 text-center">
                    <div
                        className="d-inline-flex align-items-center justify-content-center rounded-circle mb-4"
                        style={{
                            width: '100px',
                            height: '100px',
                            background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
                            color: 'white'
                        }}
                    >
                        <i className="bi bi-film fs-1"></i>
                    </div>
                    <h1 className="display-4 fw-bold text-dark mb-3">About MovieWebshop</h1>
                    <p className="lead text-muted">
                        Your ultimate destination for discovering, purchasing, and enjoying the best movies from around the world.
                    </p>
                </div>
            </div>

            {/* Main Content */}
            <div className="row g-5">
                {/* What We Are */}
                <div className="col-lg-6">
                    <div
                        className="h-100 p-4 rounded-4 border"
                        style={{
                            background: 'linear-gradient(135deg, #f8fafc, #e2e8f0)',
                            borderLeft: '5px solid transparent',
                            borderImage: 'linear-gradient(135deg, #3b82f6, #1d4ed8)',
                            borderImageSlice: 1
                        }}
                    >
                        <div className="d-flex align-items-center mb-3">
                            <div
                                className="d-flex align-items-center justify-content-center rounded-circle me-3"
                                style={{
                                    width: '50px',
                                    height: '50px',
                                    background: 'linear-gradient(135deg, #3b82f6, #1d4ed8)',
                                    color: 'white'
                                }}
                            >
                                <i className="bi bi-info-circle fs-5"></i>
                            </div>
                            <h3 className="fw-bold text-dark mb-0">What We Are</h3>
                        </div>
                        <p className="text-muted lh-lg">
                            MovieWebshop is a modern e-commerce platform dedicated to movie enthusiasts.
                            We provide a seamless shopping experience where you can browse, discover, and
                            purchase your favorite movies from various genres and eras.
                        </p>
                        <p className="text-muted lh-lg mb-0">
                            Built with cutting-edge technology using React and ASP.NET Core, we ensure
                            a fast, secure, and user-friendly experience for all our customers.
                        </p>
                    </div>
                </div>

                {/* Our Mission */}
                <div className="col-lg-6">
                    <div
                        className="h-100 p-4 rounded-4 border"
                        style={{
                            background: 'linear-gradient(135deg, #f0fdf4, #dcfce7)',
                            borderLeft: '5px solid transparent',
                            borderImage: 'linear-gradient(135deg, #10b981, #059669)',
                            borderImageSlice: 1
                        }}
                    >
                        <div className="d-flex align-items-center mb-3">
                            <div
                                className="d-flex align-items-center justify-content-center rounded-circle me-3"
                                style={{
                                    width: '50px',
                                    height: '50px',
                                    background: 'linear-gradient(135deg, #10b981, #059669)',
                                    color: 'white'
                                }}
                            >
                                <i className="bi bi-bullseye fs-5"></i>
                            </div>
                            <h3 className="fw-bold text-dark mb-0">Our Mission</h3>
                        </div>
                        <p className="text-muted lh-lg">
                            Our mission is to make great movies accessible to everyone. We believe that
                            cinema has the power to inspire, entertain, and bring people together.
                        </p>
                        <p className="text-muted lh-lg mb-0">
                            We strive to curate the finest collection of movies and provide exceptional
                            customer service to ensure every purchase enhances your movie-watching experience.
                        </p>
                    </div>
                </div>

                {/* Features Grid */}
                <div className="col-12">
                    <div className="text-center mb-5">
                        <h2 className="fw-bold text-dark mb-3">Why Choose MovieWebshop?</h2>
                        <p className="text-muted">Discover what makes us the perfect choice for movie lovers</p>
                    </div>

                    <div className="row g-4">
                        {/* Feature 1 */}
                        <div className="col-md-6 col-lg-3">
                            <div className="text-center p-4 bg-white rounded-4 shadow-sm border h-100">
                                <div
                                    className="d-inline-flex align-items-center justify-content-center rounded-circle mb-3"
                                    style={{
                                        width: '70px',
                                        height: '70px',
                                        background: 'linear-gradient(135deg, #f093fb 0%, #f5576c 100%)',
                                        color: 'white'
                                    }}
                                >
                                    <i className="bi bi-collection fs-4"></i>
                                </div>
                                <h5 className="fw-bold text-dark mb-2">Vast Collection</h5>
                                <p className="text-muted small mb-0">
                                    Browse through thousands of movies across all genres, from classics to the latest releases.
                                </p>
                            </div>
                        </div>

                        {/* Feature 2 */}
                        <div className="col-md-6 col-lg-3">
                            <div className="text-center p-4 bg-white rounded-4 shadow-sm border h-100">
                                <div
                                    className="d-inline-flex align-items-center justify-content-center rounded-circle mb-3"
                                    style={{
                                        width: '70px',
                                        height: '70px',
                                        background: 'linear-gradient(135deg, #4facfe 0%, #00f2fe 100%)',
                                        color: 'white'
                                    }}
                                >
                                    <i className="bi bi-shield-check fs-4"></i>
                                </div>
                                <h5 className="fw-bold text-dark mb-2">Secure Shopping</h5>
                                <p className="text-muted small mb-0">
                                    Shop with confidence using our secure payment system and encrypted transactions.
                                </p>
                            </div>
                        </div>

                        {/* Feature 3 */}
                        <div className="col-md-6 col-lg-3">
                            <div className="text-center p-4 bg-white rounded-4 shadow-sm border h-100">
                                <div
                                    className="d-inline-flex align-items-center justify-content-center rounded-circle mb-3"
                                    style={{
                                        width: '70px',
                                        height: '70px',
                                        background: 'linear-gradient(135deg, #43e97b 0%, #38f9d7 100%)',
                                        color: 'white'
                                    }}
                                >
                                    <i className="bi bi-chat-dots fs-4"></i>
                                </div>
                                <h5 className="fw-bold text-dark mb-2">Community Reviews</h5>
                                <p className="text-muted small mb-0">
                                    Read authentic reviews from fellow movie lovers and share your own experiences.
                                </p>
                            </div>
                        </div>

                        {/* Feature 4 */}
                        <div className="col-md-6 col-lg-3">
                            <div className="text-center p-4 bg-white rounded-4 shadow-sm border h-100">
                                <div
                                    className="d-inline-flex align-items-center justify-content-center rounded-circle mb-3"
                                    style={{
                                        width: '70px',
                                        height: '70px',
                                        background: 'linear-gradient(135deg, #fa709a 0%, #fee140 100%)',
                                        color: 'white'
                                    }}
                                >
                                    <i className="bi bi-lightning fs-4"></i>
                                </div>
                                <h5 className="fw-bold text-dark mb-2">Fast Delivery</h5>
                                <p className="text-muted small mb-0">
                                    Quick and reliable delivery service to get your movies to you as soon as possible.
                                </p>
                            </div>
                        </div>
                    </div>
                </div>

                {/* Technology Stack */}
                <div className="col-12">
                    <div
                        className="p-5 rounded-4 text-center"
                        style={{
                            background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
                            color: 'white'
                        }}
                    >
                        <h3 className="fw-bold mb-3">Built with Modern Technology</h3>
                        <p className="mb-4 opacity-90">
                            MovieWebshop is powered by cutting-edge web technologies to ensure the best user experience.
                        </p>

                        <div className="row justify-content-center g-4">
                            <div className="col-auto">
                                <div className="d-flex align-items-center gap-2 bg-white bg-opacity-20 rounded-pill px-4 py-2">
                                    <i className="bi bi-code-slash"></i>
                                    <span className="fw-semibold">React</span>
                                </div>
                            </div>
                            <div className="col-auto">
                                <div className="d-flex align-items-center gap-2 bg-white bg-opacity-20 rounded-pill px-4 py-2">
                                    <i className="bi bi-server"></i>
                                    <span className="fw-semibold">ASP.NET Core</span>
                                </div>
                            </div>
                            <div className="col-auto">
                                <div className="d-flex align-items-center gap-2 bg-white bg-opacity-20 rounded-pill px-4 py-2">
                                    <i className="bi bi-database"></i>
                                    <span className="fw-semibold">SQL Server</span>
                                </div>
                            </div>
                            <div className="col-auto">
                                <div className="d-flex align-items-center gap-2 bg-white bg-opacity-20 rounded-pill px-4 py-2">
                                    <i className="bi bi-bootstrap"></i>
                                    <span className="fw-semibold">Bootstrap</span>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                {/* Call to Action */}
                <div className="col-12">
                    <div className="text-center py-5">
                        <h3 className="fw-bold text-dark mb-3">Ready to Start Your Movie Journey?</h3>
                        <p className="text-muted mb-4">
                            Join thousands of movie enthusiasts who have made MovieWebshop their go-to destination for cinema.
                        </p>
                        <div className="d-flex flex-column flex-sm-row gap-3 justify-content-center">
                            <a
                                href="/"
                                className="btn btn-lg px-5 py-3 fw-semibold"
                                style={{
                                    background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
                                    border: 'none',
                                    color: 'white',
                                    borderRadius: '12px',
                                    textDecoration: 'none'
                                }}
                            >
                                <i className="bi bi-house-door me-2"></i>
                                Browse Movies
                            </a>
                            <a
                                href="/contact"
                                className="btn btn-outline-primary btn-lg px-5 py-3 fw-semibold"
                                style={{ borderRadius: '12px', textDecoration: 'none' }}
                            >
                                <i className="bi bi-envelope me-2"></i>
                                Contact Us
                            </a>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default About;