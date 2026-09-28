import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import {
    AlertCircle, ArrowLeft, CheckCircle2, Clapperboard, FileVideo, Hourglass, Info, PlayCircle, Trash2, Upload, X,
} from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';
import API_BASE_URL from '../../config/api';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Progress } from '@/components/ui/progress';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { LoadingState, Spinner } from '@/components/ui/spinner';
import { PageContainer, PageHeader } from '@/components/ui/page';

const StatusRow = ({ label, children }) => (
    <div className="flex items-center justify-between gap-3 py-2 text-sm">
        <span className="text-muted-foreground">{label}</span>
        <span className="text-right font-medium">{children}</span>
    </div>
);

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

    // Transzkódolás közben (percekig tarthat) 10 másodpercenként frissítjük az állapotot,
    // így a „Processing..." magától „Complete"-re vált, nem kell kézzel újratölteni az oldalt
    const transcodingInProgress = videoInfo?.hasVideo && !videoInfo?.transcodingComplete;
    useEffect(() => {
        if (!transcodingInProgress) return;
        const id = setInterval(fetchMovieAndVideoInfo, 10000);
        return () => clearInterval(id);
    }, [transcodingInProgress, movieId, token]);

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
                    setMessage({
                        type: 'success',
                        text: 'Video uploaded successfully! The original MP4 is playable right away; HLS transcoding (480p/720p/1080p) runs in the background and may take a few minutes — this page updates automatically.',
                    });
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
        return <LoadingState />;
    }

    const qualityBadge = (ready) => ready
        ? <Badge variant="success"><CheckCircle2 />Ready</Badge>
        : <Badge variant="muted"><Hourglass />Pending</Badge>;

    return (
        <PageContainer size="md">
            <PageHeader title="Video Upload" icon={FileVideo} description={movie?.title ? `${movie.title} · Movie ID: ${movieId}` : `Movie ID: ${movieId}`}>
                <Button variant="outline" onClick={() => navigate('/admin/movies')}>
                    <ArrowLeft /> Back to Movies
                </Button>
            </PageHeader>

            <div className="space-y-6">
                {message && (
                    <Alert variant={message.type === 'success' ? 'success' : 'destructive'} className="pr-10">
                        {message.type === 'success' ? <CheckCircle2 /> : <AlertCircle />}
                        <AlertDescription>{message.text}</AlertDescription>
                        <button
                            type="button"
                            className="absolute top-2.5 right-2.5 cursor-pointer rounded p-1 opacity-70 hover:opacity-100"
                            aria-label="Close"
                            onClick={() => setMessage(null)}
                        >
                            <X className="size-4" />
                        </button>
                    </Alert>
                )}

                {/* Current Video Status */}
                {videoInfo?.hasVideo && (
                    <Card className="border-success/30">
                        <CardHeader>
                            <CardTitle className="flex items-center gap-2 text-success">
                                <CheckCircle2 className="size-5" />Current Video Status
                            </CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-4">
                            <div className="grid gap-x-8 md:grid-cols-2">
                                <div className="divide-y">
                                    <StatusRow label="Filename"><code className="text-xs break-all">{videoInfo.videoFileName}</code></StatusRow>
                                    <StatusRow label="Original Size">{videoInfo.fileSizeMB?.toFixed(2)} MB</StatusRow>
                                    <StatusRow label="Original File">
                                        {videoInfo.originalExists
                                            ? <Badge variant="success">Uploaded</Badge>
                                            : <Badge variant="warning">Missing</Badge>}
                                    </StatusRow>
                                </div>
                                <div className="divide-y">
                                    <StatusRow label="Transcoding Status">
                                        {videoInfo.transcodingComplete
                                            ? <Badge variant="success"><CheckCircle2 />Complete</Badge>
                                            : <Badge variant="warning"><Spinner className="size-3" />Processing...</Badge>}
                                    </StatusRow>
                                    {videoInfo.transcodedVersions && (
                                        <>
                                            <StatusRow label="480p">{qualityBadge(videoInfo.transcodedVersions['480p'])}</StatusRow>
                                            <StatusRow label="720p">{qualityBadge(videoInfo.transcodedVersions['720p'])}</StatusRow>
                                            <StatusRow label="1080p">{qualityBadge(videoInfo.transcodedVersions['1080p'])}</StatusRow>
                                        </>
                                    )}
                                </div>
                            </div>
                            {videoInfo.transcodedVersions && videoInfo.manifestExists && (
                                <div className="flex items-center gap-2 rounded-lg bg-primary/10 px-3 py-2 text-sm font-medium text-primary">
                                    <PlayCircle className="size-4" />
                                    HLS Adaptive Streaming Ready
                                </div>
                            )}
                            <Button variant="destructive" onClick={handleDelete}>
                                <Trash2 />Delete All Video Files
                            </Button>
                        </CardContent>
                    </Card>
                )}

                {/* Upload Section */}
                <Card>
                    <CardHeader>
                        <CardTitle className="flex items-center gap-2">
                            <Upload className="size-5 text-primary" />
                            {videoInfo?.hasVideo ? 'Replace Video' : 'Upload Video'}
                        </CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-4">
                        <div className="space-y-2">
                            <Label htmlFor="videoFile">Select MP4 Video File</Label>
                            <label
                                htmlFor="videoFile"
                                className="flex cursor-pointer flex-col items-center justify-center gap-2 rounded-xl border-2 border-dashed px-6 py-8 text-center transition-colors hover:border-primary/60 hover:bg-primary/5"
                            >
                                <Clapperboard className="size-8 text-muted-foreground" />
                                {selectedFile ? (
                                    <span className="text-sm">
                                        Selected: <strong>{selectedFile.name}</strong> ({(selectedFile.size / 1024 / 1024).toFixed(2)} MB)
                                    </span>
                                ) : (
                                    <span className="text-sm text-muted-foreground">Click to choose an .mp4 file</span>
                                )}
                            </label>
                            {/* Natív, rejtett input: a fenti dropzone-label nyitja meg (a shadcn Input w-full-ja felülírná az sr-only szélességét) */}
                            <input
                                type="file"
                                id="videoFile"
                                className="sr-only"
                                accept="video/mp4"
                                onChange={handleFileSelect}
                                disabled={uploading}
                            />
                        </div>

                        {uploading && (
                            <div className="space-y-1.5">
                                <div className="flex justify-between text-xs text-muted-foreground">
                                    <span>Uploading...</span>
                                    <span className="font-mono">{uploadProgress}%</span>
                                </div>
                                <Progress value={uploadProgress} />
                            </div>
                        )}

                        <Button onClick={handleUpload} disabled={!selectedFile || uploading}>
                            {uploading ? <><Spinner />Uploading...</> : <><Upload />Upload Video</>}
                        </Button>
                    </CardContent>
                </Card>

                {/* Instructions */}
                <Alert variant="info">
                    <Info />
                    <AlertTitle>Azure Blob + HLS Streaming Info:</AlertTitle>
                    <AlertDescription>
                        <ul className="list-disc space-y-0.5 pl-4">
                            <li>Only MP4 format is supported (1080p recommended)</li>
                            <li>Video will be uploaded to Azure Blob Storage as <code className="rounded bg-muted px-1">{movieId}.mp4</code></li>
                            <li>Background job automatically transcodes to 480p, 720p, and 1080p</li>
                            <li>HLS manifest generated for adaptive streaming</li>
                            <li>Large files may take several minutes to upload and transcode</li>
                            <li>Users stream via SAS tokens with 1-hour expiry</li>
                        </ul>
                    </AlertDescription>
                </Alert>
            </div>
        </PageContainer>
    );
};

export default AdminVideoUpload;
