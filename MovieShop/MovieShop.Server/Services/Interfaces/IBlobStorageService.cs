namespace MovieShop.Server.Services.Interfaces
{
    public interface IBlobStorageService
    {
        /// <summary>
        /// Upload a file to Azure Blob Storage
        /// </summary>
        Task<string> UploadVideoAsync(Stream fileStream, string fileName);

        /// <summary>
        /// Generate a SAS token URL for streaming (1 hour expiry)
        /// </summary>
        Task<string> GenerateSasUrlAsync(string fileName, int expiryHours = 1);

        /// <summary>
        /// Delete a video file from Azure Blob Storage
        /// </summary>
        Task DeleteVideoAsync(string fileName);

        /// <summary>
        /// Check if a video file exists in Azure Blob Storage
        /// </summary>
        Task<bool> VideoExistsAsync(string fileName);

        /// <summary>
        /// Get video file size in bytes
        /// </summary>
        Task<long> GetVideoSizeAsync(string fileName);

        /// <summary>
        /// Download video to local temp for transcoding
        /// </summary>
        Task<string> DownloadToTempAsync(string fileName);

        /// <summary>
        /// List all files with a given prefix
        /// </summary>
        Task<List<string>> ListFilesAsync(string prefix = "");
    }
}
