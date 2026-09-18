using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace VertexBPMN.Infrastructure.Messaging;

/// <summary>Parsiertes Ergebnis einer TypeSafe-Choice-Evaluierung.</summary>
public sealed record TypeSafeJudgmentAnswer(
    string Choice,
    double Confidence,
    IReadOnlyDictionary<string, double> Probabilities);

/// <summary>Schmaler HTTP-Client für die TypeSafe System One Evaluierung (POST /v1/systemone).</summary>
public interface ITypeSafeConformanceClient
{
    /// <summary>
    /// Fragt ein Choice-Urteil zur Envelope-Konformität ab. Gibt null zurück, wenn die Antwort nicht
    /// als gültige Choice geparst werden kann (Aufrufer behandelt das als NotEvaluated).
    /// </summary>
    Task<TypeSafeJudgmentAnswer?> AskConformanceAsync(string stateJson, CancellationToken cancellationToken);
}

/// <summary>
/// Client für TypeSafe. Nutzt einen eigenen <see cref="HttpClient"/> (langelebig wie der Prozess);
/// Produktions-Key kommt aus <see cref="TypeSafeConformanceOptions.ApiKey"/>, ausschließlich server-seitig.
/// Rückgabe-Shape laut https://docs.typesafe.ai/api.md: answers.&lt;id&gt;.choice/probabilities/confidence.
/// </summary>
public sealed class TypeSafeConformanceClient : ITypeSafeConformanceClient, IDisposable
{
    private const string QuestionId = "conformance";
    private readonly HttpClient _http;
    private readonly TypeSafeConformanceOptions _options;
    private readonly ILogger<TypeSafeConformanceClient> _logger;

    public TypeSafeConformanceClient(
        HttpClient? http,
        TypeSafeConformanceOptions options,
        ILogger<TypeSafeConformanceClient> logger)
    {
        _options = options;
        _logger = logger;
        _http = http ?? new HttpClient();
        _http.BaseAddress = new Uri(options.Endpoint);
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", options.ApiKey);
    }

    public async Task<TypeSafeJudgmentAnswer?> AskConformanceAsync(string stateJson, CancellationToken cancellationToken)
    {
        var body = new
        {
            model = _options.Model,
            state = stateJson,
            questions = new Dictionary<string, object>
            {
                [QuestionId] = new
                {
                    type = "choice",
                    instructions = "Konformiert die Envelope-Payload ihrem deklarierten Ereignisvertrag?",
                    criteria = new Dictionary<string, string?>
                    {
                        ["conforms"] = "Alle Pflichtfelder vorhanden, korrekte Typen und semantisch passend zum Vertrag",
                        ["malformed"] = "Pflichtfelder fehlen oder haben falsche Typen",
                        ["wrong_contract"] = "Felder vorhanden, aber semantisch ein anderes Ereignis",
                        ["unclear"] = "Nicht bestimmbar"
                    }
                }
            }
        };

        using var json = new StringContent(
            JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        using var response = await _http.PostAsync(_options.Endpoint, json, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("TypeSafe conformance call failed: {(int)} {Reason}. Body: {Body}",
                (int)response.StatusCode, response.ReasonPhrase, Truncate(text));
            return null;
        }

        return ParseAnswer(text);
    }

    private TypeSafeJudgmentAnswer? ParseAnswer(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("answers", out var answers)
                || !answers.TryGetProperty(QuestionId, out var answer))
                return null;

            var choice = answer.TryGetProperty("choice", out var c) ? c.GetString() : null;
            if (choice is null)
                return null;

            var confidence = answer.TryGetProperty("confidence", out var conf) && conf.ValueKind == JsonValueKind.Number
                ? conf.GetDouble()
                : 0.0;

            var probabilities = new Dictionary<string, double>();
            if (answer.TryGetProperty("probabilities", out var probs) && probs.ValueKind == JsonValueKind.Object)
                foreach (var p in probs.EnumerateObject())
                    if (p.Value.ValueKind == JsonValueKind.Number)
                        probabilities[p.Name] = p.Value.GetDouble();

            return new TypeSafeJudgmentAnswer(choice, confidence, probabilities);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Could not parse TypeSafe conformance response.");
            return null;
        }
    }

    private static string Truncate(string s, int max = 300) =>
        s.Length <= max ? s : s[..max] + "…";

    public void Dispose() => _http.Dispose();
}
