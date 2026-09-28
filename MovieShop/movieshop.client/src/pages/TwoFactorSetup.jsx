import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { QRCodeSVG } from 'qrcode.react';
import { AlertCircle, ArrowLeft, KeyRound, ShieldCheck, ShieldOff, ShieldX, TriangleAlert } from 'lucide-react';
import { useAuth } from '../contexts/AuthContext';
import API_BASE_URL from '../config/api';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Separator } from '@/components/ui/separator';
import { LoadingState, Spinner } from '@/components/ui/spinner';
import { PageContainer } from '@/components/ui/page';

const CodeInput = ({ value, onChange }) => (
    <Input
        type="text"
        inputMode="numeric"
        autoComplete="one-time-code"
        className="h-14 text-center font-mono text-2xl tracking-[0.5em] md:text-2xl"
        placeholder="000000"
        maxLength={6}
        value={value}
        onChange={onChange}
        autoFocus
    />
);

const StepTitle = ({ step, children }) => (
    <h2 className="flex items-center gap-3 font-semibold">
        <span className="flex size-7 items-center justify-center rounded-full bg-primary text-sm text-primary-foreground">{step}</span>
        {children}
    </h2>
);

const TwoFactorSetup = () => {
    const { token } = useAuth();
    const navigate = useNavigate();

    const [status, setStatus] = useState(null);       // { isEnabled }
    const [setup, setSetup] = useState(null);          // { sharedKey, authenticatorUri }
    const [code, setCode] = useState('');
    const [recoveryCodes, setRecoveryCodes] = useState([]);
    const [error, setError] = useState('');
    const [loading, setLoading] = useState(false);
    const [phase, setPhase] = useState('status');      // status | setup | recovery | disable

    const authHeaders = { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` };

    useEffect(() => {
        fetch(`${API_BASE_URL}/api/Auth/2fa/status`, { headers: authHeaders })
            .then(r => r.json())
            .then(data => setStatus(data))
            .catch(() => setError('Failed to load 2FA status'));
    }, []);

    const startSetup = async () => {
        setLoading(true);
        setError('');
        try {
            const r = await fetch(`${API_BASE_URL}/api/Auth/2fa/setup`, { headers: authHeaders });
            const data = await r.json();
            if (!r.ok) throw new Error(data.message);
            setSetup(data);
            setPhase('setup');
        } catch (e) {
            setError(e.message);
        } finally {
            setLoading(false);
        }
    };

    const enableTwoFactor = async (e) => {
        e.preventDefault();
        setLoading(true);
        setError('');
        try {
            const r = await fetch(`${API_BASE_URL}/api/Auth/2fa/enable`, {
                method: 'POST',
                headers: authHeaders,
                body: JSON.stringify({ code }),
            });
            const data = await r.json();
            if (!r.ok) throw new Error(data.message);
            setRecoveryCodes(data.recoveryCodes);
            setStatus({ isEnabled: true });
            setPhase('recovery');
        } catch (e) {
            setError(e.message);
            setCode('');
        } finally {
            setLoading(false);
        }
    };

    const disableTwoFactor = async (e) => {
        e.preventDefault();
        setLoading(true);
        setError('');
        try {
            const r = await fetch(`${API_BASE_URL}/api/Auth/2fa/disable`, {
                method: 'POST',
                headers: authHeaders,
                body: JSON.stringify({ code }),
            });
            const data = await r.json();
            if (!r.ok) throw new Error(data.message);
            setStatus({ isEnabled: false });
            setCode('');
            setPhase('status');
        } catch (e) {
            setError(e.message);
            setCode('');
        } finally {
            setLoading(false);
        }
    };

    if (!status) {
        return error ? (
            <PageContainer size="md">
                <Alert variant="destructive"><AlertCircle /><AlertDescription>{error}</AlertDescription></Alert>
            </PageContainer>
        ) : <LoadingState />;
    }

    return (
        <PageContainer size="md" className="max-w-xl">
            <div className="mb-6 flex items-center gap-3">
                <Button variant="outline" size="sm" onClick={() => navigate('/profile')}>
                    <ArrowLeft /> Back
                </Button>
                <h1 className="text-2xl font-bold tracking-tight">Two-Factor Authentication</h1>
            </div>

            {error && (
                <Alert variant="destructive" className="mb-5">
                    <AlertCircle />
                    <AlertDescription>{error}</AlertDescription>
                </Alert>
            )}

            {/* ── STATUS ── */}
            {phase === 'status' && (
                <Card>
                    <CardContent className="flex flex-col items-center gap-3 py-4 text-center">
                        <div className={cn(
                            "flex size-20 items-center justify-center rounded-full",
                            status.isEnabled ? "bg-success/15 text-success" : "bg-muted text-muted-foreground"
                        )}>
                            {status.isEnabled ? <ShieldCheck className="size-10" /> : <ShieldX className="size-10" />}
                        </div>
                        <h2 className="text-xl font-semibold">
                            2FA is currently{' '}
                            <span className={status.isEnabled ? 'text-success' : 'text-destructive'}>
                                {status.isEnabled ? 'enabled' : 'disabled'}
                            </span>
                        </h2>
                        <p className="max-w-sm text-sm text-muted-foreground">
                            {status.isEnabled
                                ? 'Your account is protected with an authenticator app.'
                                : 'Add an extra layer of security to your account using Google Authenticator or any TOTP app.'}
                        </p>
                        {status.isEnabled ? (
                            <Button variant="outline" className="mt-2 text-destructive hover:text-destructive" onClick={() => { setPhase('disable'); setError(''); }}>
                                <ShieldOff /> Disable 2FA
                            </Button>
                        ) : (
                            <Button className="mt-2" onClick={startSetup} disabled={loading}>
                                {loading ? <Spinner /> : <ShieldCheck />}
                                {loading ? 'Loading...' : 'Set up 2FA'}
                            </Button>
                        )}
                    </CardContent>
                </Card>
            )}

            {/* ── SETUP: scan QR + enter code ── */}
            {phase === 'setup' && setup && (
                <Card>
                    <CardContent className="space-y-6">
                        <div className="space-y-3">
                            <StepTitle step={1}>Scan this QR code</StepTitle>
                            <p className="text-sm text-muted-foreground">Open Google Authenticator (or any TOTP app) and scan the code below.</p>
                            <div className="flex justify-center">
                                <div className="rounded-xl bg-white p-4 shadow-lg">
                                    <QRCodeSVG value={setup.authenticatorUri} size={200} />
                                </div>
                            </div>
                            <div className="text-center text-sm text-muted-foreground">
                                Can't scan? Enter this key manually:
                                <code className="mt-2 block rounded-md bg-muted px-3 py-2 font-mono text-base break-all text-foreground">
                                    {setup.sharedKey}
                                </code>
                            </div>
                        </div>

                        <Separator />

                        <div className="space-y-3">
                            <StepTitle step={2}>Verify the code</StepTitle>
                            <p className="text-sm text-muted-foreground">Enter the 6-digit code from the app to confirm setup.</p>
                            <form onSubmit={enableTwoFactor} className="space-y-3">
                                <CodeInput value={code} onChange={(e) => setCode(e.target.value.replace(/\D/g, ''))} />
                                <Button type="submit" variant="success" size="lg" className="w-full" disabled={loading || code.length !== 6}>
                                    {loading && <Spinner />}
                                    {loading ? 'Verifying...' : 'Enable 2FA'}
                                </Button>
                                <Button type="button" variant="ghost" className="w-full" onClick={() => setPhase('status')}>
                                    Cancel
                                </Button>
                            </form>
                        </div>
                    </CardContent>
                </Card>
            )}

            {/* ── RECOVERY CODES ── */}
            {phase === 'recovery' && (
                <Card className="border-success/40">
                    <CardContent className="space-y-5">
                        <div className="flex flex-col items-center gap-2 text-center">
                            <div className="flex size-16 items-center justify-center rounded-full bg-success/15 text-success">
                                <ShieldCheck className="size-8" />
                            </div>
                            <h2 className="text-xl font-semibold">2FA Enabled Successfully!</h2>
                        </div>
                        <Alert variant="warning">
                            <TriangleAlert />
                            <AlertTitle>Save these recovery codes!</AlertTitle>
                            <AlertDescription>
                                Each can be used once if you lose access to your authenticator app. Store them somewhere safe.
                            </AlertDescription>
                        </Alert>
                        <div className="grid grid-cols-2 gap-2 rounded-lg bg-zinc-950 p-4 font-mono text-sm text-zinc-100">
                            {recoveryCodes.map((c, i) => (
                                <div key={i} className="flex items-center gap-2">
                                    <KeyRound className="size-3.5 text-primary" />
                                    <code>{c}</code>
                                </div>
                            ))}
                        </div>
                        <Button size="lg" className="w-full" onClick={() => navigate('/profile')}>
                            Done
                        </Button>
                    </CardContent>
                </Card>
            )}

            {/* ── DISABLE ── */}
            {phase === 'disable' && (
                <Card className="border-destructive/40">
                    <CardContent className="space-y-4">
                        <div>
                            <h2 className="flex items-center gap-2 text-lg font-semibold text-destructive">
                                <ShieldOff className="size-5" /> Disable Two-Factor Authentication
                            </h2>
                            <p className="mt-1 text-sm text-muted-foreground">Enter the current code from your authenticator app to confirm.</p>
                        </div>
                        <form onSubmit={disableTwoFactor} className="space-y-3">
                            <CodeInput value={code} onChange={(e) => setCode(e.target.value.replace(/\D/g, ''))} />
                            <Button type="submit" variant="destructive" size="lg" className="w-full" disabled={loading || code.length !== 6}>
                                {loading && <Spinner />}
                                {loading ? 'Disabling...' : 'Disable 2FA'}
                            </Button>
                            <Button type="button" variant="ghost" className="w-full" onClick={() => { setPhase('status'); setError(''); }}>
                                Cancel
                            </Button>
                        </form>
                    </CardContent>
                </Card>
            )}
        </PageContainer>
    );
};

export default TwoFactorSetup;
