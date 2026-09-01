import { useState, useEffect, useRef, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import API_BASE_URL from '../config/api';
import Hls from 'hls.js';
import * as signalR from '@microsoft/signalr';

const WatchParty = () => {
    const { movieId } = useParams();
    const { token, user } = useAuth();
    const navigate = useNavigate();

    // Room state
    const [phase, setPhase] = useState('lobby'); // 'lobby' | 'in-room'
    const [roomCode, setRoomCode] = useState('');
    const [joinCodeInput, setJoinCodeInput] = useState('');
    const [isHost, setIsHost] = useState(false);
    const [members, setMembers] = useState([]);
    const [roomError, setRoomError] = useState('');
    const [isConnecting, setIsConnecting] = useState(false);
    const [codeCopied, setCodeCopied] = useState(false);
    const connectionRef = useRef(null);
    const roomCodeRef = useRef(''); // stable ref for event handlers

    // Video
    const videoRef = useRef(null);
    const hlsRef = useRef(null);
    const [streamingData, setStreamingData] = useState(null);
    const [movie, setMovie] = useState(null);
    const [videoError, setVideoError] = useState(null);
    const isSyncingRef = useRef(false); // prevents echo when applying remote sync

    // Chat
    const [messages, setMessages] = useState([]);
    const [chatInput, setChatInput] = useState('');
    const chatEndRef = useRef(null);

    // Fetch movie info and streaming URL
    useEffect(() => {
        if (!token || !movieId) return;
        const load = async () => {
            try {
                const [movieRes, streamRes] = await Promise.all([
                    fetch(`${API_BASE_URL}/api/Movie/${movieId}`),
                    fetch(`${API_BASE_URL}/api/Movie/${movieId}/stream`, {
                        headers: { 'Authorization': `Bearer ${token}` }
                    })
                ]);
                if (movieRes.ok) setMovie(await movieRes.json());
                if (streamRes.ok) {
                    setStreamingData(await streamRes.json());
                } else {
                    setVideoError('You need to purchase this movie to watch it.');
                }
            } catch {
                setVideoError('Failed to load movie.');
            }
        };
        load();
    }, [movieId, token]);

    // Setup HLS/MP4 video player - phase is a dependency because the video element
    // only renders when phase === 'in-room', so we need to re-run when it appears
    useEffect(() => {
        if (!streamingData || !videoRef.current || phase !== 'in-room') return;
        const video = videoRef.current;

        if (hlsRef.current) {
            hlsRef.current.destroy();
            hlsRef.current = null;
        }

        if (streamingData.isHls && Hls.isSupported()) {
            const hls = new Hls({
                xhrSetup: (xhr, url) => {
                    if (url.includes('/api/Movie/')) {
                        xhr.setRequestHeader('Authorization', `Bearer ${token}`);
                    }
                }
            });
            hlsRef.current = hls;
            hls.loadSource(streamingData.url);
            hls.attachMedia(video);
        } else if (streamingData.url) {
            video.src = streamingData.url;
        }

        return () => {
            if (hlsRef.current) {
                hlsRef.current.destroy();
                hlsRef.current = null;
            }
        };
    }, [streamingData, token, phase]);

    // Host: broadcast play/pause/seek events to all guests
    useEffect(() => {
        if (phase !== 'in-room' || !isHost || !videoRef.current) return;
        const video = videoRef.current;
        const conn = connectionRef.current;
        const code = roomCodeRef.current;

        const onPlay = () => {
            if (isSyncingRef.current || !conn) return;
            conn.invoke('Play', code, video.currentTime).catch(console.error);
        };
        const onPause = () => {
            if (isSyncingRef.current || !conn) return;
            conn.invoke('Pause', code, video.currentTime).catch(console.error);
        };
        const onSeeked = () => {
            if (isSyncingRef.current || !conn) return;
            conn.invoke('Seek', code, video.currentTime).catch(console.error);
        };

        video.addEventListener('play', onPlay);
        video.addEventListener('pause', onPause);
        video.addEventListener('seeked', onSeeked);

        return () => {
            video.removeEventListener('play', onPlay);
            video.removeEventListener('pause', onPause);
            video.removeEventListener('seeked', onSeeked);
        };
    }, [phase, isHost]);

    // Auto-scroll chat
    useEffect(() => {
        chatEndRef.current?.scrollIntoView({ behavior: 'smooth' });
    }, [messages]);

    const addSystemMsg = useCallback((text) => {
        setMessages(prev => [...prev, { type: 'system', text, ts: Date.now() }]);
    }, []);

    // Apply a remote sync to the video (with isSyncing guard to prevent echo)
    const applySync = useCallback((fn) => {
        isSyncingRef.current = true;
        fn();
        setTimeout(() => { isSyncingRef.current = false; }, 500);
    }, []);

    // Register all SignalR event handlers on the connection
    const setupHandlers = useCallback((conn) => {
        conn.on('UserJoined', (name) => {
            setMembers(prev => [...new Set([...prev, name])]);
            addSystemMsg(`${name} joined the party`);
        });

        conn.on('UserLeft', (name) => {
            setMembers(prev => prev.filter(m => m !== name));
            addSystemMsg(`${name} left the party`);
        });

        conn.on('HostChanged', (newHostName) => {
            addSystemMsg(`${newHostName} is now the host`);
        });

        // Received on join: syncs position + member list
        conn.on('RoomState', (state) => {
            setMembers(state.members);
            const video = videoRef.current;
            if (!video || state.position <= 0) return;
            applySync(() => {
                if (state.isPlaying) {
                    const delaySec = (Date.now() - state.serverTime) / 1000;
                    video.currentTime = state.position + delaySec;
                    video.play().catch(() => {});
                } else {
                    video.currentTime = state.position;
                }
            });
        });

        // Host started playback - latency compensated
        conn.on('Play', (position, serverTimestamp) => {
            const video = videoRef.current;
            if (!video) return;
            applySync(() => {
                const delaySec = (Date.now() - serverTimestamp) / 1000;
                video.currentTime = position + delaySec;
                video.play().catch(() => {});
            });
        });

        // Host paused
        conn.on('Pause', (position) => {
            const video = videoRef.current;
            if (!video) return;
            applySync(() => {
                video.currentTime = position;
                video.pause();
            });
        });

        // Host seeked - latency compensated
        conn.on('Seek', (position, serverTimestamp) => {
            const video = videoRef.current;
            if (!video) return;
            applySync(() => {
                const delaySec = (Date.now() - serverTimestamp) / 1000;
                video.currentTime = position + delaySec;
            });
        });

        conn.on('ChatMessage', (userName, message) => {
            setMessages(prev => [...prev, { type: 'chat', userName, message, ts: Date.now() }]);
        });
    }, [addSystemMsg, applySync]);

    const buildConnection = useCallback(() => {
        return new signalR.HubConnectionBuilder()
            .withUrl(`${API_BASE_URL}/hubs/watchparty`, {
                accessTokenFactory: () => token
            })
            .withAutomaticReconnect()
            .configureLogging(signalR.LogLevel.Warning)
            .build();
    }, [token]);

    const createRoom = async () => {
        setRoomError('');
        setIsConnecting(true);
        try {
            const conn = buildConnection();
            setupHandlers(conn);
            await conn.start();
            connectionRef.current = conn;

            const code = await conn.invoke('CreateRoom', parseInt(movieId));
            roomCodeRef.current = code;
            setRoomCode(code);
            setIsHost(true);
            setMembers([user?.name || user?.email || 'You (Host)']);
            addSystemMsg(`Room created! Share code: ${code}`);
            setPhase('in-room');
        } catch (e) {
            setRoomError('Failed to create room: ' + (e.message || 'Connection error'));
            if (connectionRef.current) {
                await connectionRef.current.stop().catch(() => {});
                connectionRef.current = null;
            }
        } finally {
            setIsConnecting(false);
        }
    };

    const joinRoom = async () => {
        if (!joinCodeInput.trim()) return;
        setRoomError('');
        setIsConnecting(true);
        try {
            const conn = buildConnection();
            setupHandlers(conn);
            await conn.start();
            connectionRef.current = conn;

            const code = joinCodeInput.toUpperCase().trim();
            await conn.invoke('JoinRoom', code, user?.name || user?.email || 'Guest');
            roomCodeRef.current = code;
            setRoomCode(code);
            setIsHost(false);
            setPhase('in-room');
        } catch (e) {
            setRoomError('Failed to join room: ' + (e.message || 'Room not found'));
            if (connectionRef.current) {
                await connectionRef.current.stop().catch(() => {});
                connectionRef.current = null;
            }
        } finally {
            setIsConnecting(false);
        }
    };

    const leaveRoom = async () => {
        if (connectionRef.current) {
            await connectionRef.current.stop().catch(() => {});
            connectionRef.current = null;
        }
        roomCodeRef.current = '';
        setPhase('lobby');
        setRoomCode('');
        setMembers([]);
        setMessages([]);
        setIsHost(false);
    };

    const sendChat = async (e) => {
        e.preventDefault();
        const msg = chatInput.trim();
        if (!msg || !connectionRef.current) return;
        try {
            await connectionRef.current.invoke('SendMessage', roomCodeRef.current, msg);
            setChatInput('');
        } catch (e) {
            console.error('Chat error:', e);
        }
    };

    // Re-sync guest to current host position
    const resyncToHost = async () => {
        const conn = connectionRef.current;
        if (!conn) return;
        try {
            const state = await conn.invoke('GetRoomState', roomCodeRef.current);
            if (state && videoRef.current) {
                applySync(() => {
                    const delaySec = (Date.now() - state.serverTime) / 1000;
                    videoRef.current.currentTime = state.position + (state.isPlaying ? delaySec : 0);
                    if (state.isPlaying) videoRef.current.play().catch(() => {});
                    else videoRef.current.pause();
                });
            }
        } catch (e) {
            console.error('Re-sync error:', e);
        }
    };

    const copyCode = () => {
        navigator.clipboard.writeText(roomCode).catch(() => {});
        setCodeCopied(true);
        setTimeout(() => setCodeCopied(false), 2000);
    };

    // ─── LOBBY ────────────────────────────────────────────────────────────────
    if (phase === 'lobby') {
        return (
            <div className="container mt-5">
                <div className="row justify-content-center">
                    <div className="col-md-6 col-lg-5">
                        <button onClick={() => navigate('/my-movies')} className="btn btn-outline-secondary btn-sm mb-3">
                            <i className="bi bi-arrow-left me-1"></i> Back to My Movies
                        </button>

                        <div className="card shadow-sm">
                            <div className="card-body p-4">
                                <h3 className="card-title mb-1">
                                    <i className="bi bi-people-fill me-2 text-primary"></i>
                                    Watch Party
                                </h3>
                                {movie && <p className="text-muted mb-4 small">{movie.title}</p>}

                                {videoError && <div className="alert alert-danger">{videoError}</div>}
                                {roomError && <div className="alert alert-danger">{roomError}</div>}

                                <div className="d-grid mb-3">
                                    <button
                                        className="btn btn-primary btn-lg"
                                        onClick={createRoom}
                                        disabled={isConnecting || !!videoError}
                                    >
                                        {isConnecting ? (
                                            <><span className="spinner-border spinner-border-sm me-2" />Connecting...</>
                                        ) : (
                                            <><i className="bi bi-plus-circle me-2" />Start Watch Party</>
                                        )}
                                    </button>
                                </div>

                                <div className="text-center text-muted mb-3 small">— or join with a code —</div>

                                <div className="input-group">
                                    <input
                                        type="text"
                                        className="form-control form-control-lg text-uppercase text-center"
                                        placeholder="XXXXXX"
                                        value={joinCodeInput}
                                        onChange={e => setJoinCodeInput(e.target.value.toUpperCase())}
                                        onKeyDown={e => e.key === 'Enter' && joinRoom()}
                                        maxLength={6}
                                        style={{ letterSpacing: '0.25em', fontWeight: 'bold' }}
                                        disabled={isConnecting}
                                    />
                                    <button
                                        className="btn btn-outline-primary"
                                        onClick={joinRoom}
                                        disabled={isConnecting || !joinCodeInput.trim() || !!videoError}
                                    >
                                        Join
                                    </button>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        );
    }

    // ─── IN-ROOM ──────────────────────────────────────────────────────────────
    return (
        <div className="bg-dark text-white" style={{ minHeight: '100vh' }}>
            {/* Room header bar */}
            <div className="container-fluid py-2 border-bottom border-secondary bg-black">
                <div className="container d-flex align-items-center justify-content-between flex-wrap gap-2">
                    <div className="d-flex align-items-center gap-2 flex-wrap">
                        <span className="text-muted small">Room:</span>
                        <span className="badge bg-primary fs-6 px-3 py-2" style={{ letterSpacing: '0.2em' }}>
                            {roomCode}
                        </span>
                        <button className="btn btn-sm btn-outline-light" onClick={copyCode}>
                            <i className={`bi ${codeCopied ? 'bi-check-lg' : 'bi-clipboard'} me-1`} />
                            {codeCopied ? 'Copied!' : 'Copy'}
                        </button>
                        {isHost && (
                            <span className="badge bg-warning text-dark">
                                <i className="bi bi-crown-fill me-1" />Host
                            </span>
                        )}
                    </div>
                    <div className="d-flex align-items-center gap-3">
                        <span className="text-muted small">
                            <i className="bi bi-people me-1" />{members.length} watching
                        </span>
                        <button className="btn btn-sm btn-outline-danger" onClick={leaveRoom}>
                            <i className="bi bi-box-arrow-left me-1" />Leave
                        </button>
                    </div>
                </div>
            </div>

            {/* Main content */}
            <div className="container-fluid py-3 px-3">
                <div className="row g-3" style={{ minHeight: 'calc(100vh - 60px)' }}>

                    {/* Video column */}
                    <div className="col-lg-9">
                        {!isHost && (
                            <div className="alert alert-info alert-dismissible py-2 mb-2 d-flex align-items-center justify-content-between">
                                <span className="small">
                                    <i className="bi bi-info-circle me-2" />
                                    Host controls playback. You can seek locally.
                                </span>
                                <button className="btn btn-sm btn-outline-info" onClick={resyncToHost}>
                                    <i className="bi bi-arrow-clockwise me-1" />Re-sync
                                </button>
                            </div>
                        )}

                        <div className="ratio ratio-16x9 rounded overflow-hidden bg-black">
                            <video ref={videoRef} controls className="w-100 h-100" />
                        </div>

                        {movie && (
                            <h5 className="mt-3 mb-0">{movie.title}</h5>
                        )}
                    </div>

                    {/* Sidebar: members + chat */}
                    <div className="col-lg-3 d-flex flex-column" style={{ maxHeight: 'calc(100vh - 70px)' }}>
                        {/* Members */}
                        <div className="card bg-secondary text-white mb-2">
                            <div className="card-body py-2 px-3">
                                <div className="d-flex align-items-center mb-2">
                                    <i className="bi bi-people me-2 text-info" />
                                    <span className="fw-bold small">Viewers ({members.length})</span>
                                </div>
                                {members.map((m, i) => (
                                    <div key={i} className="d-flex align-items-center gap-2 small mb-1">
                                        <i className="bi bi-person-circle text-info" />
                                        <span className="text-truncate">{m}</span>
                                    </div>
                                ))}
                            </div>
                        </div>

                        {/* Chat */}
                        <div className="card bg-secondary text-white flex-grow-1 d-flex flex-column" style={{ minHeight: 0 }}>
                            <div className="card-header py-2 px-3 small fw-bold border-0">
                                <i className="bi bi-chat-dots me-2 text-info" />Chat
                            </div>

                            <div className="flex-grow-1 overflow-auto px-3 py-2" style={{ fontSize: '0.82rem' }}>
                                {messages.map((msg, i) => (
                                    msg.type === 'system' ? (
                                        <div key={i} className="text-center text-muted fst-italic my-1" style={{ fontSize: '0.75rem' }}>
                                            {msg.text}
                                        </div>
                                    ) : (
                                        <div key={i} className="mb-1">
                                            <span className="fw-semibold text-info">{msg.userName}: </span>
                                            <span className="text-white-50">{msg.message}</span>
                                        </div>
                                    )
                                ))}
                                <div ref={chatEndRef} />
                            </div>

                            <div className="card-footer p-2 border-0">
                                <form onSubmit={sendChat} className="d-flex gap-2">
                                    <input
                                        type="text"
                                        className="form-control form-control-sm bg-dark text-white border-secondary"
                                        placeholder="Send a message..."
                                        value={chatInput}
                                        onChange={e => setChatInput(e.target.value)}
                                        maxLength={200}
                                    />
                                    <button type="submit" className="btn btn-sm btn-primary px-2">
                                        <i className="bi bi-send-fill" />
                                    </button>
                                </form>
                            </div>
                        </div>
                    </div>

                </div>
            </div>
        </div>
    );
};

export default WatchParty;
