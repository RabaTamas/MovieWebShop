import { useState, useEffect, useRef } from 'react';
import { useParams, useNavigate, Link, useLocation } from 'react-router-dom';
import { useAuth } from '../contexts/AuthContext';
import API_BASE_URL from '../config/api';
import Hls from 'hls.js';

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
        return (
            <div className="container mt-5">
                <div className="text-center">
                    <div className="spinner-border text-primary" role="status">
                        <span className="visually-hidden">Loading...</span>
                    </div>
                    <p className="mt-3">Loading movie...</p>
                </div>
            </div>
        );
    }

    if (error) {
        return (
            <div className="container mt-5">
                <div className="alert alert-danger" role="alert">
                    <h4 className="alert-heading">Unable to play movie</h4>
                    <p>{error}</p>
                    <hr />
                    <div className="d-flex gap-2">
                        <Link to="/my-movies" className="btn btn-primary">
                            Back to My Movies
                        </Link>
                        <Link to={`/movie/${movieId}`} className="btn btn-outline-primary">
                            View Movie Details
                        </Link>
                    </div>
                </div>
            </div>
        );
    }

    return (
        <div className="bg-dark text-white" style={{ minHeight: '100vh' }}>
            {/* Back button - fixed at top */}
            <div className="container-fluid py-3 border-bottom border-secondary">
                <div className="container">
                    <button 
                        onClick={() => navigate('/my-movies')} 
                        className="btn btn-outline-light btn-sm"
                    >
                        <i className="bi bi-arrow-left me-2"></i>
                        Back to My Movies
                    </button>
                </div>
            </div>

            {/* Main content */}
            <div className="container py-4">
                {/* Video player section */}
                <div className="row g-4">
                    <div className="col-lg-8">
                        {streamingData && streamingData.url ? (
                            <>
                                {/* Streaming info badge */}
                                <div className="d-flex align-items-center mb-3 flex-wrap gap-2">
                                    <div className="badge bg-success px-3 py-2">
                                        <i className="bi bi-play-circle me-2"></i>
                                        Full Movie Streaming
                                    </div>
                                    {streamingData.isHls && (
                                        <div className="badge bg-primary px-3 py-2">
                                            <i className="bi bi-lightning-charge me-1"></i>
                                            Adaptive Streaming (HLS)
                                        </div>
                                    )}
                                    <div className="badge bg-secondary px-3 py-2">
                                        <i className="bi bi-clock me-1"></i>
                                        Expires: {new Date(streamingData.expiresAt).toLocaleTimeString()}
                                    </div>
                                </div>
                                
                                {/* Resume toast */}
                                {showResumeToast && (
                                    <div className="alert alert-info d-flex align-items-center justify-content-between py-2 mb-3">
                                        <span>
                                            <i className="bi bi-play-circle me-2"></i>
                                            Continuing from {new Date(savedProgress * 1000).toISOString().substring(11, 19)}
                                        </span>
                                        <button
                                            className="btn btn-sm btn-outline-secondary ms-3"
                                            onClick={() => {
                                                if (videoRef.current) videoRef.current.currentTime = 0;
                                                setShowResumeToast(false);
                                            }}
                                        >
                                            Start from beginning
                                        </button>
                                        <button
                                            type="button"
                                            className="btn-close ms-2"
                                            onClick={() => setShowResumeToast(false)}
                                        />
                                    </div>
                                )}

                                {/* HLS Quality Selector */}
                                {streamingData.isHls && hlsQualityLevels.length > 0 && (
                                    <div className="card bg-secondary text-white mb-3">
                                        <div className="card-body py-2">
                                            <div className="d-flex align-items-center flex-wrap gap-2">
                                                <span className="fw-bold me-2">
                                                    <i className="bi bi-gear me-1"></i>
                                                    Quality:
                                                </span>
                                                <div className="btn-group btn-group-sm" role="group">
                                                    <button
                                                        type="button"
                                                        className={`btn ${selectedHlsLevel === -1 ? 'btn-success' : 'btn-outline-light'}`}
                                                        onClick={() => setSelectedHlsLevel(-1)}
                                                    >
                                                        <i className="bi bi-wifi me-1"></i>Auto
                                                    </button>
                                                    {hlsQualityLevels.map((level, index) => (
                                                        <button
                                                            key={index}
                                                            type="button"
                                                            className={`btn ${selectedHlsLevel === index ? 'btn-primary' : 'btn-outline-light'}`}
                                                            onClick={() => setSelectedHlsLevel(index)}
                                                        >
                                                            {level.height}p
                                                        </button>
                                                    ))}
                                                </div>
                                                <small className="text-light opacity-75 d-none d-md-inline">
                                                    Auto adjusts based on your connection
                                                </small>
                                            </div>
                                        </div>
                                    </div>
                                )}
                                
                                {/* Video Player with shadow */}
                                <div className="ratio ratio-16x9 rounded overflow-hidden shadow-lg">
                                    <video
                                        ref={videoRef}
                                        controls
                                        className="bg-black"
                                        style={{ width: '100%', height: '100%' }}
                                    >
                                        Your browser does not support the video tag.
                                    </video>
                                </div>
                            </>
                        ) : trailerData && trailerData.youtubeKey ? (
                            <>
                                <div className="alert alert-info mb-3">
                                    <i className="bi bi-film me-2"></i>
                                    <strong>Trailer Preview</strong> - Full movie streaming not yet available
                                </div>
                                <div className="ratio ratio-16x9 rounded overflow-hidden shadow-lg">
                                    <iframe
                                        src={`https://www.youtube.com/embed/${trailerData.youtubeKey}?autoplay=0&rel=0`}
                                        title={trailerData.name || 'Movie Trailer'}
                                        frameBorder="0"
                                        allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture"
                                        allowFullScreen
                                        className="bg-black"
                                    />
                                </div>
                            </>
                        ) : (
                            <div className="alert alert-warning">
                                <i className="bi bi-exclamation-triangle me-2"></i>
                                No video available for this movie.
                            </div>
                        )}
                    </div>

                    {/* Movie info sidebar */}
                    <div className="col-lg-4">
                        {movie && (
                            <div className="sticky-lg-top" style={{ top: '20px' }}>
                                <div className="card bg-secondary text-white">
                                    <div className="card-body">
                                        <h2 className="card-title h4 mb-3">{movie.title}</h2>
                                        
                                        {movie.categories && movie.categories.length > 0 && (
                                            <div className="mb-3">
                                                <div className="d-flex flex-wrap gap-2">
                                                    {movie.categories.map(cat => (
                                                        <span key={cat.id} className="badge bg-primary px-3 py-2">
                                                            {cat.name}
                                                        </span>
                                                    ))}
                                                </div>
                                            </div>
                                        )}

                                        <div className="mb-3">
                                            <h6 className="text-light opacity-75 mb-2">
                                                <i className="bi bi-info-circle me-2"></i>
                                                Description
                                            </h6>
                                            <p className="card-text small">{movie.description}</p>
                                        </div>

                                        {trailerData && trailerData.name && (
                                            <div className="mb-3">
                                                <h6 className="text-light opacity-75 mb-2">
                                                    <i className="bi bi-film me-2"></i>
                                                    Currently Playing
                                                </h6>
                                                <p className="small mb-0">{trailerData.name}</p>
                                            </div>
                                        )}

                                        <hr className="border-light opacity-25" />

                                        {/* Technical info */}
                                        <div className="small">
                                            <h6 className="text-light opacity-75 mb-2">
                                                <i className="bi bi-info-square me-2"></i>
                                                Streaming Info
                                            </h6>
                                            {streamingData && streamingData.isHls ? (
                                                <p className="mb-0">
                                                    <strong>HLS Adaptive Streaming</strong><br/>
                                                    Quality auto-adjusts (480p, 720p, 1080p) based on your connection speed.
                                                </p>
                                            ) : streamingData && streamingData.availableQualities ? (
                                                <p className="mb-0">
                                                    <strong>Multi-Quality Streaming</strong><br/>
                                                    Available: {Object.keys(streamingData.availableQualities).join(', ')}
                                                </p>
                                            ) : streamingData && streamingData.url ? (
                                                <p className="mb-0">Streaming from Azure Blob Storage</p>
                                            ) : (
                                                <p className="mb-0">Official trailer - Full movie available after transcoding</p>
                                            )}
                                        </div>
                                    </div>
                                </div>

                                {/* Quick actions */}
                                <div className="mt-3 d-grid gap-2">
                                    <Link
                                        to={`/my-movies/${movieId}/watch-party`}
                                        className="btn btn-primary"
                                    >
                                        <i className="bi bi-people-fill me-2"></i>
                                        Watch Party
                                    </Link>
                                    <Link
                                        to={`/movies/${movieId}`}
                                        className="btn btn-outline-light"
                                    >
                                        <i className="bi bi-info-circle me-2"></i>
                                        View Details
                                    </Link>
                                </div>
                            </div>
                        )}
                    </div>
                </div>
            </div>
        </div>
    );
};

export default WatchMovie;
