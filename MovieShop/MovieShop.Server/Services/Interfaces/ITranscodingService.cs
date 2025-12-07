namespace MovieShop.Server.Services.Interfaces
{
    /// <summary>
    /// Service for background video transcoding using FFmpeg
    /// </summary>
    public interface ITranscodingService
    {
        /// <summary>
        /// Transcodes video to multiple resolutions (480p, 720p, 1080p) and generates HLS manifests
        /// This is executed as a Hangfire background job
        /// </summary>
        /// <param name="movieId">Movie ID in database</param>
        /// <param name="originalFileName">Original video filename in Azure Blob (e.g., "1.mp4")</param>
        Task TranscodeVideoAsync(int movieId, string originalFileName);
    }
}
