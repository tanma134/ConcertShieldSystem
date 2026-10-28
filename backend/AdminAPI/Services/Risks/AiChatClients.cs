using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AdminAPI.Services.Risks;


public interface IAiChatClient
{
    string Model { get; }
    bool IsConfigured { get; }

  
    Task<string> CompleteAsync(string system, string user, CancellationToken ct = default);
}

internal static class AiHttp
{
    public static RiskServiceException Unavailable() =>
        new(503, "AI is temporarily unavailable. The alert can still be reviewed using its scores and rules.");

   
    public static async Task<HttpResponseMessage> SendWithRetryAsync(
        HttpClient http, Func<HttpRequestMessage> build, TimeSpan maxWait, ILogger logger, CancellationToken ct)
    {
        var res = await http.SendAsync(build(), ct);
        if (res.StatusCode != HttpStatusCode.TooManyRequests) return res;

        var wait = res.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(2);
        if (wait > maxWait) wait = maxWait;
        logger.LogWarning("AI returned 429; waiting {Seconds}s before retrying once", wait.TotalSeconds);
        res.Dispose();
        if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
        return await http.SendAsync(build(), ct);
    }
}


public sealed class OpenAiCompatibleChatClient : IAiChatClient
{
    public const string GroqBaseUrl = "https://api.groq.com/openai/v1";
    public const string GroqDefaultModel = "openai/gpt-oss-120b";

    private readonly HttpClient _http;
    private readonly IConfiguration _conf;
    private readonly ILogger<OpenAiCompatibleChatClient> _logger;


    internal TimeSpan MaxRetryWait { get; set; } = TimeSpan.FromSeconds(10);

    public OpenAiCompatibleChatClient(HttpClient http, IConfiguration conf, ILogger<OpenAiCompatibleChatClient> logger)
    {
        _http = http; _conf = conf; _logger = logger;
    }

    public string Model => _conf["Ai:Model"] is { Length: > 0 } m ? m : GroqDefaultModel;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_conf["Ai:ApiKey"]);

    public async Task<string> CompleteAsync(string system, string user, CancellationToken ct = default)
    {
        var baseUrl = (_conf["Ai:BaseUrl"] is { Length: > 0 } b ? b : GroqBaseUrl).TrimEnd('/');
        var apiKey = _conf["Ai:ApiKey"]!;
        var effort = _conf["Ai:ReasoningEffort"];

  
        var body = new Dictionary<string, object?>
        {
            ["model"] = Model,
            ["messages"] = new[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user }
            },
            ["temperature"] = 0.2,
            ["max_completion_tokens"] = 2000
        };
        if (!string.IsNullOrWhiteSpace(effort)) body["reasoning_effort"] = effort;

        HttpRequestMessage Build()
        {
            var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/chat/completions") { Content = JsonContent.Create(body) };
            req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
            return req;
        }

        try
        {
            using var res = await AiHttp.SendWithRetryAsync(_http, Build, MaxRetryWait, _logger, ct);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("AI API returned {Status}", (int)res.StatusCode);
                throw AiHttp.Unavailable();
            }

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var choice = doc.RootElement.GetProperty("choices")[0];
            var content = choice.GetProperty("message").TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String
                ? c.GetString() : null;
            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("AI returned empty content (finish_reason={Reason})",
                    choice.TryGetProperty("finish_reason", out var fr) ? fr.ToString() : "?");
                throw new RiskServiceException(503, "The AI returned empty content.");
            }
            return content;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                      or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            if (ct.IsCancellationRequested) throw;
            _logger.LogWarning(ex, "AI API call failed");
            throw AiHttp.Unavailable();
        }
    }
}


public sealed class AnthropicChatClient : IAiChatClient
{
    private readonly HttpClient _http;
    private readonly IConfiguration _conf;
    private readonly ILogger<AnthropicChatClient> _logger;

    internal TimeSpan MaxRetryWait { get; set; } = TimeSpan.FromSeconds(10);

    public AnthropicChatClient(HttpClient http, IConfiguration conf, ILogger<AnthropicChatClient> logger)
    {
        _http = http; _conf = conf; _logger = logger;
    }

    private string? ApiKey => _conf["Ai:ApiKey"] is { Length: > 0 } k ? k : _conf["Anthropic:ApiKey"];
    public string Model => _conf["Ai:Model"] is { Length: > 0 } m ? m : (_conf["Anthropic:Model"] is { Length: > 0 } m2 ? m2 : "claude-sonnet-5");
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);

    public async Task<string> CompleteAsync(string system, string user, CancellationToken ct = default)
    {
        var baseUrl = (_conf["Ai:BaseUrl"] is { Length: > 0 } b ? b : "https://api.anthropic.com").TrimEnd('/');
        var apiKey = ApiKey!;
        var body = new
        {
            model = Model,
            max_tokens = 800,
            system,
            messages = new[] { new { role = "user", content = user } }
        };

        HttpRequestMessage Build()
        {
            var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/v1/messages") { Content = JsonContent.Create(body) };
            req.Headers.Add("x-api-key", apiKey);
            req.Headers.Add("anthropic-version", "2023-06-01");
            return req;
        }

        try
        {
            using var res = await AiHttp.SendWithRetryAsync(_http, Build, MaxRetryWait, _logger, ct);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("AI API returned {Status}", (int)res.StatusCode);
                throw AiHttp.Unavailable();
            }
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var text = doc.RootElement.GetProperty("content").EnumerateArray()
                .Where(c => c.TryGetProperty("type", out var t) && t.GetString() == "text")
                .Select(c => c.GetProperty("text").GetString()).FirstOrDefault();
            return string.IsNullOrWhiteSpace(text) ? throw new RiskServiceException(503, "The AI returned empty content.") : text;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            if (ct.IsCancellationRequested) throw;
            _logger.LogWarning(ex, "AI API call failed");
            throw AiHttp.Unavailable();
        }
    }
}