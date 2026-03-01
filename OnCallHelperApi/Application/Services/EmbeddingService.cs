namespace OnCallHelperApi.Application.Services;

using OpenAI;
using OpenAI.Embeddings;

public class EmbeddingService : IEmbeddingService
{
    private const string EmbeddingModel = "text-embedding-3-small";
    private readonly EmbeddingClient _embeddingClient;

    public EmbeddingService(IConfiguration configuration)
    {
        var apiKey = configuration["OpenAI:ApiKey"];
        if (string.IsNullOrEmpty(apiKey))
            throw new ArgumentException("OpenAI API key not configured");

        // instantiate the embedding client
        _embeddingClient = new EmbeddingClient(
            model: EmbeddingModel,
            apiKey: apiKey
        );
    }

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<float>();

        // Generate embedding
        OpenAIEmbedding embeddingResult = await _embeddingClient.GenerateEmbeddingAsync(text);

        // The actual vector is a ReadOnlyMemory<float>
        return embeddingResult.ToFloats().ToArray();
    }
}