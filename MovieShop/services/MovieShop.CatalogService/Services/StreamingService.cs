using System.Security.Cryptography;
using System.Text;

namespace MovieShop.CatalogService.Services;

public interface IStreamingService
{
    string GenerateSecureStreamingUrl(string videoFileName, int expiryMinutes = 60);
}

/// <summary>
/// Aláírt, lejáró URL-ek az nginx `secure_link` moduljához (a lokálisan tárolt
/// fájlok kiszolgálásához). Az elsődleges streaming útvonal az Azure Blob + SAS,
/// ez a kiegészítő megoldás.
///
/// A monolithoz képest a titkos kulcs konfigurációból jön, nem konstansként a
/// kódban — mikroszervizeknél a kulcsot az nginx konténerrel kell megosztani,
/// ezért környezeti változóban a helye.
/// </summary>
public class StreamingService : IStreamingService
{
    private readonly string _secretKey;
    private readonly string _baseUrl;
    private readonly ILogger<StreamingService> _logger;

    public StreamingService(IConfiguration configuration, ILogger<StreamingService> logger)
    {
        _logger = logger;
        _secretKey = configuration["Streaming:SecretKey"] ?? "moviesecretkey123";
        _baseUrl = configuration["Streaming:BaseUrl"] ?? "http://localhost:8080";
    }

    public string GenerateSecureStreamingUrl(string videoFileName, int expiryMinutes = 60)
    {
        var expiryTime = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes).ToUnixTimeSeconds();
        var path = $"/secure/{videoFileName}";

        // Az nginx secure_link_md5 direktívája: "$secure_link_expires$uri <titok>"
        // — a szóköz csak az URI és a titok között van.
        var stringToHash = $"{expiryTime}{path} {_secretKey}";

        var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(stringToHash));

        // Az nginx URL-biztos base64-et vár, kitöltő '=' jelek nélkül
        var md5Base64 = Convert.ToBase64String(hashBytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        _logger.LogDebug("Aláírt streaming URL generálva: {Path}", path);

        return $"{_baseUrl}{path}?md5={md5Base64}&expires={expiryTime}";
    }
}
