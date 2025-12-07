using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace MovieShop.Server.Services
{
    public interface IStreamingService
    {
        string GenerateSecureStreamingUrl(string videoFileName, int expiryMinutes = 60);
    }

    public class StreamingService : IStreamingService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<StreamingService> _logger;
        private const string SecretKey = "moviesecretkey123"; // Same as in nginx.conf

        public StreamingService(IConfiguration configuration, ILogger<StreamingService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public string GenerateSecureStreamingUrl(string videoFileName, int expiryMinutes = 60)
        {
            // Calculate expiry timestamp (Unix timestamp)
            var expiryTime = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes).ToUnixTimeSeconds();
            
            // Build the path (must match nginx location)
            var path = $"/secure/{videoFileName}";
            
            // Create the string to hash: "{expiry}{path} {secret}" (note the space before secret!)
            // Nginx secure_link_md5 "$secure_link_expires$uri moviesecretkey123"
            // means: MD5("{expires}{uri} {secret}") - space between URI and secret only!
            var stringToHash = $"{expiryTime}{path} {SecretKey}";
            
            _logger.LogInformation($"Generating secure link - String to hash: {stringToHash}");
            
            // Generate MD5 hash as binary
            using var md5 = MD5.Create();
            var inputBytes = Encoding.UTF8.GetBytes(stringToHash);
            var hashBytes = md5.ComputeHash(inputBytes);
            
            // Log the hex hash for debugging
            var hexHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
            _logger.LogInformation($"MD5 hex: {hexHash}");
            
            // Base64 encode and make URL-safe (Nginx expects this format)
            var md5Base64 = Convert.ToBase64String(hashBytes)
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
            
            _logger.LogInformation($"MD5 base64 URL-safe: {md5Base64}");
            
            // Get streaming server URL from configuration
            var streamingBaseUrl = _configuration["Streaming:BaseUrl"] ?? "http://localhost:8080";
            
            // Build final URL with query parameters
            return $"{streamingBaseUrl}{path}?md5={md5Base64}&expires={expiryTime}";
        }
    }
}
