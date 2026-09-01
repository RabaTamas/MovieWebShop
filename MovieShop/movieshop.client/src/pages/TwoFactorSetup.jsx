import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { QRCodeSVG } from 'qrcode.react';
import { useAuth } from '../contexts/AuthContext';
import API_BASE_URL from '../config/api';

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

    if (!status) return <div className="container mt-5 text-center"><div className="spinner-border" /></div>;

    return (
        <div className="container mt-5">
            <div className="row justify-content-center">
                <div className="col-md-7 col-lg-6">
                    <div className="d-flex align-items-center mb-4 gap-2">
                        <button className="btn btn-outline-secondary btn-sm" onClick={() => navigate('/profile')}>
                            <i className="bi bi-arrow-left" /> Back
                        </button>
                        <h4 className="mb-0">Two-Factor Authentication</h4>
                    </div>

                    {error && <div className="alert alert-danger">{error}</div>}

                    {/* ── STATUS ── */}
                    {phase === 'status' && (
                        <div className="card shadow-sm">
                            <div className="card-body p-4 text-center">
                                <i className={`bi bi-shield-${status.isEnabled ? 'check' : 'x'} display-4 ${status.isEnabled ? 'text-success' : 'text-secondary'}`} />
                                <h5 className="mt-3">
                                    2FA is currently{' '}
                                    <span className={status.isEnabled ? 'text-success' : 'text-danger'}>
                                        {status.isEnabled ? 'enabled' : 'disabled'}
                                    </span>
                                </h5>
                                <p className="text-muted small mt-2">
                                    {status.isEnabled
                                        ? 'Your account is protected with an authenticator app.'
                                        : 'Add an extra layer of security to your account using Google Authenticator or any TOTP app.'}
                                </p>
                                {status.isEnabled ? (
                                    <button className="btn btn-outline-danger" onClick={() => { setPhase('disable'); setError(''); }}>
                                        Disable 2FA
                                    </button>
                                ) : (
                                    <button className="btn btn-primary" onClick={startSetup} disabled={loading}>
                                        {loading ? 'Loading...' : 'Set up 2FA'}
                                    </button>
                                )}
                            </div>
                        </div>
                    )}

                    {/* ── SETUP: scan QR + enter code ── */}
                    {phase === 'setup' && setup && (
                        <div className="card shadow-sm">
                            <div className="card-body p-4">
                                <h5>Step 1 – Scan this QR code</h5>
                                <p className="text-muted small">Open Google Authenticator (or any TOTP app) and scan the code below.</p>
                                <div className="text-center my-3">
                                    <QRCodeSVG value={setup.authenticatorUri} size={200} />
                                </div>
                                <p className="text-muted small text-center">
                                    Can't scan? Enter this key manually:
                                    <br />
                                    <code className="fs-6">{setup.sharedKey}</code>
                                </p>

                                <hr />

                                <h5>Step 2 – Verify the code</h5>
                                <p className="text-muted small">Enter the 6-digit code from the app to confirm setup.</p>
                                <form onSubmit={enableTwoFactor}>
                                    <div className="mb-3">
                                        <input
                                            type="text"
                                            className="form-control text-center fs-5"
                                            placeholder="000000"
                                            maxLength={6}
                                            value={code}
                                            onChange={(e) => setCode(e.target.value.replace(/\D/g, ''))}
                                            autoFocus
                                        />
                                    </div>
                                    <button type="submit" className="btn btn-success w-100" disabled={loading || code.length !== 6}>
                                        {loading ? 'Verifying...' : 'Enable 2FA'}
                                    </button>
                                    <button type="button" className="btn btn-link w-100 mt-1" onClick={() => setPhase('status')}>
                                        Cancel
                                    </button>
                                </form>
                            </div>
                        </div>
                    )}

                    {/* ── RECOVERY CODES ── */}
                    {phase === 'recovery' && (
                        <div className="card shadow-sm border-success">
                            <div className="card-body p-4">
                                <div className="text-center mb-3">
                                    <i className="bi bi-shield-check display-4 text-success" />
                                    <h5 className="mt-2">2FA Enabled Successfully!</h5>
                                </div>
                                <div className="alert alert-warning">
                                    <strong>Save these recovery codes!</strong> Each can be used once if you lose access to your authenticator app. Store them somewhere safe.
                                </div>
                                <div className="bg-dark text-light rounded p-3 mb-3">
                                    <div className="row row-cols-2 g-1">
                                        {recoveryCodes.map((c, i) => (
                                            <div key={i} className="col">
                                                <code className="fs-6">{c}</code>
                                            </div>
                                        ))}
                                    </div>
                                </div>
                                <button className="btn btn-primary w-100" onClick={() => navigate('/profile')}>
                                    Done
                                </button>
                            </div>
                        </div>
                    )}

                    {/* ── DISABLE ── */}
                    {phase === 'disable' && (
                        <div className="card shadow-sm border-danger">
                            <div className="card-body p-4">
                                <h5 className="text-danger">Disable Two-Factor Authentication</h5>
                                <p className="text-muted small">Enter the current code from your authenticator app to confirm.</p>
                                <form onSubmit={disableTwoFactor}>
                                    <div className="mb-3">
                                        <input
                                            type="text"
                                            className="form-control text-center fs-5"
                                            placeholder="000000"
                                            maxLength={6}
                                            value={code}
                                            onChange={(e) => setCode(e.target.value.replace(/\D/g, ''))}
                                            autoFocus
                                        />
                                    </div>
                                    <button type="submit" className="btn btn-danger w-100" disabled={loading || code.length !== 6}>
                                        {loading ? 'Disabling...' : 'Disable 2FA'}
                                    </button>
                                    <button type="button" className="btn btn-link w-100 mt-1" onClick={() => { setPhase('status'); setError(''); }}>
                                        Cancel
                                    </button>
                                </form>
                            </div>
                        </div>
                    )}
                </div>
            </div>
        </div>
    );
};

export default TwoFactorSetup;
