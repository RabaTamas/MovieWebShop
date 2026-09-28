using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MovieShop.ChatService.Services;

/// <summary>
/// A Groq API hívásának közös rétege. A kérés törzsét (modell, tokenkeret,
/// hőmérséklet, eszközök) a hívó állítja össze — a ChatService és az AgentService
/// a monolitban is eltérő paraméterekkel dolgozott, és ez itt is így marad.
/// </summary>
public interface IGroqClient
{
    bool IsConfigured { get; }
    Task<JsonNode> SendAsync(object requestBody);
}

public class GroqClient : IGroqClient
{
    private const string Endpoint = "https://api.groq.com/openai/v1/chat/completions";

    // A monolit AgentService-e snake_case szerializálót használt; az anonim objektumok
    // mezőnevei (max_tokens, top_p, tool_call_id) ettől változatlanok maradnak.
    private static readonly JsonSerializerOptions SnakeCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<GroqClient> _logger;

    public GroqClient(HttpClient httpClient, IConfiguration configuration, ILogger<GroqClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["Groq:ApiKey"] ?? string.Empty;
    }

    public bool IsConfigured => !string.IsNullOrEmpty(_apiKey);

    public async Task<JsonNode> SendAsync(object requestBody)
    {
        if (!IsConfigured)
            throw new GroqApiException("{\"error\":{\"message\":\"Groq API key not configured\"}}");

        var json = JsonSerializer.Serialize(requestBody, SnakeCase);

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.SendAsync(request);
        var responseJson = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Groq API error: {Response}", responseJson);
            throw new GroqApiException(responseJson);
        }

        return JsonNode.Parse(responseJson)
               ?? throw new InvalidOperationException("Empty response from Groq API");
    }
}

/// <summary>
/// A Groq nem-sikeres válasza. A nyers törzs továbbadódik, mert `tool_use_failed`
/// hibánál abban van a modell félresikerült eszközhívása, amiből a szándék kinyerhető.
/// </summary>
public class GroqApiException : Exception
{
    public string ResponseJson { get; }

    public GroqApiException(string responseJson) : base($"Groq API error: {responseJson}")
        => ResponseJson = responseJson;

    public string? GetFailedGeneration()
    {
        try
        {
            var errorDoc = JsonNode.Parse(ResponseJson);

            return errorDoc?["error"]?["code"]?.GetValue<string>() == "tool_use_failed"
                ? errorDoc["error"]!["failed_generation"]?.GetValue<string>()
                : null;
        }
        catch
        {
            return null;
        }
    }
}
