import { useState, useEffect, useRef } from 'react';
import { useParams, useNavigate, Link } from 'react-router-dom';
import {
    ArrowLeft, Clock, Film, Gauge, Info, PlayCircle, RotateCcw, TriangleAlert, Users, Wifi, X, Zap,
} from 'lucide-react';
import { useAuth } from '../contexts/AuthContext';
import API_BASE_URL from '../config/api';
import Hls from 'hls.js';
import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Separator } from '@/components/ui/separator';
import { LoadingState } from '@/components/ui/spinner';
import { PageContainer } from '@/components/ui/page';

const WatchMovie = () => {
    const { movieId } = useParams();
    const { token } = useAuth();
    const navigate = useNavigate();
    const videoRef = useRef(null);
    const hlsRef = useRef(null);

    const [movie, setMovie] = useState(null);
    const [trailerData, setTrailerData] = useState(null);
    const [streamingData, setStreamingData] = useState(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [hlsQualityLevels, setHlsQualityLevels] = useState([]);
    const [selectedHlsLevel, setSelectedHlsLevel] = useState(-1); // -1 = auto
    const [savedProgress, setSavedProgress] = useState(0);
    const [showResumeToast, setShowResumeToast] = useState(false);
    const saveIntervalRef = useRef(null);
    const savedProgressRef = useRef(0); // ref hogy ne triggerelődjön újra a video setup

    useEffect(() => {
        const fetchMovieAndVideo = async () => {
            try {
                // Fetch movie details
                const movieResponse = await fetch(`${API_BASE_URL}/api/Movie/${movieId}`);
                if (!movieResponse.ok) {
                    throw new Error('Movie not found');
                }
                const movieData = await movieResponse.json();
                setMovie(movieData);

                // Try to fetch streaming URL first (Phase 3: Azure Blob + HLS)
                if (movieData.videoFileName) {
                    try {
                        const streamingResponse = await fetch(`${API_BASE_URL}/api/Movie/${movieId}/stream`, {
                            headers: {
                                'Authorization': `Bearer ${token}`
                            }
                        });

                        if (streamingResponse.ok) {
                            const streamData = await streamingResponse.json();
                            setStreamingData(streamData);
                            return; // Use streaming if available
                        }
                    } catch {
                        console.log('Streaming not available, falling back to trailer');
                    }
                }

                // Fallback to trailer (Phase 1)
                const trailerResponse = await fetch(`${API_BASE_URL}/api/Movie/${movieId}/trailer`, {
                    headers: {
                        'Authorization': `Bearer ${token}`
                    }
                });

                if (trailerResponse.status === 403) {
                    const data = await trailerResponse.json();
                    setError(data.message || 'You need to purchase this movie to watch it.');
                    return;
                }

                if (!trailerResponse.ok) {
                    const data = await trailerResponse.json();
                    throw new Error(data.message || 'Failed to load video');
                }

                const data = await trailerResponse.json();
                setTrailerData(data);
            } catch (err) {
                setError(err.message);
            } finally {
                setLoading(false);
            }
        };

        if (token && movieId) {
            fetchMovieAndVideo();
        }
    }, [movieId, token]);

    // Mentett pozíció lekérése
    useEffect(() => {
        if (!token || !movieId) return;

        const fetchProgress = async () => {
            try {
                const response = await fetch(`${API_BASE_URL}/api/Movie/${movieId}/progress`, {
                    headers: { 'Authorization': `Bearer ${token}` }
                });
                if (response.ok) {
                    const data = await response.json();
                    if (data.progressSeconds > 5) {
                        setSavedProgress(data.progressSeconds);
                        savedProgressRef.current = data.progressSeconds; // ref frissítés
                        setShowResumeToast(true);
                        // Ha a video már betöltődött mire a progress megérkezett (race condition fix)
                        const video = videoRef.current;
                        if (video && video.readyState >= 1) {
                            video.currentTime = data.progressSeconds;
                        }
                    }
                }
            } catch {
                // Nem kritikus hiba, csendben kezeljük
            }
        };

        fetchProgress();
    }, [movieId, token]);

    // Progress mentés helper - keepalive:true biztosítja hogy navigáláskor is lefut
    const saveProgress = (useKeepalive = false) => {
        const video = videoRef.current;
        if (!video || video.currentTime < 1) return;

        fetch(`${API_BASE_URL}/api/Movie/${movieId}/progress`, {
            method: 'POST',
            headers: {
                'Authorization': `Bearer ${token}`,
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ progressSeconds: video.currentTime }),
            keepalive: useKeepalive // navigáláskor ne szakadjon meg a request
        }).catch(() => {}); // Nem kritikus hiba
    };

    // Video player setup - HLS or MP4
    useEffect(() => {
        if (!streamingData || !videoRef.current) return;

        const video = videoRef.current;

        // Cleanup previous HLS instance
        if (hlsRef.current) {
            hlsRef.current.destroy();
            hlsRef.current = null;
        }

        // HLS Adaptive Streaming (Phase 3)
        if (streamingData.isHls) {
            if (Hls.isSupported()) {
                const hls = new Hls({
                    enableWorker: true,
                    lowLatencyMode: false,
                    backBufferLength: 90,
                    xhrSetup: function(xhr, url) {
                        // Add authorization header for all backend requests
                        if (url.includes('/api/Movie/')) {
                            xhr.setRequestHeader('Authorization', `Bearer ${token}`);
                        }
                    }
                });

                hlsRef.current = hls;
                hls.loadSource(streamingData.url);
                hls.attachMedia(video);

                hls.on(Hls.Events.MANIFEST_PARSED, () => {
                    console.log('HLS manifest loaded, adaptive streaming ready');
                    console.log('Available quality levels:', hls.levels.map(l => `${l.height}p`));
                    setHlsQualityLevels(hls.levels);
                    video.play().catch(err => console.log('Autoplay prevented:', err));
                });

                hls.on(Hls.Events.ERROR, (event, data) => {
                    if (data.fatal) {
                        console.error('HLS fatal error:', data);
                        switch (data.type) {
                            case Hls.ErrorTypes.NETWORK_ERROR:
                                console.log('Network error, trying to recover...');
                                hls.startLoad();
                                break;
                            case Hls.ErrorTypes.MEDIA_ERROR:
                                console.log('Media error, trying to recover...');
                                hls.recoverMediaError();
                                break;
                            default:
                                hls.destroy();
                                break;
                        }
                    }
                });

                hls.on(Hls.Events.LEVEL_SWITCHED, (event, data) => {
                    const level = hls.levels[data.level];
                    console.log(`Quality switched to: ${level.height}p (${Math.round(level.bitrate / 1000)}kbps)`);
                });

            } else if (video.canPlayType('application/vnd.apple.mpegurl')) {
                // Native HLS support (Safari)
                video.src = streamingData.url;
                video.addEventListener('loadedmetadata', () => {
                    console.log('HLS loaded via native support (Safari)');
                    video.play().catch(err => console.log('Autoplay prevented:', err));
                });
            } else {
                console.error('HLS is not supported in this browser');
            }
        }
        // Legacy MP4 with manual quality selection
        else if (streamingData.availableQualities) {
            const currentTime = video.currentTime;
            const wasPlaying = !video.paused;

            // Use default URL (fallback to first available quality)
            const videoUrl = streamingData.url;

            video.src = videoUrl;
            video.currentTime = currentTime;

            if (wasPlaying) {
                video.play().catch(err => console.log('Autoplay prevented:', err));
            }

            console.log('Available video qualities:', Object.keys(streamingData.availableQualities));
            console.log('Playing video');

            video.load();
        }
        // Simple single MP4
        else {
            video.src = streamingData.url;
            video.load();
        }

        // Pozíció visszaállítás - savedProgressRef-et olvas, nem state-et!
        const onMetadataLoaded = () => {
            if (savedProgressRef.current > 5) {
                video.currentTime = savedProgressRef.current;
            }
        };

        const onPause = () => {
            saveProgress();
        };

        video.addEventListener('loadedmetadata', onMetadataLoaded);
        video.addEventListener('pause', onPause);

        // 30 másodpercenként automatikus mentés
        saveIntervalRef.current = setInterval(() => {
            if (!video.paused) saveProgress();
        }, 30000);

        return () => {
            if (hlsRef.current) {
                hlsRef.current.destroy();
                hlsRef.current = null;
            }
            video.removeEventListener('loadedmetadata', onMetadataLoaded);
            video.removeEventListener('pause', onPause);
            clearInterval(saveIntervalRef.current);
            saveProgress(true); // keepalive=true: navigáláskor is lefut
            video.pause();
        };
    }, [streamingData, token]); // savedProgress NINCS itt - ref-et használunk helyette

    // Handle HLS manual quality selection
    useEffect(() => {
        if (hlsRef.current && hlsQualityLevels.length > 0) {
            if (selectedHlsLevel === -1) {
                // Auto mode
                hlsRef.current.currentLevel = -1;
                console.log('Quality mode: Auto (adaptive)');
            } else {
                // Manual mode
                hlsRef.current.currentLevel = selectedHlsLevel;
                const level = hlsQualityLevels[selectedHlsLevel];
                console.log(`Quality mode: Manual - ${level.height}p locked`);
            }
        }
    }, [selectedHlsLevel, hlsQualityLevels]);

    if (loading) {
        return <LoadingState label="Loading movie..." />;
    }

    if (error) {
        return (
            <PageContainer size="md">
                <Alert variant="destructive">
                    <TriangleAlert />
                    <AlertTitle>Unable to play movie</AlertTitle>
                    <AlertDescription>
                        <p>{error}</p>
                        <div className="mt-3 flex flex-wrap gap-2">
                            <Button asChild size="sm">
                                <Link to="/my-movies">Back to My Movies</Link>
                            </Button>
                            <Button asChild size="sm" variant="outline" className="text-foreground">
                                <Link to={`/movies/${movieId}`}>View Movie Details</Link>
                            </Button>
                        </div>
                    </AlertDescription>
                </Alert>
            </PageContainer>
        );
    }

    return (
        // A lejátszó mindig sötét, a választott témától függetlenül
        <div className="dark flex-1 bg-background text-foreground">
            {/* Back button */}
            <div className="border-b">
                <div className="mx-auto flex max-w-7xl items-center gap-3 px-4 py-3 sm:px-6 lg:px-8">
                    <Button variant="ghost" size="sm" onClick={() => navigate('/my-movies')}>
                        <ArrowLeft />
                        Back to My Movies
                    </Button>
                    {movie && <span className="hidden truncate text-sm text-muted-foreground sm:inline">/ {movie.title}</span>}
                </div>
            </div>

            {/* Main content */}
            <div className="mx-auto grid max-w-7xl gap-6 px-4 py-6 sm:px-6 lg:grid-cols-[1fr_340px] lg:px-8">
                {/* Video player section */}
                <div className="min-w-0 space-y-3">
                    {streamingData && streamingData.url ? (
                        <>
                            {/* Streaming info badges */}
                            <div className="flex flex-wrap items-center gap-2">
                                <Badge variant="success" className="px-2.5 py-1">
                                    <PlayCircle /> Full Movie Streaming
                                </Badge>
                                {streamingData.isHls && (
                                    <Badge variant="warning" className="px-2.5 py-1">
                                        <Zap /> Adaptive Streaming (HLS)
                                    </Badge>
                                )}
                                <Badge variant="muted" className="px-2.5 py-1">
                                    <Clock /> Expires: {new Date(streamingData.expiresAt).toLocaleTimeString()}
                                </Badge>
                            </div>

                            {/* Resume toast */}
                            {showResumeToast && (
                                <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg border border-primary/30 bg-primary/10 px-3 py-2 text-sm">
                                    <span className="flex items-center gap-2">
                                        <PlayCircle className="size-4 text-primary" />
                                        Continuing from <strong className="font-mono">{new Date(savedProgress * 1000).toISOString().substring(11, 19)}</strong>
                                    </span>
                                    <div className="flex items-center gap-1">
                                        <Button
                                            size="sm"
                                            variant="outline"
                                            onClick={() => {
                                                if (videoRef.current) videoRef.current.currentTime = 0;
                                                setShowResumeToast(false);
                                            }}
                                        >
                                            <RotateCcw /> Start from beginning
                                        </Button>
                                        <Button size="icon-sm" variant="ghost" aria-label="Close" onClick={() => setShowResumeToast(false)}>
                                            <X />
                                        </Button>
                                    </div>
                                </div>
                            )}

                            {/* Video Player */}
                            <div className="aspect-video w-full overflow-hidden rounded-xl bg-black shadow-2xl ring-1 shadow-black/60 ring-white/10">
                                <video ref={videoRef} controls className="size-full bg-black">
                                    Your browser does not support the video tag.
                                </video>
                            </div>

                            {/* HLS Quality Selector */}
                            {streamingData.isHls && hlsQualityLevels.length > 0 && (
                                <div className="flex flex-wrap items-center gap-3 rounded-lg border bg-card px-3 py-2">
                                    <span className="flex items-center gap-1.5 text-sm font-medium">
                                        <Gauge className="size-4 text-primary" />
                                        Quality:
                                    </span>
                                    <div className="inline-flex flex-wrap rounded-md bg-muted p-0.5" role="group">
                                        <button
                                            type="button"
                                            className={cn(
                                                "inline-flex cursor-pointer items-center gap-1 rounded px-2.5 py-1 text-xs font-medium transition-colors",
                                                selectedHlsLevel === -1 ? "bg-primary text-primary-foreground" : "text-muted-foreground hover:text-foreground"
                                            )}
                                            onClick={() => setSelectedHlsLevel(-1)}
                                        >
                                            <Wifi className="size-3" />Auto
                                        </button>
                                        {hlsQualityLevels.map((level, index) => (
                                            <button
                                                key={index}
                                                type="button"
                                                className={cn(
                                                    "cursor-pointer rounded px-2.5 py-1 text-xs font-medium transition-colors",
                                                    selectedHlsLevel === index ? "bg-primary text-primary-foreground" : "text-muted-foreground hover:text-foreground"
                                                )}
                                                onClick={() => setSelectedHlsLevel(index)}
                                            >
                                                {level.height}p
                                            </button>
                                        ))}
                                    </div>
                                    <span className="hidden text-xs text-muted-foreground md:inline">
                                        Auto adjusts based on your connection
                                    </span>
                                </div>
                            )}
                        </>
                    ) : trailerData && trailerData.youtubeKey ? (
                        <>
                            <Alert variant="info">
                                <Film />
                                <AlertDescription>
                                    <span><strong className="text-foreground">Trailer Preview</strong> - Full movie streaming not yet available</span>
                                </AlertDescription>
                            </Alert>
                            <div className="aspect-video w-full overflow-hidden rounded-xl bg-black shadow-2xl ring-1 ring-white/10">
                                <iframe
                                    src={`https://www.youtube.com/embed/${trailerData.youtubeKey}?autoplay=0&rel=0`}
                                    title={trailerData.name || 'Movie Trailer'}
                                    frameBorder="0"
                                    allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
                                    allowFullScreen
                                    className="size-full bg-black"
                                />
                            </div>
                        </>
                    ) : (
                        <Alert variant="warning">
                            <TriangleAlert />
                            <AlertDescription>No video available for this movie.</AlertDescription>
                        </Alert>
                    )}
                </div>

                {/* Movie info sidebar */}
                <aside>
                    {movie && (
                        <div className="space-y-3 lg:sticky lg:top-24">
                            <div className="space-y-4 rounded-xl border bg-card p-5">
                                <h1 className="font-display text-4xl leading-none tracking-wide">{movie.title}</h1>

                                {movie.categories && movie.categories.length > 0 && (
                                    <div className="flex flex-wrap gap-1.5">
                                        {movie.categories.map(cat => (
                                            <Badge key={cat.id} variant="secondary">{cat.name}</Badge>
                                        ))}
                                    </div>
                                )}

                                <div>
                                    <h2 className="mb-1.5 flex items-center gap-1.5 text-xs font-semibold tracking-wider text-muted-foreground uppercase">
                                        <Info className="size-3.5" /> Description
                                    </h2>
                                    <p className="text-sm leading-relaxed text-foreground/85">{movie.description}</p>
                                </div>

                                {trailerData && trailerData.name && (
                                    <div>
                                        <h2 className="mb-1.5 flex items-center gap-1.5 text-xs font-semibold tracking-wider text-muted-foreground uppercase">
                                            <Film className="size-3.5" /> Currently Playing
                                        </h2>
                                        <p className="text-sm">{trailerData.name}</p>
                                    </div>
                                )}

                                <Separator />

                                {/* Technical info */}
                                <div className="text-sm">
                                    <h2 className="mb-1.5 flex items-center gap-1.5 text-xs font-semibold tracking-wider text-muted-foreground uppercase">
                                        <Zap className="size-3.5" /> Streaming Info
                                    </h2>
                                    {streamingData && streamingData.isHls ? (
                                        <p className="text-foreground/85">
                                            <strong className="text-foreground">HLS Adaptive Streaming</strong><br />
                                            Quality auto-adjusts (480p, 720p, 1080p) based on your connection speed.
                                        </p>
                                    ) : streamingData && streamingData.availableQualities ? (
                                        <p className="text-foreground/85">
                                            <strong className="text-foreground">Multi-Quality Streaming</strong><br />
                                            Available: {Object.keys(streamingData.availableQualities).join(', ')}
                                        </p>
                                    ) : streamingData && streamingData.url ? (
                                        <p className="text-foreground/85">Streaming from Azure Blob Storage</p>
                                    ) : (
                                        <p className="text-foreground/85">Official trailer - Full movie available after transcoding</p>
                                    )}
                                </div>
                            </div>

                            {/* Quick actions */}
                            <div className="grid gap-2">
                                <Button asChild size="lg">
                                    <Link to={`/my-movies/${movieId}/watch-party`}>
                                        <Users />
                                        Watch Party
                                    </Link>
                                </Button>
                                <Button asChild variant="outline">
                                    <Link to={`/movies/${movieId}`}>
                                        <Info />
                                        View Details
                                    </Link>
                                </Button>
                            </div>
                        </div>
                    )}
                </aside>
            </div>
        </div>
    );
};

export default WatchMovie;
