import { useState, useEffect, useRef, useCallback } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import {
    AlertCircle, ArrowLeft, Check, Copy, Crown, Info, LogOut, MessageCircle, PartyPopper, Plus, RefreshCw,
    Send, UserRound, Users,
} from 'lucide-react';
import { useAuth } from '../contexts/AuthContext';
import API_BASE_URL from '../config/api';
import Hls from 'hls.js';
import * as signalR from '@microsoft/signalr';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Spinner } from '@/components/ui/spinner';

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
        chatEndRef.current?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
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
            <div className="relative flex flex-1 items-center justify-center overflow-hidden px-4 py-12">
                {movie?.imageUrl && (
                    <div
                        className="absolute inset-0 scale-110 bg-cover bg-center opacity-25 blur-3xl"
                        style={{ backgroundImage: `url(${movie.imageUrl})` }}
                        aria-hidden="true"
                    />
                )}
                <div className="relative w-full max-w-md">
                    <Button variant="ghost" size="sm" className="mb-4" onClick={() => navigate('/my-movies')}>
                        <ArrowLeft /> Back to My Movies
                    </Button>

                    <div className="overflow-hidden rounded-2xl border bg-card/90 shadow-2xl backdrop-blur">
                        <div className="flex items-center gap-4 border-b p-6">
                            <div className="flex size-12 shrink-0 items-center justify-center rounded-xl bg-primary text-primary-foreground shadow-lg shadow-primary/30">
                                <PartyPopper className="size-6" />
                            </div>
                            <div className="min-w-0">
                                <h1 className="text-2xl font-bold tracking-tight">Watch Party</h1>
                                {movie && <p className="truncate text-sm text-muted-foreground">{movie.title}</p>}
                            </div>
                        </div>

                        <div className="space-y-5 p-6">
                            {videoError && (
                                <Alert variant="destructive"><AlertCircle /><AlertDescription>{videoError}</AlertDescription></Alert>
                            )}
                            {roomError && (
                                <Alert variant="destructive"><AlertCircle /><AlertDescription>{roomError}</AlertDescription></Alert>
                            )}

                            <Button size="lg" className="h-12 w-full text-base" onClick={createRoom} disabled={isConnecting || !!videoError}>
                                {isConnecting ? <><Spinner />Connecting...</> : <><Plus />Start Watch Party</>}
                            </Button>

                            <div className="flex items-center gap-3 text-xs tracking-wider text-muted-foreground uppercase">
                                <span className="h-px flex-1 bg-border" />
                                or join with a code
                                <span className="h-px flex-1 bg-border" />
                            </div>

                            <div className="flex gap-2">
                                <Input
                                    type="text"
                                    className="h-12 text-center font-mono text-lg font-bold tracking-[0.35em] uppercase md:text-lg"
                                    placeholder="XXXXXX"
                                    aria-label="Room code"
                                    value={joinCodeInput}
                                    onChange={e => setJoinCodeInput(e.target.value.toUpperCase())}
                                    onKeyDown={e => e.key === 'Enter' && joinRoom()}
                                    maxLength={6}
                                    disabled={isConnecting}
                                />
                                <Button
                                    variant="secondary"
                                    className="h-12 px-6"
                                    onClick={joinRoom}
                                    disabled={isConnecting || !joinCodeInput.trim() || !!videoError}
                                >
                                    Join
                                </Button>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        );
    }

    // ─── IN-ROOM ──────────────────────────────────────────────────────────────
    return (
        // A szoba nézet mindig sötét, a választott témától függetlenül
        <div className="dark flex flex-1 flex-col bg-background text-foreground">
            {/* Room header bar */}
            <div className="border-b bg-black/40">
                <div className="mx-auto flex max-w-[1600px] flex-wrap items-center justify-between gap-2 px-4 py-2 sm:px-6">
                    <div className="flex flex-wrap items-center gap-2">
                        <span className="text-sm text-muted-foreground">Room:</span>
                        <span className="rounded-md bg-primary px-3 py-1 font-mono text-base font-bold tracking-[0.25em] text-primary-foreground">
                            {roomCode}
                        </span>
                        <Button size="sm" variant="outline" onClick={copyCode}>
                            {codeCopied ? <Check /> : <Copy />}
                            {codeCopied ? 'Copied!' : 'Copy'}
                        </Button>
                        {isHost && (
                            <Badge variant="warning" className="px-2 py-1">
                                <Crown />Host
                            </Badge>
                        )}
                    </div>
                    <div className="flex items-center gap-3">
                        <span className="flex items-center gap-1.5 text-sm text-muted-foreground">
                            <Users className="size-4" />{members.length} watching
                        </span>
                        <Button size="sm" variant="outline" className="text-destructive hover:text-destructive" onClick={leaveRoom}>
                            <LogOut />Leave
                        </Button>
                    </div>
                </div>
            </div>

            {/* Main content */}
            <div className="mx-auto grid w-full max-w-[1600px] flex-1 gap-4 p-4 sm:px-6 lg:grid-cols-[1fr_320px]">
                {/* Video column */}
                <div className="min-w-0 space-y-3">
                    {!isHost && (
                        <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-sky-500/30 bg-sky-500/10 px-3 py-2 text-sm">
                            <span className="flex items-center gap-2">
                                <Info className="size-4 text-sky-400" />
                                Host controls playback. You can seek locally.
                            </span>
                            <Button size="sm" variant="outline" onClick={resyncToHost}>
                                <RefreshCw />Re-sync
                            </Button>
                        </div>
                    )}

                    <div className="aspect-video w-full overflow-hidden rounded-xl bg-black shadow-2xl ring-1 ring-white/10">
                        <video ref={videoRef} controls className="size-full" />
                    </div>

                    {movie && (
                        <h2 className="font-display text-3xl tracking-wide">{movie.title}</h2>
                    )}
                </div>

                {/* Sidebar: members + chat */}
                <div className="flex min-h-[420px] flex-col gap-3 lg:max-h-[calc(100vh-10rem)]">
                    {/* Members */}
                    <div className="rounded-xl border bg-card p-3">
                        <div className="mb-2 flex items-center gap-2 text-sm font-semibold">
                            <Users className="size-4 text-primary" />
                            Viewers ({members.length})
                        </div>
                        <div className="flex flex-wrap gap-1.5">
                            {members.map((m, i) => (
                                <span key={i} className="inline-flex max-w-full items-center gap-1.5 rounded-full bg-muted px-2.5 py-1 text-xs">
                                    <UserRound className="size-3 text-primary" />
                                    <span className="truncate">{m}</span>
                                </span>
                            ))}
                        </div>
                    </div>

                    {/* Chat */}
                    <div className="flex min-h-0 flex-1 flex-col rounded-xl border bg-card">
                        <div className="flex items-center gap-2 border-b px-3 py-2 text-sm font-semibold">
                            <MessageCircle className="size-4 text-primary" />Chat
                        </div>

                        <div className="min-h-0 flex-1 space-y-1.5 overflow-y-auto px-3 py-2 text-sm">
                            {messages.map((msg, i) => (
                                msg.type === 'system' ? (
                                    <div key={i} className="py-0.5 text-center text-xs text-muted-foreground italic">
                                        {msg.text}
                                    </div>
                                ) : (
                                    <div key={i} className="break-words">
                                        <span className="font-semibold text-primary">{msg.userName}: </span>
                                        <span className="text-foreground/80">{msg.message}</span>
                                    </div>
                                )
                            ))}
                            <div ref={chatEndRef} />
                        </div>

                        <form onSubmit={sendChat} className="flex gap-2 border-t p-2">
                            <Input
                                type="text"
                                className="h-8"
                                placeholder="Send a message..."
                                aria-label="Chat message"
                                value={chatInput}
                                onChange={e => setChatInput(e.target.value)}
                                maxLength={200}
                            />
                            <Button type="submit" size="icon-sm" aria-label="Send">
                                <Send />
                            </Button>
                        </form>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default WatchParty;
