using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SohailOS.Ecosystem;

public sealed class OpenAICompatibleEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly string? _apiKey;
    private readonly int _expectedDimensions;

    public OpenAICompatibleEmbeddingProvider(
        HttpClient httpClient,
        string baseUrl,
        string model,
        string? apiKey = null,
        int expectedDimensions = 1536)
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
        _apiKey = apiKey;
        _expectedDimensions = expectedDimensions > 0 ? expectedDimensions : 1536;

        Generation = new EmbeddingGeneration(
            KnowledgeIdentity.EmbeddingGenerationId(
                "openai-compatible",
                model,
                _expectedDimensions,
                "l2-v1"),
            "openai-compatible",
            model,
            _expectedDimensions,
            "l2-v1");
    }

    public bool IsAvailable =>
        !string.IsNullOrWhiteSpace(_baseUrl) &&
        !string.IsNullOrWhiteSpace(Generation.Model);

    public EmbeddingGeneration Generation { get; }

    public async Task<float[]?> EmbedAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        if (!IsAvailable)
            return null;

        var payload = JsonSerializer.Serialize(new
        {
            model = Generation.Model,
            input = text
        });

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"{_baseUrl}/embeddings")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(_apiKey))
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Embedding provider returned {(int)response.StatusCode}: {body}");
        }

        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        var embedding = json.RootElement
            .GetProperty("data")[0]
            .GetProperty("embedding")
            .EnumerateArray()
            .Select(x => x.GetSingle())
            .ToArray();

        if (embedding.Length != _expectedDimensions)
            throw new InvalidDataException(
                $"Embedding dimension mismatch: expected {_expectedDimensions}, received {embedding.Length}.");

        NormalizeInPlace(embedding);
        return embedding;
    }

    private static void NormalizeInPlace(float[] vector)
    {
        var norm = Math.Sqrt(vector.Sum(x => (double)x * x));
        if (norm <= double.Epsilon)
            return;

        for (var i = 0; i < vector.Length; i++)
            vector[i] = (float)(vector[i] / norm);
    }
}
