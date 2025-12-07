import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import API_BASE_URL from '../../config/api';

const AdminVideoUpload = () => {
    const { movieId } = useParams();
    const { token } = useAuth();
    const navigate = useNavigate();
    
    const [movie, setMovie] = useState(null);
    const [videoInfo, setVideoInfo] = useState(null);
    const [selectedFile, setSelectedFile] = useState(null);
    const [uploading, setUploading] = useState(false);
    const [uploadProgress, setUploadProgress] = useState(0);
    const [message, setMessage] = useState(null);
    const [loading, setLoading] = useState(true);

    useEffect(() => {
        fetchMovieAndVideoInfo();
    }, [movieId, token]);

    const fetchMovieAndVideoInfo = async () => {
        try {
            // Fetch movie details
            const movieResponse = await fetch(`${API_BASE_URL}/api/Movie/${movieId}`, {
                headers: { 'Authorization': `Bearer ${token}` }
            });
            const movieData = await movieResponse.json();
            setMovie(movieData);

            // Check if video exists based on videoFileName in movie data
            if (movieData.videoFileName) {
                // Try to fetch detailed video info from admin endpoint
                try {
                    const videoResponse = await fetch(`${API_BASE_URL}/api/admin/Video/${movieId}/info`, {
                        headers: { 'Authorization': `Bearer ${token}` }
                    });
                    
                    if (videoResponse.ok) {
                        const videoData = await videoResponse.json();
                        setVideoInfo(videoData);
                    } else {
                        // Fallback: construct basic video info from movie data
                        setVideoInfo({
                            hasVideo: true,
                            videoFileName: movieData.videoFileName,
                            manifestExists: movieData.videoFileName.endsWith('.m3u8'),
                            transcodingComplete: movieData.videoFileName.endsWith('.m3u8')
                        });
                    }
                } catch (err) {
                    console.error('Error fetching video info, using fallback:', err);
                    // Fallback to basic info
                    setVideoInfo({
                        hasVideo: true,
                        videoFileName: movieData.videoFileName,
                        manifestExists: movieData.videoFileName.endsWith('.m3u8'),
                        transcodingComplete: movieData.videoFileName.endsWith('.m3u8')
                    });
                }
            } else {
                setVideoInfo({ hasVideo: false });
            }
        } catch (err) {
            console.error('Error loading movie information:', err);
            setMessage({ type: 'danger', text: 'Error loading movie information' });
        } finally {
            setLoading(false);
        }
    };

    const handleFileSelect = (e) => {
        const file = e.target.files[0];
        if (file) {
            if (file.type !== 'video/mp4') {
                setMessage({ type: 'danger', text: 'Only MP4 files are supported' });
                return;
            }
            setSelectedFile(file);
            setMessage(null);
        }
    };

    const handleUpload = async () => {
        if (!selectedFile) {
            setMessage({ type: 'danger', text: 'Please select a file first' });
            return;
        }

        setUploading(true);
        setUploadProgress(0);
        setMessage(null);

        const formData = new FormData();
        formData.append('videoFile', selectedFile);

        try {
            const xhr = new XMLHttpRequest();

            xhr.upload.addEventListener('progress', (e) => {
                if (e.lengthComputable) {
                    const percentComplete = (e.loaded / e.total) * 100;
                    setUploadProgress(Math.round(percentComplete));
                }
            });

            xhr.addEventListener('load', () => {
                if (xhr.status === 200) {
                    setMessage({ type: 'success', text: 'Video uploaded successfully!' });
                    setSelectedFile(null);
                    fetchMovieAndVideoInfo();
                } else {
                    const response = JSON.parse(xhr.responseText);
                    setMessage({ type: 'danger', text: response.message || 'Upload failed' });
                }
                setUploading(false);
            });

            xhr.addEventListener('error', () => {
                setMessage({ type: 'danger', text: 'Network error during upload' });
                setUploading(false);
            });

            xhr.open('POST', `${API_BASE_URL}/api/admin/Video/upload/${movieId}`);
            xhr.setRequestHeader('Authorization', `Bearer ${token}`);
            xhr.send(formData);
        } catch (err) {
            console.error('Error uploading video:', err);
            setMessage({ type: 'danger', text: 'Error uploading video' });
            setUploading(false);
        }
    };

    const handleDelete = async () => {
        if (!window.confirm('Are you sure you want to delete this video?')) {
            return;
        }

        try {
            const response = await fetch(`${API_BASE_URL}/api/admin/Video/${movieId}`, {
                method: 'DELETE',
                headers: { 'Authorization': `Bearer ${token}` }
            });

            if (response.ok) {
                setMessage({ type: 'success', text: 'Video deleted successfully' });
                fetchMovieAndVideoInfo();
            } else {
                const data = await response.json();
                setMessage({ type: 'danger', text: data.message || 'Delete failed' });
            }
        } catch (err) {
            console.error('Error deleting video:', err);
            setMessage({ type: 'danger', text: 'Error deleting video' });
        }
    };

    if (loading) {
        return (
            <div className="container mt-5">
                <div className="text-center">
                    <div className="spinner-border" role="status">
                        <span className="visually-hidden">Loading...</span>
                    </div>
                </div>
            </div>
        );
    }

    return (
        <div className="container mt-4">
            <div className="row">
                <div className="col-md-8 offset-md-2">
                    <div className="d-flex justify-content-between align-items-center mb-4">
                        <h2>Video Upload</h2>
                        <button onClick={() => navigate('/admin/movies')} className="btn btn-secondary">
                            <i className="bi bi-arrow-left me-2"></i>Back to Movies
                        </button>
                    </div>

                    {message && (
                        <div className={`alert alert-${message.type} alert-dismissible fade show`} role="alert">
                            {message.text}
                            <button type="button" className="btn-close" onClick={() => setMessage(null)}></button>
                        </div>
                    )}

                    {/* Movie Info */}
                    <div className="card mb-4">
                        <div className="card-body">
                            <h5 className="card-title">{movie?.title}</h5>
                            <p className="text-muted">Movie ID: {movieId}</p>
                        </div>
                    </div>

                    {/* Current Video Status */}
                    {videoInfo?.hasVideo && (
                        <div className="card mb-4">
                            <div className="card-header bg-success text-white">
                                <i className="bi bi-check-circle me-2"></i>Current Video Status
                            </div>
                            <div className="card-body">
                                <div className="row">
                                    <div className="col-md-6">
                                        <p><strong>Filename:</strong> {videoInfo.videoFileName}</p>
                                        <p><strong>Original Size:</strong> {videoInfo.fileSizeMB?.toFixed(2)} MB</p>
                                        <p><strong>Original File:</strong> 
                                            {videoInfo.originalExists ? (
                                                <span className="badge bg-success ms-2">Uploaded</span>
                                            ) : (
                                                <span className="badge bg-warning ms-2">Missing</span>
                                            )}
                                        </p>
                                    </div>
                                    <div className="col-md-6">
                                        <p><strong>Transcoding Status:</strong> 
                                            {videoInfo.transcodingComplete ? (
                                                <span className="badge bg-success ms-2">✓ Complete</span>
                                            ) : (
                                                <span className="badge bg-warning ms-2">⏳ Processing...</span>
                                            )}
                                        </p>
                                        {videoInfo.transcodedVersions && (
                                            <>
                                                <p><strong>Available Qualities:</strong></p>
                                                <ul className="list-unstyled ms-3">
                                                    <li>
                                                        480p: {videoInfo.transcodedVersions['480p'] ? (
                                                            <span className="badge bg-success">✓</span>
                                                        ) : (
                                                            <span className="badge bg-secondary">⏳</span>
                                                        )}
                                                    </li>
                                                    <li>
                                                        720p: {videoInfo.transcodedVersions['720p'] ? (
                                                            <span className="badge bg-success">✓</span>
                                                        ) : (
                                                            <span className="badge bg-secondary">⏳</span>
                                                        )}
                                                    </li>
                                                    <li>
                                                        1080p: {videoInfo.transcodedVersions['1080p'] ? (
                                                            <span className="badge bg-success">✓</span>
                                                        ) : (
                                                            <span className="badge bg-secondary">⏳</span>
                                                        )}
                                                    </li>
                                                    {videoInfo.manifestExists && (
                                                        <li className="mt-2">
                                                            <span className="badge bg-primary">
                                                                <i className="bi bi-play-circle me-1"></i>
                                                                HLS Adaptive Streaming Ready
                                                            </span>
                                                        </li>
                                                    )}
                                                </ul>
                                            </>
                                        )}
                                    </div>
                                </div>
                                <button onClick={handleDelete} className="btn btn-danger mt-3">
                                    <i className="bi bi-trash me-2"></i>Delete All Video Files
                                </button>
                            </div>
                        </div>
                    )}

                    {/* Upload Section */}
                    <div className="card">
                        <div className="card-header">
                            <i className="bi bi-upload me-2"></i>
                            {videoInfo?.hasVideo ? 'Replace Video' : 'Upload Video'}
                        </div>
                        <div className="card-body">
                            <div className="mb-3">
                                <label htmlFor="videoFile" className="form-label">
                                    Select MP4 Video File
                                </label>
                                <input
                                    type="file"
                                    className="form-control"
                                    id="videoFile"
                                    accept="video/mp4"
                                    onChange={handleFileSelect}
                                    disabled={uploading}
                                />
                                {selectedFile && (
                                    <div className="mt-2">
                                        <small className="text-muted">
                                            Selected: {selectedFile.name} ({(selectedFile.size / 1024 / 1024).toFixed(2)} MB)
                                        </small>
                                    </div>
                                )}
                            </div>

                            {uploading && (
                                <div className="mb-3">
                                    <div className="progress">
                                        <div
                                            className="progress-bar progress-bar-striped progress-bar-animated"
                                            role="progressbar"
                                            style={{ width: `${uploadProgress}%` }}
                                        >
                                            {uploadProgress}%
                                        </div>
                                    </div>
                                </div>
                            )}

                            <button
                                onClick={handleUpload}
                                className="btn btn-primary"
                                disabled={!selectedFile || uploading}
                            >
                                {uploading ? (
                                    <>
                                        <span className="spinner-border spinner-border-sm me-2"></span>
                                        Uploading...
                                    </>
                                ) : (
                                    <>
                                        <i className="bi bi-upload me-2"></i>Upload Video
                                    </>
                                )}
                            </button>
                        </div>
                    </div>

                    {/* Instructions */}
                    <div className="alert alert-info mt-4">
                        <h6><i className="bi bi-info-circle me-2"></i>Azure Blob + HLS Streaming Info:</h6>
                        <ul className="mb-0">
                            <li>Only MP4 format is supported (1080p recommended)</li>
                            <li>Video will be uploaded to Azure Blob Storage as <code>{movieId}.mp4</code></li>
                            <li>Background job automatically transcodes to 480p, 720p, and 1080p</li>
                            <li>HLS manifest generated for adaptive streaming</li>
                            <li>Large files may take several minutes to upload and transcode</li>
                            <li>Users stream via SAS tokens with 1-hour expiry</li>
                        </ul>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default AdminVideoUpload;
