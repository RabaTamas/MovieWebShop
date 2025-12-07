using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using MovieShop.Server.Services.Interfaces;

namespace MovieShop.Server.Services.Implementations
{
    public class BlobStorageService : IBlobStorageService
    {
        private readonly BlobContainerClient _containerClient;
        private readonly BlobServiceClient _blobServiceClient;
        private readonly ILogger<BlobStorageService> _logger;

        public BlobStorageService(IConfiguration configuration, ILogger<BlobStorageService> logger)
        {
            _logger = logger;
            
            var connectionString = configuration["AzureBlob:ConnectionString"];
            var containerName = configuration["AzureBlob:ContainerName"] ?? "movie-videos";

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Azure Blob Storage connection string is not configured");
            }

            _blobServiceClient = new BlobServiceClient(connectionString);
            _containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            
            // Create container if it doesn't exist
            _containerClient.CreateIfNotExists(PublicAccessType.None);
            
            // Configure CORS for the storage account
            ConfigureCorsAsync().Wait();
            
            _logger.LogInformation($"BlobStorageService initialized for container: {containerName}");
        }

        private async Task ConfigureCorsAsync()
        {
            try
            {
                var properties = await _blobServiceClient.GetPropertiesAsync();
                var cors = properties.Value.Cors.ToList();

                // Check if CORS rule already exists
                var existingRule = cors.FirstOrDefault(c => c.AllowedOrigins.Contains("http://localhost:3000"));
                
                if (existingRule == null)
                {
                    // Add CORS rule for localhost
                    cors.Add(new BlobCorsRule
                    {
                        AllowedOrigins = "http://localhost:3000,https://localhost:3000",
                        AllowedMethods = "GET,HEAD,OPTIONS",
                        AllowedHeaders = "*",
                        ExposedHeaders = "*",
                        MaxAgeInSeconds = 3600
                    });

                    await _blobServiceClient.SetPropertiesAsync(new BlobServiceProperties
                    {
                        Cors = cors
                    });

                    _logger.LogInformation("CORS configured for Azure Blob Storage");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to configure CORS (this is non-critical if CORS is already configured via Azure Portal)");
            }
        }

        public async Task<string> UploadVideoAsync(Stream fileStream, string fileName)
        {
            try
            {
                var blobClient = _containerClient.GetBlobClient(fileName);
                
                await blobClient.UploadAsync(fileStream, overwrite: true);
                
                _logger.LogInformation($"Uploaded video to Azure Blob: {fileName}");
                return blobClient.Uri.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error uploading video to Azure Blob: {fileName}");
                throw;
            }
        }

        public async Task<string> GenerateSasUrlAsync(string fileName, int expiryHours = 1)
        {
            try
            {
                var blobClient = _containerClient.GetBlobClient(fileName);

                if (!await blobClient.ExistsAsync())
                {
                    throw new FileNotFoundException($"Video file not found: {fileName}");
                }

                // Check if we can generate SAS tokens (need storage account key)
                if (blobClient.CanGenerateSasUri)
                {
                    var sasBuilder = new BlobSasBuilder
                    {
                        BlobContainerName = _containerClient.Name,
                        BlobName = fileName,
                        Resource = "b", // blob
                        StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5), // 5 min grace period
                        ExpiresOn = DateTimeOffset.UtcNow.AddHours(expiryHours)
                    };

                    sasBuilder.SetPermissions(BlobSasPermissions.Read);

                    var sasUri = blobClient.GenerateSasUri(sasBuilder);
                    _logger.LogInformation($"Generated SAS URL for {fileName}, expires in {expiryHours} hours");
                    return sasUri.ToString();
                }
                else
                {
                    // Fallback: return direct URL (only works if container is public)
                    _logger.LogWarning("Cannot generate SAS token, returning direct URL");
                    return blobClient.Uri.ToString();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error generating SAS URL for: {fileName}");
                throw;
            }
        }

        public async Task DeleteVideoAsync(string fileName)
        {
            try
            {
                var blobClient = _containerClient.GetBlobClient(fileName);
                await blobClient.DeleteIfExistsAsync();
                _logger.LogInformation($"Deleted video from Azure Blob: {fileName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deleting video from Azure Blob: {fileName}");
                throw;
            }
        }

        public async Task<bool> VideoExistsAsync(string fileName)
        {
            try
            {
                var blobClient = _containerClient.GetBlobClient(fileName);
                return await blobClient.ExistsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error checking if video exists: {fileName}");
                return false;
            }
        }

        public async Task<long> GetVideoSizeAsync(string fileName)
        {
            try
            {
                var blobClient = _containerClient.GetBlobClient(fileName);
                
                if (!await blobClient.ExistsAsync())
                {
                    return 0;
                }

                var properties = await blobClient.GetPropertiesAsync();
                return properties.Value.ContentLength;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting video size: {fileName}");
                return 0;
            }
        }

        public async Task<string> DownloadToTempAsync(string fileName)
        {
            try
            {
                var blobClient = _containerClient.GetBlobClient(fileName);
                var tempPath = Path.Combine(Path.GetTempPath(), fileName);

                await blobClient.DownloadToAsync(tempPath);
                
                _logger.LogInformation($"Downloaded {fileName} to temp: {tempPath}");
                return tempPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error downloading video to temp: {fileName}");
                throw;
            }
        }

        public async Task<List<string>> ListFilesAsync(string prefix = "")
        {
            try
            {
                var files = new List<string>();
                await foreach (var blobItem in _containerClient.GetBlobsAsync(prefix: prefix))
                {
                    files.Add(blobItem.Name);
                }
                _logger.LogInformation($"Listed {files.Count} files with prefix: {prefix}");
                return files;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error listing files with prefix: {prefix}");
                throw;
            }
        }
    }
}
