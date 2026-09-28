using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace MovieShop.CatalogService.Services;

public interface IBlobStorageService
{
    Task<string> UploadVideoAsync(Stream fileStream, string fileName);
    Task<string> GenerateSasUrlAsync(string fileName, int expiryHours = 1);
    Task DeleteVideoAsync(string fileName);
    Task<bool> VideoExistsAsync(string fileName);
    Task<long> GetVideoSizeAsync(string fileName);
    Task<string> DownloadToTempAsync(string fileName);
    Task<List<string>> ListFilesAsync(string prefix = "");
}

/// <summary>
/// Azure Blob Storage a HLS szegmensekhez és lejátszási listákhoz.
///
/// A monolithoz hasonlóan induláskor beállítja a tárfiók CORS-szabályát: a böngésző
/// a SAS URL-eken közvetlenül a Blob Storage-ból tölti le a szegmenseket, ami CORS
/// nélkül elbukik. A monolit csak a http://localhost:3000 eredetet vette fel; itt a
/// konfigurált eredetek mindegyike (így a :3001-es mikroszervizes frontend is) bekerül.
///
/// Eltérés a monolittól: hiányzó connection string esetén a konstruktor nem dob
/// kivételt, így a katalógus a videótároló nélkül is működik.
/// </summary>
public class BlobStorageService : IBlobStorageService
{
    private static readonly object CorsLock = new();
    private static Task? _corsTask;

    private readonly BlobContainerClient? _containerClient;
    private readonly ILogger<BlobStorageService> _logger;

    public BlobStorageService(IConfiguration configuration, ILogger<BlobStorageService> logger)
    {
        _logger = logger;

        var connectionString = configuration["AzureBlob:ConnectionString"];
        var containerName = configuration["AzureBlob:ContainerName"] ?? "movie-videos";

        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.LogWarning("Azure Blob Storage is not configured — video upload and streaming are unavailable");
            return;
        }

        try
        {
            var blobServiceClient = new BlobServiceClient(connectionString);
            _containerClient = blobServiceClient.GetBlobContainerClient(containerName);
            _containerClient.CreateIfNotExists(PublicAccessType.None);

            var origins = (configuration["CORS:AllowedOrigins"] ?? "http://localhost:3000")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // A monolit minden példányosításkor lekérdezte a tárfiók beállításait;
            // itt folyamatonként egyszer fut le.
            lock (CorsLock)
            {
                _corsTask ??= ConfigureCorsAsync(blobServiceClient, origins, logger);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Azure Blob Storage");
        }
    }

    private static async Task ConfigureCorsAsync(BlobServiceClient client, string[] requiredOrigins, ILogger logger)
    {
        try
        {
            var properties = await client.GetPropertiesAsync();
            var cors = properties.Value.Cors.ToList();

            var existingOrigins = cors
                .SelectMany(rule => rule.AllowedOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (existingOrigins.Contains("*"))
                return;

            var missing = requiredOrigins.Where(origin => !existingOrigins.Contains(origin)).ToList();
            if (missing.Count == 0)
                return;

            var serviceProperties = properties.Value;

            if (serviceProperties.Cors.Count >= 5)
            {
                logger.LogWarning("Azure Blob CORS already has 5 rules; could not add origins: {Origins}", string.Join(",", missing));
                return;
            }

            serviceProperties.Cors.Add(new BlobCorsRule
            {
                AllowedOrigins = string.Join(",", missing),
                AllowedMethods = "GET,HEAD,OPTIONS",
                AllowedHeaders = "*",
                ExposedHeaders = "*",
                MaxAgeInSeconds = 3600
            });

            // A lekérdezett tulajdonságobjektumot küldjük vissza módosítva (a Microsoft
            // dokumentált mintája). Egy csak Cors-t tartalmazó új BlobServiceProperties-t
            // — ahogy a monolit is küldte — a szolgáltatás "XML specified is not
            // syntactically valid" hibával utasított el.
            await client.SetPropertiesAsync(serviceProperties);

            logger.LogInformation("CORS configured for Azure Blob Storage: {Origins}", string.Join(",", missing));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to configure CORS (non-critical if CORS is already configured via Azure Portal)");
        }
    }

    private BlobContainerClient Container => _containerClient
        ?? throw new InvalidOperationException("Azure Blob Storage connection string is not configured");

    public async Task<string> UploadVideoAsync(Stream fileStream, string fileName)
    {
        var blobClient = Container.GetBlobClient(fileName);
        await blobClient.UploadAsync(fileStream, overwrite: true);

        _logger.LogInformation("Uploaded video to Azure Blob: {FileName}", fileName);
        return blobClient.Uri.ToString();
    }

    public async Task<string> GenerateSasUrlAsync(string fileName, int expiryHours = 1)
    {
        var blobClient = Container.GetBlobClient(fileName);

        if (!await blobClient.ExistsAsync())
            throw new FileNotFoundException($"Video file not found: {fileName}");

        if (!blobClient.CanGenerateSasUri)
        {
            _logger.LogWarning("Cannot generate SAS token, returning direct URL");
            return blobClient.Uri.ToString();
        }

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = Container.Name,
            BlobName = fileName,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5),
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(expiryHours)
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        return blobClient.GenerateSasUri(sasBuilder).ToString();
    }

    public async Task DeleteVideoAsync(string fileName)
    {
        await Container.GetBlobClient(fileName).DeleteIfExistsAsync();
        _logger.LogInformation("Deleted video from Azure Blob: {FileName}", fileName);
    }

    public async Task<bool> VideoExistsAsync(string fileName)
    {
        if (_containerClient == null)
            return false;

        try
        {
            return await _containerClient.GetBlobClient(fileName).ExistsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if video exists: {FileName}", fileName);
            return false;
        }
    }

    public async Task<long> GetVideoSizeAsync(string fileName)
    {
        try
        {
            var blobClient = Container.GetBlobClient(fileName);

            if (!await blobClient.ExistsAsync())
                return 0;

            var properties = await blobClient.GetPropertiesAsync();
            return properties.Value.ContentLength;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting video size: {FileName}", fileName);
            return 0;
        }
    }

    public async Task<string> DownloadToTempAsync(string fileName)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), fileName);
        await Container.GetBlobClient(fileName).DownloadToAsync(tempPath);

        _logger.LogInformation("Downloaded {FileName} to temp: {TempPath}", fileName, tempPath);
        return tempPath;
    }

    public async Task<List<string>> ListFilesAsync(string prefix = "")
    {
        var files = new List<string>();

        await foreach (var blobItem in Container.GetBlobsAsync(prefix: prefix))
            files.Add(blobItem.Name);

        return files;
    }
}
