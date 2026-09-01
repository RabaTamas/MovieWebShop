import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { GoogleLogin } from '@react-oauth/google';
import API_BASE_URL from '../config/api';

const Login = () => {
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [error, setError] = useState('');
    const [loading, setLoading] = useState(false);

    // 2FA state
    const [twoFactorRequired, setTwoFactorRequired] = useState(false);
    const [twoFactorUserId, setTwoFactorUserId] = useState('');
    const [twoFactorCode, setTwoFactorCode] = useState('');

    const navigate = useNavigate();
    const { login } = useAuth();

    const handleSubmit = async (e) => {
        e.preventDefault();
        if (!email || !password) {
            setError('Please enter both email and password');
            return;
        }
        setLoading(true);
        setError('');
        try {
            const response = await fetch(`${API_BASE_URL}/api/Auth/login`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ email, password }),
            });
            const data = await response.json();
            if (!response.ok) throw new Error(data.message || 'Login failed');

            if (data.requiresTwoFactor) {
                setTwoFactorRequired(true);
                setTwoFactorUserId(data.twoFactorUserId);
                return;
            }

            login(data);
            navigate('/');
        } catch (err) {
            setError(err.message || 'An error occurred during login');
        } finally {
            setLoading(false);
        }
    };

    const handleTwoFactorSubmit = async (e) => {
        e.preventDefault();
        if (!twoFactorCode.trim()) {
            setError('Please enter the authentication code');
            return;
        }
        setLoading(true);
        setError('');
        try {
            const response = await fetch(`${API_BASE_URL}/api/Auth/2fa/login`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ twoFactorUserId, code: twoFactorCode }),
            });
            const data = await response.json();
            if (!response.ok) throw new Error(data.message || 'Invalid code');

            login(data);
            navigate('/');
        } catch (err) {
            setError(err.message || 'An error occurred');
            setTwoFactorCode('');
        } finally {
            setLoading(false);
        }
    };

    const handleGoogleSuccess = async (credentialResponse) => {
        setLoading(true);
        setError('');
        try {
            const response = await fetch(`${API_BASE_URL}/api/Auth/google-login`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ idToken: credentialResponse.credential }),
            });
            const data = await response.json();
            if (!response.ok) throw new Error(data.message || 'Google login failed');
            login(data);
            navigate('/');
        } catch (err) {
            setError(err.message || 'An error occurred during Google login');
        } finally {
            setLoading(false);
        }
    };

    // --- 2FA step ---
    if (twoFactorRequired) {
        return (
            <div className="container mt-5">
                <div className="row justify-content-center">
                    <div className="col-md-5">
                        <div className="card shadow-sm">
                            <div className="card-body p-4">
                                <div className="text-center mb-4">
                                    <i className="bi bi-shield-lock fs-1 text-primary"></i>
                                    <h4 className="mt-2">Two-Factor Authentication</h4>
                                    <p className="text-muted small">Open your authenticator app and enter the 6-digit code.</p>
                                </div>

                                {error && <div className="alert alert-danger">{error}</div>}

                                <form onSubmit={handleTwoFactorSubmit}>
                                    <div className="mb-3">
                                        <label className="form-label">Authentication Code</label>
                                        <input
                                            type="text"
                                            className="form-control form-control-lg text-center"
                                            placeholder="000000"
                                            maxLength={6}
                                            value={twoFactorCode}
                                            onChange={(e) => setTwoFactorCode(e.target.value.replace(/\D/g, ''))}
                                            autoFocus
                                            disabled={loading}
                                        />
                                    </div>
                                    <button type="submit" className="btn btn-primary w-100" disabled={loading || twoFactorCode.length !== 6}>
                                        {loading ? 'Verifying...' : 'Verify'}
                                    </button>
                                    <button
                                        type="button"
                                        className="btn btn-link w-100 mt-2"
                                        onClick={() => { setTwoFactorRequired(false); setError(''); }}
                                    >
                                        ← Back to login
                                    </button>
                                </form>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        );
    }

    // --- Normal login ---
    return (
        <div className="container mt-5">
            <div className="row justify-content-center">
                <div className="col-md-6">
                    <h2 className="text-center mb-4">Login</h2>

                    {error && <div className="alert alert-danger">{error}</div>}

                    <form onSubmit={handleSubmit}>
                        <div className="mb-3">
                            <label className="form-label" htmlFor="email">Email</label>
                            <input
                                type="email"
                                className="form-control"
                                id="email"
                                value={email}
                                onChange={(e) => setEmail(e.target.value)}
                                disabled={loading}
                                required
                            />
                        </div>

                        <div className="mb-3">
                            <label className="form-label" htmlFor="password">Password</label>
                            <input
                                type="password"
                                className="form-control"
                                id="password"
                                value={password}
                                onChange={(e) => setPassword(e.target.value)}
                                disabled={loading}
                                required
                            />
                        </div>

                        <button type="submit" className="btn btn-primary w-100 mb-3" disabled={loading}>
                            {loading ? 'Logging in...' : 'Login'}
                        </button>
                    </form>

                    <div className="text-center mb-3">
                        <hr className="my-4" />
                        <p className="text-muted">Or continue with</p>
                    </div>

                    <div className="d-flex justify-content-center">
                        <GoogleLogin
                            onSuccess={handleGoogleSuccess}
                            onError={() => setError('Google login failed. Please try again.')}
                            theme="outline"
                            size="large"
                            text="continue_with"
                        />
                    </div>

                    <div className="mt-4 text-center">
                        Don't have an account? <a href="/register">Register here</a>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default Login;
