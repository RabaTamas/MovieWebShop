import React, { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import { useTheme } from '../contexts/ThemeContext';
import { GoogleLogin } from '@react-oauth/google';
import { AlertCircle, ArrowLeft, LogIn, ShieldCheck } from 'lucide-react';
import API_BASE_URL from '../config/api';
import AuthLayout from '../components/AuthLayout';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Spinner } from '@/components/ui/spinner';

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
    const { theme } = useTheme();

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

    const errorAlert = error && (
        <Alert variant="destructive" className="mb-5">
            <AlertCircle />
            <AlertDescription>{error}</AlertDescription>
        </Alert>
    );

    // --- 2FA step ---
    if (twoFactorRequired) {
        return (
            <AuthLayout
                icon={ShieldCheck}
                title="Two-Factor Authentication"
                description="Open your authenticator app and enter the 6-digit code."
            >
                {errorAlert}

                <form onSubmit={handleTwoFactorSubmit} className="space-y-5">
                    <div className="space-y-2">
                        <Label htmlFor="twofactor-code">Authentication Code</Label>
                        <Input
                            id="twofactor-code"
                            type="text"
                            inputMode="numeric"
                            autoComplete="one-time-code"
                            className="h-14 text-center font-mono text-2xl tracking-[0.5em] md:text-2xl"
                            placeholder="000000"
                            maxLength={6}
                            value={twoFactorCode}
                            onChange={(e) => setTwoFactorCode(e.target.value.replace(/\D/g, ''))}
                            autoFocus
                            disabled={loading}
                        />
                    </div>
                    <Button type="submit" size="lg" className="w-full" disabled={loading || twoFactorCode.length !== 6}>
                        {loading && <Spinner />}
                        {loading ? 'Verifying...' : 'Verify'}
                    </Button>
                    <Button
                        type="button"
                        variant="ghost"
                        className="w-full"
                        onClick={() => { setTwoFactorRequired(false); setError(''); }}
                    >
                        <ArrowLeft /> Back to login
                    </Button>
                </form>
            </AuthLayout>
        );
    }

    // --- Normal login ---
    return (
        <AuthLayout icon={LogIn} title="Login" description="Welcome back! Sign in to continue watching.">
            {errorAlert}

            <form onSubmit={handleSubmit} className="space-y-4">
                <div className="space-y-2">
                    <Label htmlFor="email">Email</Label>
                    <Input
                        type="email"
                        id="email"
                        autoComplete="email"
                        placeholder="you@example.com"
                        value={email}
                        onChange={(e) => setEmail(e.target.value)}
                        disabled={loading}
                        required
                    />
                </div>

                <div className="space-y-2">
                    <Label htmlFor="password">Password</Label>
                    <Input
                        type="password"
                        id="password"
                        autoComplete="current-password"
                        value={password}
                        onChange={(e) => setPassword(e.target.value)}
                        disabled={loading}
                        required
                    />
                </div>

                <Button type="submit" size="lg" className="w-full" disabled={loading}>
                    {loading && <Spinner />}
                    {loading ? 'Logging in...' : 'Login'}
                </Button>
            </form>

            <div className="my-6 flex items-center gap-3 text-xs tracking-wider text-muted-foreground uppercase">
                <span className="h-px flex-1 bg-border" />
                Or continue with
                <span className="h-px flex-1 bg-border" />
            </div>

            <div className="flex justify-center">
                <GoogleLogin
                    onSuccess={handleGoogleSuccess}
                    onError={() => setError('Google login failed. Please try again.')}
                    theme={theme === 'dark' ? 'filled_black' : 'outline'}
                    size="large"
                    shape="pill"
                    text="continue_with"
                />
            </div>

            <p className="mt-6 text-center text-sm text-muted-foreground">
                Don't have an account?{' '}
                <Link to="/register" className="font-medium text-primary hover:underline">Register here</Link>
            </p>
        </AuthLayout>
    );
};

export default Login;
